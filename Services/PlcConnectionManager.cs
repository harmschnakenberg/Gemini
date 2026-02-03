// Services\PlcConnectionManager.cs
using S7.Net;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace Gemini.Services
{
    /// <summary>
    /// Verwaltet langlebige/verwendbare Plc-Instanzen.
    /// Methoden:
    /// - GetOrCreate(key, plc): registriert eine Plc-Instanz oder liefert die bereits registrierte zurück.
    /// - EnsureOpen(key): versucht, eine Verbindung zu öffnen, falls sie nicht verbunden ist.
    /// - DisposeAll(): schliesst und disposed alle Verbindungen.
    /// </summary>
    internal sealed class PlcConnectionManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, Plc> _plcs = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public static bool PingHost(string hostUri, int portNumber)
        {
            try
            {
                using var client = new TcpClient(hostUri, portNumber);
                return true;
            }
            catch (SocketException)
            {
#if DEBUG
                //Console.WriteLine("Error pinging host:'" + hostUri + ":" + portNumber.ToString() + "' " + ex);
#endif
                return false;
            }
        }

        public Plc GetOrCreate(string key, Plc plc)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(plc);

            return _plcs.GetOrAdd(key, plc);
        }

        public void UpdatePlc(string key, Plc plc)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(plc);
            _plcs[key] = plc;
        }

        public void EnsureOpen(string key)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (!_plcs.TryGetValue(key, out var plc)) return;

            var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            sem.Wait();
            try
            {
                if (!plc.IsConnected)
                {
                    try
                    {
                        plc.Open();
                    }
                    catch
                    {
                        // swallowing here; caller should log/handle
                        Db.Db.DbLogReadFailure(plc.IP, 0, 0, 0);
                    }
                }
            }
            finally
            {
                sem.Release();
            }
        }

        public void DisposeAll()
        {
            foreach (var kv in _plcs)
            {
                try
                {
                    var plc = kv.Value;
                    try { plc.Close(); } catch { }
                    try { ((IDisposable)plc).Dispose(); } catch { }
                }
                catch { }
            }
            _plcs.Clear();

            foreach (var s in _locks.Values)
            {
                try { s.Dispose(); } catch { }
            }
            _locks.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeAll();
        }
    }
}