// Services\PlcTagManager.cs
using Gemini.Models;
using S7.Net;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;

namespace Gemini.Services
{
    /// <summary>
    /// Singleton: verwaltet alle angemeldeten Clients & Tags, pollt SPSen periodisch und sendet Änderungen.
    /// Optimierungen:
    /// - persistente PLC-Verbindungen über PlcConnectionManager
    /// - block-bündeltes Lesen (Merge von Bereichen)
    /// - asynchrones Polling mit begrenzter Parallelität
    /// - Vermeidung doppelten Pollens durch globale Subscriber-Registry
    /// </summary>
    public sealed partial class PlcTagManager : IDisposable
    {
        // Precompiled regexes
        [GeneratedRegex(@"^DB(\d+)\.DBX(\d+)\.(\d+)$", RegexOptions.IgnoreCase)]
        private static partial Regex DbxRegex();
        [GeneratedRegex(@"^DB(\d+)\.DBB(\d+)$", RegexOptions.IgnoreCase)]
        private static partial Regex DbbRegex();
        [GeneratedRegex(@"^DB(\d+)\.DBW(\d+)$", RegexOptions.IgnoreCase)]
        private static partial Regex DbwRegex();
        [GeneratedRegex(@"^DB(\d+)\.DBD(\d+)$", RegexOptions.IgnoreCase)]
        private static partial Regex DbdRegex();
        [GeneratedRegex(@"^DB(\d+)\.(DBB|DBW|DBD|DBX)(\d+)(?:\.(\d+))?$", RegexOptions.IgnoreCase)]
        private static partial Regex GenericRegex();

        internal Guid DataBaseClientIdentifier { get; set; }

        private readonly ConcurrentDictionary<string, ParsedAddress?> _parseCache = new(StringComparer.Ordinal);
        // tagName -> subscribers (clientId -> byte placeholder)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _subscribers = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<Guid, ClientEntry> _clients = new();
        internal readonly ConcurrentDictionary<Guid, IPAddress> ClientInfo = new();
        private readonly ConcurrentDictionary<string, Plc> _plcConfigs = new(StringComparer.OrdinalIgnoreCase);

        private readonly PlcConnectionManager _connectionManager = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _plcLocks = new(StringComparer.OrdinalIgnoreCase);

        // global concurrency for PLC polls
        private readonly SemaphoreSlim _globalConcurrency;
        private readonly CancellationTokenSource _cts = new();
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
        private readonly Task _pollTask;

        private const int MaxBlockBytes = 2000; // maximale Bytes pro Block-Read
        private const double ChangeThreshold = 0.1; // minimaler Unterschied, um als Wertänderung zu gelten

        public static PlcTagManager Instance { get; } = new PlcTagManager();

        private PlcTagManager()
        {
            _globalConcurrency = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount));
            var loaded = LoadPlcConfigs();
            foreach (var kv in loaded) _plcConfigs[kv.Key] = kv.Value;
            _pollTask = Task.Run(PollLoop);
        }

        private static ConcurrentDictionary<string, Plc> LoadPlcConfigs()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                var section = config.GetSection("Plcs");
                var dict = new ConcurrentDictionary<string, Plc>(StringComparer.OrdinalIgnoreCase);
                foreach (var child in section.GetChildren())
                {
                    var key = child.Key;
                    var host = child.GetValue<string>("Host");
                    var cpu = child.GetValue<string>("Cpu") ?? "S71500";
                    var rack = child.GetValue<short?>("Rack") ?? 0;
                    var slot = child.GetValue<short?>("Slot") ?? 1;

                    CpuType cpuType = cpu switch
                    {
                        "S71200" => CpuType.S71200,
                        "S7400" => CpuType.S7400,
                        "S7300" => CpuType.S7300,
                        _ => CpuType.S71500
                    };

                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        dict[key] = new Plc(cpuType, host, rack, slot);
                    }
                }

                Db.Db.SelectActivePlcs().ToList().ForEach(kv => dict[kv.Key] = kv.Value);
                return dict;
            }
            catch
            {
                return new ConcurrentDictionary<string, Plc>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public void AddOrUpdateClient(Guid clientId, IPAddress ip, JsonTag[] tags, Func<JsonTag[], Task> sendCallback)
        {
            tags ??= []; // Wenn Tags = null, leeres Array verwenden
            var entry = new ClientEntry(sendCallback, tags);
            _clients.AddOrUpdate(clientId, entry, (_, __) => entry);
            ClientInfo.TryAdd(clientId, ip);

            // persist names async
            _ = Task.Run(() => Db.Db.InsertTagNamesBulk(tags));

            // register subscribers
            foreach (var t in tags)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.N)) continue;
                var parsed = ParseTagCached(t);
                if (parsed == null) continue;
                var subs = _subscribers.GetOrAdd(t.N, _ => new ConcurrentDictionary<Guid, byte>());
                subs[clientId] = 0;
            }

            if (ip.MapToIPv4() == IPAddress.Loopback)
                Db.Db.DbLogInfo($"Lokalen Client mit {tags.Length} Tags hinzugefügt/aktualisiert.");
            else
                Db.Db.DbLogInfo($"Client an {ip.MapToIPv4()} mit {tags.Length} Tags hinzugefügt/aktualisiert.");
        }

        public void RemoveClient(Guid clientId)
        {            
            Db.Db.DbLogInfo($"Client an {ClientInfo.GetValueOrDefault(clientId)?.MapToIPv4()} entfernt.");
            ClientInfo.TryRemove(clientId, out _);

            _clients.TryRemove(clientId, out _);
            foreach (var kv in _subscribers)
            {
                kv.Value.TryRemove(clientId, out _);
                if (kv.Value.IsEmpty)
                {
                    _subscribers.TryRemove(kv.Key, out _);
                    _parseCache.TryRemove(kv.Key, out _);
                }
            }

           
            //Datenbank schreiben neu initiieren, wenn der entsprechende CVlient entfernt wurde.
            if (clientId == DataBaseClientIdentifier) 
                Db.Db.InitiateDbWriting();
        }

        private async Task PollLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var activeTags = _subscribers.Keys.ToList();
                    if (activeTags.Count == 0)
                    {
                        await Task.Delay(_pollInterval, _cts.Token);
                        continue;
                    }

                    // group by plc ip
                    var tagsByPlc = new Dictionary<string, List<(string tagName, ParsedAddress parsed)>>();
                    foreach (var tagName in activeTags)
                    {
                        if (!_parseCache.TryGetValue(tagName, out var parsed) || parsed == null) continue;
                        var ip = parsed.Value.Ip;
                        if (!tagsByPlc.TryGetValue(ip, out var list)) { list = []; tagsByPlc[ip] = list; }
                        list.Add((tagName, parsed.Value));
                    }

                    var plcTasks = new List<Task>();
                    foreach (var kv in tagsByPlc)
                    {
                        await _globalConcurrency.WaitAsync(_cts.Token);
                        var plcIp = kv.Key;
                        var tagsForPlc = kv.Value;
                        plcTasks.Add(Task.Run(async () =>
                        {
                            try { await ProcessPlcPoll(plcIp, tagsForPlc); }
                            finally { _globalConcurrency.Release(); }
                        }, _cts.Token));
                    }

                    await Task.WhenAll(plcTasks);
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    //TEST Wieder löschen
                    Db.Db.DbLogInfo("halte PollLoop() am Leben...");
                    // keep loop alive
                }

                try { await Task.Delay(_pollInterval, _cts.Token); } catch (TaskCanceledException) { break; }
            }
        }

        private async Task ProcessPlcPoll(string plcIp, List<(string tagName, ParsedAddress parsed)> tagsForPlc)
        {
            if (tagsForPlc == null || tagsForPlc.Count == 0) return;
            var plc = GetOrCreatePlc(plcIp);
            if (plc == null) return;

            var plcLock = _plcLocks.GetOrAdd(plcIp, _ => new SemaphoreSlim(1, 1));
            await plcLock.WaitAsync(_cts.Token);
            try
            {
                // ensure persistent connection
                _connectionManager.GetOrCreate(plcIp, plc);
                _connectionManager.EnsureOpen(plcIp);
                if (!plc.IsConnected) { Db.Db.DbLogReadFailure(plcIp, 0, 0, 0); return; }

                var byDb = tagsForPlc.GroupBy(t => t.parsed.Db);
                foreach (var dbGroup in byDb)
                {
                    int db = dbGroup.Key;
                    var ranges = dbGroup.Select(x => (x.tagName, x.parsed, start: x.parsed.Offset, end: x.parsed.Offset + x.parsed.Size - 1))
                                       .OrderBy(x => x.start).ToList();

                    var blocks = new List<(int start, int length, List<(string tagName, ParsedAddress parsed)>)>();
                    int idx = 0;
                    while (idx < ranges.Count)
                    {
                        int blockStart = ranges[idx].start;
                        int blockEnd = ranges[idx].end;
                        var members = new List<(string, ParsedAddress)> { (ranges[idx].tagName, ranges[idx].parsed) };
                        idx++;
                        while (idx < ranges.Count)
                        {
                            int nextEnd = ranges[idx].end;
                            int tentativeEnd = Math.Max(blockEnd, nextEnd);
                            int tentativeLength = tentativeEnd - blockStart + 1;
                            if (tentativeLength > MaxBlockBytes) break;
                            blockEnd = tentativeEnd;
                            members.Add((ranges[idx].tagName, ranges[idx].parsed));
                            idx++;
                        }
                        blocks.Add((blockStart, blockEnd - blockStart + 1, members));
                    }

                    foreach (var block in blocks)
                    {
                        byte[]? blockBytes;
                        try
                        {
                            blockBytes = await Task.Run(() => plc.ReadBytes(DataType.DataBlock, db, block.start, block.length), _cts.Token);
                        }
                        catch
                        {
                            Db.Db.DbLogReadFailure(plcIp, db, block.start, block.length);
                            continue;
                        }
                        if (blockBytes == null || blockBytes.Length == 0) continue;

                        foreach (var (tagName, parsed) in block.Item3)
                        {
                            int relOffset = parsed.Offset - block.start;
                            object? newValue;
                            try { newValue = ExtractValue(blockBytes, relOffset, parsed); }
                            catch { continue; }
                            if (newValue == null) continue;

                            // dispatch to subscribers
                            if (!_subscribers.TryGetValue(tagName, out var subs) || subs.IsEmpty) continue;

                            foreach (var clientId in subs.Keys)
                            {
                                if (!_clients.TryGetValue(clientId, out var clientEntry)) continue;
                                var existingIndex = Array.FindIndex(clientEntry.Tags, x => x.N == tagName);
                                if (existingIndex < 0) continue;
                                var existingTag = clientEntry.Tags[existingIndex];
                                var oldValObj = existingTag.V;
                                double oldVal = oldValObj is null ? double.NaN : Convert.ToDouble(oldValObj);
                                double newValD = Convert.ToDouble(newValue);
                                bool changed = double.IsNaN(oldVal) || Math.Abs(oldVal - newValD) > ChangeThreshold;
                                if (!changed) continue;

                                var updated = new JsonTag(existingTag.N, newValue, DateTime.UtcNow);
                                clientEntry.Tags[existingIndex] = updated;

                                _ = Task.Run(async () =>
                                {
                                    try { await clientEntry.SendCallback([updated]); }
                                    catch (Exception ex) {
                                        Db.Db.DbLogWarn($"Fehler beim Senden an Client {clientId}, entferne Client. clientEntry. |{clientEntry}|{updated}| " + ex);
                                        RemoveClient(clientId); 
                                    }
                                });
                            }
                        }
                    }
                }
            }
            finally
            {
                plcLock.Release();
            }
        }

        private ParsedAddress? ParseTagCached(JsonTag tag)
        {
            if (tag == null || string.IsNullOrWhiteSpace(tag.N)) return null;
            return _parseCache.GetOrAdd(tag.N, key => ParseTagNoCache(key));
        }

        private ParsedAddress? ParseTagNoCache(string tagName)
        {
            var parts = tagName.Split('_', 2);
            if (parts.Length != 2) return null;
            var plcName = parts[0].Trim();
            var addr = parts[1].Trim().Replace('_', '.');

            if (!_plcConfigs.TryGetValue(plcName, out Plc? plc)) return null;
            var ip = plc.IP;

            var dbxMatch = DbxRegex().Match(addr);
            if (dbxMatch.Success)
            {
                int db = int.Parse(dbxMatch.Groups[1].Value);
                int byteOffset = int.Parse(dbxMatch.Groups[2].Value);
                int bit = int.Parse(dbxMatch.Groups[3].Value);
                return new ParsedAddress(ip, db, byteOffset, TagDataType.Bit, size: 1, bit: bit);
            }

            var dbbMatch = DbbRegex().Match(addr);
            if (dbbMatch.Success) return new ParsedAddress(ip, int.Parse(dbbMatch.Groups[1].Value), int.Parse(dbbMatch.Groups[2].Value), TagDataType.Byte, size: 1);
            var dbwMatch = DbwRegex().Match(addr);
            if (dbwMatch.Success) return new ParsedAddress(ip, int.Parse(dbwMatch.Groups[1].Value), int.Parse(dbwMatch.Groups[2].Value), TagDataType.Int16, size: 2);
            var dbdMatch = DbdRegex().Match(addr);
            if (dbdMatch.Success) return new ParsedAddress(ip, int.Parse(dbdMatch.Groups[1].Value), int.Parse(dbdMatch.Groups[2].Value), TagDataType.Int32, size: 4);

            var genericMatch = GenericRegex().Match(addr);
            if (genericMatch.Success)
            {
                int db = int.Parse(genericMatch.Groups[1].Value);
                var t = genericMatch.Groups[2].Value.ToUpperInvariant();
                int offset = int.Parse(genericMatch.Groups[3].Value);
                if (t == "DBB") return new ParsedAddress(ip, db, offset, TagDataType.Byte, size: 1);
                if (t == "DBW") return new ParsedAddress(ip, db, offset, TagDataType.Int16, size: 2);
                if (t == "DBD") return new ParsedAddress(ip, db, offset, TagDataType.Int32, size: 4);
            }

            return null;
        }

        private static object? ExtractValue(byte[] blockBytes, int relOffset, ParsedAddress parsed)
        {
            if (relOffset < 0 || relOffset >= blockBytes.Length) return null;
            switch (parsed.DataType)
            {
                case TagDataType.Bit:
                    {
                        if (relOffset >= blockBytes.Length) return null;
                        byte b = blockBytes[relOffset];
                        int bit = parsed.Bit ?? 0;
                        return ((b >> (7 - bit)) & 1) == 1;
                    }
                case TagDataType.Byte:
                    return blockBytes[relOffset];
                case TagDataType.Int16:
                    {
                        if (relOffset + 1 >= blockBytes.Length) return null;
                        short val = BinaryPrimitives.ReadInt16BigEndian(blockBytes.AsSpan(relOffset, 2));
                        return val;
                    }
                case TagDataType.Int32:
                    {
                        if (relOffset + 3 >= blockBytes.Length) return null;
                        int val = BinaryPrimitives.ReadInt32BigEndian(blockBytes.AsSpan(relOffset, 4));
                        return val;
                    }
                default:
                    return null;
            }
        }

        internal Dictionary<string, Plc> GetAllPlcs()
        {
            return _plcConfigs.ToDictionary(k => k.Key, v => v.Value);
        }

        private Plc? GetOrCreatePlc(string plcName)
        {
            return _plcConfigs.GetOrAdd(plcName, key =>
            {
                try { return new Plc(CpuType.S71500, key, 0, 0); }
                catch { return new Plc(CpuType.S71500, "0.0.0.0", 0, 0); }
            });
        }

        internal void UpdatePlcConfig(string plcName, Plc plc)
        {
            _plcConfigs[plcName] = plc;
            _connectionManager.UpdatePlc(plcName, plc);
        }

        internal void RemovePlcConfig(string plcName)
        {
            _plcConfigs.Remove(plcName, out _);
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _pollTask.Wait(); } catch { }
            _connectionManager.DisposeAll();
            foreach (var plc in _plcConfigs.Values) { try { plc.Close(); } catch { } }
            foreach (var s in _plcLocks.Values) { try { s.Dispose(); } catch { } }
            _globalConcurrency.Dispose();
            _cts.Dispose();
        }

        private class ClientEntry(Func<JsonTag[], Task> sendCallback, JsonTag[] tags)
        {
            public Func<JsonTag[], Task> SendCallback { get; } = sendCallback; 
            public JsonTag[] Tags { get; set; } = tags;
        }

        private readonly struct ParsedAddress(string ip, int db, int offset, TagDataType dataType, int size, int? bit = null)
        {
            public string Ip { get; } = ip;
            public int Db { get; } = db;
            public int Offset { get; } = offset;
            public TagDataType DataType { get; } = dataType;
            public int Size { get; } = size;
            public int? Bit { get; } = bit;
        }

        private enum TagDataType { Bit, Byte, Int16, Int32 }
    }
}

#region Funktionaler PlcTagManager, aber mit parallelen polls bei mehreren Clients
/*
//Services\PlcTagManager.cs
using Gemini.Models;
using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Text;
using S7.Net;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace Gemini.Services
{





    /// <summary>
    /// Singleton: verwaltet alle angemeldeten Clients & Tags, pollt SPSen periodisch und sendet Änderungen.
    /// Batch-Read-Optimierung: gruppiert Tags pro PLC-Key und DB, liest zusammenhängende Byte-Blöcke per ReadBytes und extrahiert einzelne Tag-Werte.
    /// Erwartet JsonTag.N im Format "A{int}_DB{n}_DB{TYPE}{offset}" (z. B. "A1_DB1_DBW10").
    /// PLC-Mapping A{int} -> Host/Cpu/Rack/Slot wird aus appsettings.json Sektion "Plcs" geladen.
    /// </summary>
    public sealed partial class PlcTagManager : IDisposable
    {
        // Precompiled regexes to avoid recompilation/allocation on each call     
        [GeneratedRegex(@"^DB(\d+)\.DBX(\d+)\.(\d+)$", RegexOptions.IgnoreCase)]
        private static partial Regex DbxRegex();

        [GeneratedRegex(@"^DB(\d+)\.DBB(\d+)$", RegexOptions.IgnoreCase)]
        private static partial Regex DbbRegex();

        [GeneratedRegex(@"^DB(\d+)\.DBW(\d+)$", RegexOptions.IgnoreCase)]
        private static partial Regex DbwRegex();

        [GeneratedRegex(@"^DB(\d+)\.DBD(\d+)$", RegexOptions.IgnoreCase)]
        private static partial Regex DbdRegex();

        [GeneratedRegex(@"^DB(\d+)\.(DBB|DBW|DBD|DBX)(\d+)(?:\.(\d+))?$", RegexOptions.IgnoreCase)]
        private static partial Regex GenericRegex();


        // Cache parsed addresses by tag name to avoid repeated regex parsing
        private readonly ConcurrentDictionary<string, ParsedAddress?> _parseCache = new(StringComparer.Ordinal);

        public static PlcTagManager Instance { get; } = new PlcTagManager();

        private readonly ConcurrentDictionary<Guid, ClientEntry> _clients = new();
        private readonly ConcurrentDictionary<string, Plc> _plcConfigs = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _plcLocks = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
        private readonly Task _pollTask;

        // Maximale Anzahl Bytes pro Block-Read (verringert die Größe großer Reads)
        private const int MaxBlockBytes = 2000;
        
        private PlcTagManager()
        {
            _plcConfigs = LoadPlcConfigs() ?? new ConcurrentDictionary<string, Plc>();

            //Console.WriteLine($"{_plcConfigs.Count} SPS-Konfigurationen geladen.");

    #if DEBUG
            foreach (var item in _plcConfigs)
            {
                Console.WriteLine($"SPS {item.Key}: {item.Value.CPU} {item.Value.IP}");
            }
    #endif
            _pollTask = Task.Run(PollLoop);
        }

        private static ConcurrentDictionary<string, Plc> LoadPlcConfigs()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                var section = config.GetSection("Plcs");
                var dict = new ConcurrentDictionary<string, Plc>(StringComparer.OrdinalIgnoreCase);
                foreach (var child in section.GetChildren())
                {
                    var key = child.Key;
                    var host = child.GetValue<string>("Host");
                    var cpu = child.GetValue<string>("Cpu") ?? "S71500";
                    var rack = child.GetValue<short?>("Rack") ?? 0;
                    var slot = child.GetValue<short?>("Slot") ?? 1;

                    CpuType cpuType = CpuType.S71500;

                    switch (cpu)
                    {
                        case "S71500":
                            cpuType = CpuType.S71500;
                            break;
                        case "S71200":
                            cpuType = CpuType.S71200;
                            break;                        
                        case "S7400":
                            cpuType = CpuType.S7400;
                            break;
                        case "S7300":
                            cpuType = CpuType.S7300;
                            break;
                    }


                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        dict[key] = new Plc(cpuType, host, rack, slot);
                    }
                }
                return dict;
            }
            catch
            {
                // Bei Fehlern leere Map zurückgeben; GetOrCreatePlc fällt auf Fallback-Verhalten zurück.
                return new ConcurrentDictionary<string, Plc>(StringComparer.OrdinalIgnoreCase);
            }
        }


        public void AddOrUpdateClient(Guid clientId, JsonTag[] tags, Func<JsonTag[], Task> sendCallback)
        {
            // Invalidate cache entries for tags belonging to this client to avoid stale mapping if config changed
            foreach (var t in tags)
            {
                if (!string.IsNullOrEmpty(t?.N))
                    _parseCache.TryRemove(t.N, out _);
            }

            //Hier prüfen, ob alle TagNames in der Datenbank bekannt sind
            Db.Db.InsertTagNamesBulk(tags);

            var entry = new ClientEntry(sendCallback, tags);
            _clients.AddOrUpdate(clientId, entry, (_, __) => entry);

#if DEBUG
         /*   List<string> x = [];
            foreach (var client in _clients)
            {
                foreach (var tag in client.Value.Tags)
                {
                    x.Add(tag.N);
                }                
            }

            Console.WriteLine($"{string.Join(", ", x.Order())}"); //*//*
#endif
        }

        public void RemoveClient(Guid clientId)
        {
#if DEBUG
            //Console.WriteLine($"Client {clientId} wird entfernt.");
#endif
            _clients.TryRemove(clientId, out _);
        }

        /// <summary>
        /// Retrieves a dictionary mapping IP addresses to lists of client tag information, including the client
        /// identifier, associated tag, and parsed address details.
        /// </summary>
        /// <remarks>Only tags with successfully parsed address information are included in the result.
        /// Tags that cannot be parsed are excluded from the returned dictionary.</remarks>
        /// <returns>A dictionary where each key is an IP address and each value is a list of tuples containing the client ID,
        /// the corresponding tag, and the parsed address information. If no tags are associated with an IP address, the
        /// dictionary will not contain an entry for that IP.</returns>
        private Dictionary<string, List<(Guid clientId, JsonTag tag, ParsedAddress parsed)>> GetTagsForClient()
        {
            // Sammle alle benötigten (ip, db, requests)
            var byIp = new Dictionary<string, List<(Guid clientId, JsonTag tag, ParsedAddress parsed)>>();

            foreach (var kv in _clients)
            {
                var clientId = kv.Key;
                var entry = kv.Value;

                foreach (var t in entry.Tags)
                {
                    ParsedAddress? parsed = ParseTagCached(t);
                    if (parsed == null) continue;

                    string ip = parsed.Value.Ip;

                    if (!byIp.TryGetValue(ip, out var list))
                    {
                        list = [];
                        byIp[ip] = list;
                    }

                    list.Add((clientId, t, parsed.Value));
                }
            }

            return byIp;
        }

        private async Task PollLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {

                    // Sammle alle benötigten (ip, db, requests)
                    var byIp = GetTagsForClient();

                    foreach (var kv in byIp)
                    {
                        var ip = kv.Key;
                        var requests = kv.Value;

                        var plc = GetOrCreatePlc(ip);

                        var plcLock = _plcLocks.GetOrAdd(ip, _ => new SemaphoreSlim(1, 1));

                        await plcLock.WaitAsync();
                        try
                        {
                            #region SPS-Verbindung öffnen
                            if (plc == null)
                            {
                                Console.WriteLine($"Keine SPS-Konfiguration für {ip} gefunden.");
                                continue;
                            }

                            if (!plc.IsConnected)
                            {
                                try { plc.Open(); }
                                catch {
                                    Console.WriteLine($"Verbindung zur SPS {ip} konnte nicht hergestellt werden.");
                                    continue;
                                }
                            }

                            if (!plc.IsConnected)
                            {
                                Console.WriteLine($"Verbindung zur SPS {ip} konnte wirklich nicht hergestellt werden.");
                                continue;
                            }
                            #endregion

                            // Gruppiere requests pro DB-Nummer
                            var byDb = requests.GroupBy(r => r.parsed.Db);

                            foreach (var dbGroup in byDb)
                            {
                                int db = dbGroup.Key;
                                var list = dbGroup.ToList();

                                // Erzeuge eine Liste von benötigten Ranges (start, end)
                                var ranges = list
                                    .Select(r => new { r.clientId, r.tag, r.parsed, start = r.parsed.Offset, end = r.parsed.Offset + r.parsed.Size - 1 })
                                    .OrderBy(x => x.start)
                                    //.DistinctBy(g => g.tag.N) // TEST: Datenpunkte nicht doppelt abfragen funktionietr nicht, da unterschiedliche Clients unterschiedliche Tags mit gleicher Adresse haben können
                                    .ToList();

                                // Merge zu Blöcken, begrenze Größe durch MaxBlockBytes
                                var blocks = new List<(int start, int length, List<(Guid clientId, JsonTag tag, ParsedAddress parsed)>)>();
                                int iIdx = 0;
                                while (iIdx < ranges.Count)
                                {
                                    int blockStart = ranges[iIdx].start;
                                    int blockEnd = ranges[iIdx].end;

                                    //Console.WriteLine($"DB{db}-Range Start {blockStart}, Ende {blockEnd}, Länge {blockEnd - blockStart}");

                                    var blockMembers = new List<(Guid, JsonTag, ParsedAddress)>
                                    {
                                        (ranges[iIdx].clientId, ranges[iIdx].tag, ranges[iIdx].parsed)
                                    };
                                    iIdx++;

                                    while (iIdx < ranges.Count)
                                    {
                                        int nextStart = ranges[iIdx].start;
                                        int nextEnd = ranges[iIdx].end;

                                        int tentativeStart = blockStart;
                                        int tentativeEnd = Math.Max(blockEnd, nextEnd);                                        
                                        int tentativeLength = tentativeEnd - tentativeStart + 1;

                                        //Console.WriteLine($"DB{db}-Block Start {tentativeStart}, Ende {tentativeEnd}, Länge {tentativeLength}");

                                        if (tentativeLength > MaxBlockBytes)
                                        {
                                            // block would grow too big -> start new block
#if DEBUG
                                            Console.WriteLine("block would grow too big -> start new block");
#endif
                                            break;
                                        }

                                        // extend block
                                        blockEnd = tentativeEnd;
                                        blockMembers.Add((ranges[iIdx].clientId, ranges[iIdx].tag, ranges[iIdx].parsed));                                        
                                        iIdx++;
                                    }

                                    blocks.Add((blockStart, blockEnd - blockStart + 1, blockMembers));
                                    //Console.WriteLine($"Auswertung DB{db}-Block Start {blockStart}, Länge {blockEnd - blockStart + 1}, Anzahl {blockMembers.Count}");
                                }

                                // Für jeden Block: ReadBytes und extrahiere Werte
                                foreach (var block in blocks)
                                {
                                    byte[] blockBytes;
                                    try
                                    {
                                        // S7.Net: ReadBytes(DataType, db, start, count)
                                        blockBytes = plc.ReadBytes(DataType.DataBlock, db, block.start, block.length);                                        
                                    }
                                    catch
                                    {
                                        // Lesefehler -> skip
#if DEBUG
                                        Console.WriteLine($"Fehler beim Lesen von DB{db} ab Offset {block.start} Länge {block.length} von SPS {ip}.");
#endif
                                        continue;
                                    }

                                    if (blockBytes == null || blockBytes.Length == 0)
                                    {
                                        Console.WriteLine($"Leere Daten beim Lesen von DB{db} ab Offset {block.start} Länge {block.length} von SPS {ip}.");
                                        continue;
                                    }

                                    // Verarbeite alle Member in diesem Block
                                    foreach (var (clientId, tag, parsed) in block.Item3)
                                    {
                                        var relOffset = parsed.Offset - block.start;
                                        object? newValue = null;
                                        try
                                        {
                                            newValue = ExtractValue(blockBytes, relOffset, parsed);
#if DEBUG
                                           // Console.WriteLine($"Gelesener Wert für Tag {tag.N}: {newValue}");
#endif
                                        }
                                        catch
                                        {
#if DEBUG
                                            Console.WriteLine($"Extrahieren des Werte für {tag.N} nicht möglich.");
#endif
                                            continue;
                                        }

                                        if (newValue == null)
                                        {
#if DEBUG
                                            Console.WriteLine($"Kein Wert für Tag {tag.N} extrahiert.");
#endif
                                            continue;
                                        }

                                        // Vergleiche und ggf. senden
                                        if (!_clients.TryGetValue(clientId, out var clientEntry))
                                        {
#if DEBUG
                                            Console.WriteLine($"Client {clientId} nicht mehr vorhanden.");
#endif
                                            continue;
                                        }

                                        var existingIndex = Array.FindIndex(clientEntry.Tags, x => x.N == tag.N);
                                        if (existingIndex < 0)
                                        {
#if DEBUG
                                            Console.WriteLine($"Tag {tag.N} nicht mehr im Client {clientId} vorhanden.");
#endif
                                            continue;
                                        }

                                        var existingTag = clientEntry.Tags[existingIndex];
                                        var oldVal = existingTag.V;

                                        if (!AreEqual(oldVal, newValue)) //ToDO: evtl. einen Offset einbauen (LogDeepBand)
                                        {
                                            var updated = new JsonTag(existingTag.N, newValue, DateTime.UtcNow);
                                            clientEntry.Tags[existingIndex] = updated;

                                            try
                                            {
                                                await clientEntry.SendCallback([updated]);
                                            }
                                            catch
                                            {
                                                // Fehler beim Senden -> entferne Client defensiv
#if DEBUG
                                                Console.WriteLine($"Fehler beim Senden an {clientId}");
#endif
                                                RemoveClient(clientId);
                                            }
                                        }                                
                                    }



                                }
                            }
                        }
                        finally
                        {
                            plcLock.Release();
                        }
                    }
                }
                catch
                {
                    // Swallow exceptions to keep loop alive; ergänzen Sie Logging bei Bedarf.
    #if DEBUG
                    Console.WriteLine("Fehler in PollLoop().");
    #endif
                }

                try
                {
                    await Task.Delay(_pollInterval, _cts.Token);
                }
                catch (TaskCanceledException) { break; }
            }

            Console.WriteLine("PollLoop() beendet."); 
        }

        private static bool AreEqual(object? a, object? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        }

        // New cached parse entry point
        private ParsedAddress? ParseTagCached(JsonTag tag)
        {
            if (tag == null || string.IsNullOrWhiteSpace(tag.N)) return null;
            return _parseCache.GetOrAdd(tag.N, key => ParseTagNoCache(key));
        }

        // Original parsing logic refactored to accept tag.N string
        private ParsedAddress? ParseTagNoCache(string tagName)
        {
            var parts = tagName.Split('_', 2);
            if (parts.Length != 2) return null;
            var plcName = parts[0].Trim();
            var addr = parts[1].Trim().Replace('_', '.');

            if (!_plcConfigs.TryGetValue(plcName, out Plc? plc))
            {
                return null;
            }

            var ip = plc.IP;

            var dbxMatch = DbxRegex().Match(addr);
            if (dbxMatch.Success)
            {
                int db = int.Parse(dbxMatch.Groups[1].Value);
                int byteOffset = int.Parse(dbxMatch.Groups[2].Value);
                int bit = int.Parse(dbxMatch.Groups[3].Value);
                return new ParsedAddress(ip, db, byteOffset, TagDataType.Bit, size: 1, bit: bit);
            }

            var dbbMatch = DbbRegex().Match(addr);
            if (dbbMatch.Success)
            {
                int db = int.Parse(dbbMatch.Groups[1].Value);
                int offset = int.Parse(dbbMatch.Groups[2].Value);
                return new ParsedAddress(ip, db, offset, TagDataType.Byte, size: 1);
            }

            var dbwMatch = DbwRegex().Match(addr);
            if (dbwMatch.Success)
            {
                int db = int.Parse(dbwMatch.Groups[1].Value);
                int offset = int.Parse(dbwMatch.Groups[2].Value);
                return new ParsedAddress(ip, db, offset, TagDataType.Int16, size: 2);
            }

            var dbdMatch = DbdRegex().Match(addr);
            if (dbdMatch.Success)
            {
                int db = int.Parse(dbdMatch.Groups[1].Value);
                int offset = int.Parse(dbdMatch.Groups[2].Value);
                return new ParsedAddress(ip, db, offset, TagDataType.Int32, size: 4);
            }

            var genericMatch = GenericRegex().Match(addr);
            if (genericMatch.Success)
            {
                int db = int.Parse(genericMatch.Groups[1].Value);
                var t = genericMatch.Groups[2].Value.ToUpperInvariant();
                int offset = int.Parse(genericMatch.Groups[3].Value);
                if (t == "DBB") return new ParsedAddress(ip, db, offset, TagDataType.Byte, size: 1);
                if (t == "DBW") return new ParsedAddress(ip, db, offset, TagDataType.Int16, size: 2);
                if (t == "DBD") return new ParsedAddress(ip, db, offset, TagDataType.Int32, size: 4);
            }

            return null;
        }

        private static object? ExtractValue(byte[] blockBytes, int relOffset, ParsedAddress parsed)
        {
            // relOffset: Offset innerhalb des blockBytes
            if (relOffset < 0 || relOffset >= blockBytes.Length) return null;

            switch (parsed.DataType)
            {
                case TagDataType.Bit:
                    {
                        if (relOffset >= blockBytes.Length) return null;
                        byte b = blockBytes[relOffset];
                        int bit = parsed.Bit ?? 0;
                        return ((b >> (7 - bit)) & 1) == 1; // S7 bit numbering: high bit first
                    }
                case TagDataType.Byte:
                    return blockBytes[relOffset];
                case TagDataType.Int16:
                    {
                        if (relOffset + 1 >= blockBytes.Length) return null;
                        // S7 is Big-Endian for words
                        short val = BinaryPrimitives.ReadInt16BigEndian(blockBytes.AsSpan(relOffset, 2));
                        return val;
                    }
                case TagDataType.Int32:
                    {
                        if (relOffset + 3 >= blockBytes.Length) return null;
                        int val = BinaryPrimitives.ReadInt32BigEndian(blockBytes.AsSpan(relOffset, 4));
                        return val;
                    }
                default:
                    return null;
            }
        }

        internal Dictionary<string, Plc> GetAllPlcs()
        {
            return _plcConfigs.ToDictionary<string, Plc>();
        }

        private Plc? GetOrCreatePlc(string plcName)
        {
            //Console.WriteLine($"GetOrCreatePlc({plcName})");
            return _plcConfigs.GetOrAdd(plcName, key =>
            {
                try
                {
                    // Default: S7-1500 (S71500) Rack 0 Slot 0; passen Sie bei Bedarf an.
                    return new Plc(CpuType.S71500, key, 0, 0);
                }
                catch
                {
                    return new Plc(CpuType.S71500, "0.0.0.0", 0, 0);
                }
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _pollTask.Wait(); } catch { }
            foreach (var plc in _plcConfigs.Values)
            {
                try { plc.Close(); } catch { }
            }
            foreach (var s in _plcLocks.Values) s.Dispose();
            _cts.Dispose();
        }

        private class ClientEntry(Func<JsonTag[], Task> sendCallback, JsonTag[] tags)
        {
            public Func<JsonTag[], Task> SendCallback { get; } = sendCallback;
            public JsonTag[] Tags { get; set; } = tags;
        }

        private readonly struct ParsedAddress(string ip, int db, int offset, PlcTagManager.TagDataType dataType, int size, int? bit = null)
        {
            public string Ip { get; } = ip;
            public int Db { get; } = db;
            public int Offset { get; } = offset;
            public TagDataType DataType { get; } = dataType;
            public int Size { get; } = size;
            public int? Bit { get; } = bit;
        }

        private enum TagDataType
        {
            Bit,
            Byte,
            Int16,
            Int32
        }
    }
}
*/

#endregion