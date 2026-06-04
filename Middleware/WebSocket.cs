using Gemini.Models;
using Gemini.Services;
using Microsoft.AspNetCore.Antiforgery;
using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.IO;

namespace Gemini.Middleware
{
    /// <summary>
    /// Fängt WebSocket abfragen ab und verarbeitet sie.
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <param name="next"></param>
    public class WebSocketMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;
        
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest && context.User?.Identity?.IsAuthenticated == true)
            {
                #region Sicherheitsüberprüfungen

                // Weil die globale Middleware bereits validiert hat, überspringen wir doppelte Validierung.
                if (!context.Items.ContainsKey("AntiforgeryValidatedForWebSocket"))
                {
                    Db.Db.DbLogInfo($"WebSocket Verbindungsversuch ohne AntiForgeryToken wurde nicht zugelassen.");
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                // Robuste Origin-Prüfung: vergleiche Scheme+Host(+Port)
                if (context.Request.Headers.TryGetValue("Origin", out var originHeader))
                {
#if DEBUG
                    Db.Db.DbLogInfo($"WebSocket handshake - Origin: {originHeader}; RemoteIP: {context.Connection.RemoteIpAddress}" + $" | AllowedOrigins: {string.Join(',', ApiSettings.AllowedOrigins)}");
#endif
                
                    if (!Uri.TryCreate(originHeader.ToString(), UriKind.Absolute, out var originUri))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }

                    bool allowed = ApiSettings.AllowedOrigins.Any(allowedOrigin =>
                    {
                        if (!Uri.TryCreate(allowedOrigin, UriKind.Absolute, out var allowedUri))
                            return false;

                        bool schemeMatch = string.Equals(allowedUri.Scheme, originUri.Scheme, StringComparison.OrdinalIgnoreCase);
                        bool hostMatch = string.Equals(allowedUri.Host, originUri.Host, StringComparison.OrdinalIgnoreCase);
                        bool portMatch = (allowedUri.IsDefaultPort && originUri.IsDefaultPort) || (allowedUri.Port == originUri.Port);

                        return schemeMatch && hostMatch && portMatch;
                    });

                    if (!allowed)
                    {
                        string logText = $"WebSocket Verbindungsversuch von {originHeader} wurde nicht zugelassen.";                    
#if DEBUG
                        logText += $" Zulässig sind {string.Join(',', ApiSettings.AllowedOrigins)}";
#endif
                        Db.Db.DbLogInfo(logText);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }
                }

                #endregion

                #region Token zurück schicken an Browser (für Chrome/Edge)

                // Falls der Client einen Subprotocol-String (csrf-...) sendet,
                // muss der Server denselben Subprotocol im Accept-Handshake zurückgeben (erforderlich für Chrome, Edge).
                string? selectedSubProtocol = null;
                if (context.Request.Headers.TryGetValue("Sec-WebSocket-Protocol", out var protoHeader))
                {
                    // Mehrere Protokolle möglich, durch Komma getrennt
                    var items = protoHeader.ToString()
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in items)
                    {
                        if (item.StartsWith("csrf-", StringComparison.OrdinalIgnoreCase))
                        {
                            selectedSubProtocol = item;
                            break;
                        }
                    }
#if DEBUG
                    Db.Db.DbLogInfo($"WebSocket handshake - Sec-WebSocket-Protocol received: {protoHeader}; selected: {selectedSubProtocol}");
#endif
                }

                WebSocket ws;
                if (!string.IsNullOrEmpty(selectedSubProtocol))                
                    ws = await context.WebSockets.AcceptWebSocketAsync(selectedSubProtocol);                
                else                
                    ws = await context.WebSockets.AcceptWebSocketAsync();
                

                #endregion

                await ReadTagsLoop(ws, context.Connection.RemoteIpAddress ?? IPAddress.Loopback);
                return;
            }

            await _next(context);
        }


        /// <summary>
        /// Serialisiert die Tags und sendet sie über den WebSocket an den Client.
        /// Nutzt ArrayPool und eine Puffervergrößerungs-Schleife für optimale Performance.
        /// </summary>
        private static async Task SendWebSocketUpdateAsync(
            WebSocket webSocket,
            Guid clientId,
            JsonTag[] tagsToSend)
        {
            // Die statische Methode geht davon aus, dass die Hilfsklasse PooledArrayBufferWriter 
            // und AppJsonSerializerContext global oder in dieser Klasse verfügbar sind.

            try
            {
                if (webSocket.State != WebSocketState.Open)
                {
#if DEBUG
                    Console.WriteLine("WebSocket to client " + clientId + " is not open anymore.");
#endif
                    return;
                }

                var pool = ArrayPool<byte>.Shared;
                byte[]? rented = null;
                int bufferSize = 4096;

                try
                {
                    // Puffervergrößerungs-Schleife
                    while (true)
                    {
                        rented = pool.Rent(bufferSize);
                        var bufferWriter = new PooledArrayBufferWriter(rented);

                        try
                        {
                            var writerOptions = new JsonWriterOptions { Indented = false, SkipValidation = false };
                            using var jsonWriter = new Utf8JsonWriter(bufferWriter, writerOptions);

                            // Hier wird der JsonSerializerContext verwendet (Native AOT-freundlich)
                            JsonSerializer.Serialize(jsonWriter, tagsToSend, AppJsonSerializerContext.Default.JsonTagArray);
                            jsonWriter.Flush();

                            int bytesUsed = bufferWriter.WrittenCount;
                            if (bytesUsed == 0)
                            {
                                // nothing serialized -> return
                                break;
                            }

                            // Sende nur den verwendeten Teil des Arrays
                            await webSocket.SendAsync(
                                new ArraySegment<byte>(rented, 0, bytesUsed),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);

                            break; // Erfolg, Schleife beenden
                        }
                        catch (ArgumentException) when (bufferSize <= 8 * 1024 * 1024)
                        {
                            // Puffer zu klein für Writer -> Wiederholung mit größerem Puffer
                            pool.Return(rented, clearArray: true);
                            rented = null;
                            bufferSize *= 2;
                            continue; // Nächster Schleifendurchlauf mit größerem Puffer
                        }
                        finally
                        {
                            // Die Ausnahme außerhalb des inneren catch/finally werfen
                        }
                    }
                }
                finally
                {
                    // Wichtig: Gemietetes Array muss IMMER an den Pool zurückgegeben werden
                    if (rented != null)
                    {
                        try { pool.Return(rented, clearArray: true); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fehler beim Senden => Client entfernen (wie im Original-Code)
#if DEBUG
                Console.WriteLine($"Error in sending to WebSocket client {clientId}. Removing client.\\r\\n{ex}");
#endif
                Db.Db.DbLogWarn($"Error in sending to WebSocket client {clientId}. Removing client.\\r\\n{ex}");

                PlcTagManager.Instance.RemoveClient(clientId);
            }
        }



        /// <summary>
        /// Verarbeitet eingehende Nachrichten vom Client, bis der Socket geschlossen wird.
        /// </summary>
        private static async Task ProcessClientMessagesLoop(
            WebSocket webSocket,
            IPAddress ip,
            Guid clientId,
            Func<JsonTag[], Task> sendCallback,
            byte[] buffer)
        {

            const int MaxJsonSize = 1024 * 100; // 100 KB limit
            const int MaxMessagesPerSecond = 100; // Rate-Limiting
            var rateLimiter = new RateLimiter(MaxMessagesPerSecond);

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    // Rate-Limiting prüfen
                    if (!rateLimiter.AllowRequest())
                    {
                        Db.Db.DbLogWarn($"Anfrage-Limit überschritten für WebSocket client {clientId}");
                        await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Rate limit exceeded", CancellationToken.None);
                        break;
                    }

                    WebSocketReceiveResult r;
                    try
                    {
                        r = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    }
                    catch (WebSocketException wsex)
                    {
                        Db.Db.DbLogWarn($"WebSocket receive error for client {clientId}: {wsex.Message}");
                        break;
                    }
                    catch (IOException ioex)
                    {
                        Db.Db.DbLogWarn($"IO error on WebSocket receive for client {clientId}: {ioex.Message}");
                        break;
                    }

                    if (r.MessageType == WebSocketMessageType.Close)
                        break;

                    // Nur Textnachrichten verarbeiten
                    else if (r.MessageType == WebSocketMessageType.Text)
                    {
                        // Falls Nachricht fragmentiert ist, alle Fragmente sammeln
                        using var ms = new MemoryStream();
                        ms.Write(buffer, 0, r.Count);

                        if (!r.EndOfMessage)
                        {
                            // weitere Fragmente lesen
                            while (!r.EndOfMessage)
                            {
                                try
                                {
                                    r = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                                }
                                catch (WebSocketException wsex)
                                {
                                    Db.Db.DbLogWarn($"WebSocket receive error for client {clientId} while assembling fragments: {wsex.Message}");
                                    ms.Dispose();
                                    goto ContinueLoop;
                                }
                                catch (IOException ioex)
                                {
                                    Db.Db.DbLogWarn($"IO error on WebSocket receive for client {clientId} while assembling fragments: {ioex.Message}");
                                    ms.Dispose();
                                    goto ContinueLoop;
                                }

                                if (r.MessageType == WebSocketMessageType.Close)
                                {
                                    ms.Dispose();
                                    goto ContinueLoop;
                                }

                                ms.Write(buffer, 0, r.Count);

                                if (ms.Length > MaxJsonSize)
                                {
                                    Db.Db.DbLogWarn($"JSON Datei zu groß von Client {clientId}: {ms.Length} bytes");
                                    await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Gesendete Datei zu groß", CancellationToken.None);
                                    ms.Dispose();
                                    break;
                                }
                            }
                        }

                        // Verarbeite die vollständige Nachricht
                        try
                        {
                            var incoming = Encoding.UTF8.GetString(ms.ToArray(), 0, (int)ms.Length);

                            try
                            {
                                // Verwende den AOT-freundlichen JsonSerializerContext
                                var newTags = JsonSerializer.Deserialize(incoming, AppJsonSerializerContext.Default.JsonTagArray);

                                if (newTags is not null && newTags.Length <= 500) // Array-Größenlimit
                                {
#if DEBUG
                                    Console.WriteLine($"Updating tags for client {clientId} on {ip}.");
#endif
                                    // Registrierung mit dem GLEICHEN Callback und der NEUEN Tag-Liste
                                    PlcTagManager.Instance.AddOrUpdateClient(clientId, ip, newTags, sendCallback);
                                }
                                else
                                {
                                    Db.Db.DbLogWarn($"Ungültige Tag-Anzahl von Client {clientId}: {newTags?.Length ?? 0}");
                                }
                            }
                            catch (JsonException ex)
                            {
                                Db.Db.DbLogWarn($"Ungültiges JSON von Client {clientId} {ip}: {ex.Message}");
                                // Nicht schließen, aber Nachricht ignorieren
                            }
                            catch
                            {
#if DEBUG
                                Console.WriteLine($"Invalid tag payload from client {clientId} on {ip}.");
                                // Ignoriere ungültige Payload
#endif
                            }
                        }
                        finally
                        {
                            // ms disposed by using
                        }
                    }

                ContinueLoop:
                    continue;
                }
            }
            // Der 'finally' des ursprünglichen Blocks wird in ReadTagsLoop beibehalten, 
            // um die Ressourcenfreigabe zu garantieren, auch wenn dieser Loop abstürzt.
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine($"Error processing client messages for {clientId}. Forcing disconnect.\\r\\n {ex.GetType().Name}: {ex.Message}");
#endif
                Db.Db.DbLogWarn($"Error processing client messages for {clientId}. Forcing disconnect.\\r\\n {ex.GetType().Name}: {ex.Message}");
                // WICHTIG: Wenn der Loop abbricht, muss die Verbindung entfernt werden.
                PlcTagManager.Instance.RemoveClient(clientId);
                throw; // Wir werfen die Ausnahme weiter, damit sie im outer finally (ReadTagsLoop) behandelt wird.
            }
        }

        private static async Task ReadTagsLoop(WebSocket webSocket, IPAddress ip)
        {
            const int MaxInitialPayloadSize = 1024 * 100; // 100 KB
            // Empfang der initialen Tags (Code bleibt unverändert)
            var buffer = new byte[1024 * 8];

            // Empfange initiale Nachricht robust (fragmente und Fehler behandeln)
            using var ms = new MemoryStream();
            WebSocketReceiveResult receiveResult;
            try
            {
                do
                {
                    receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        // Client wollte schließen
                        return;
                    }

                    ms.Write(buffer, 0, receiveResult.Count);

                    if (ms.Length > MaxInitialPayloadSize)
                    {
                        Db.Db.DbLogWarn($"WebSocket Payload zu groß von {ip}: {ms.Length} bytes");
                        await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Gesendete Datei zu groß", CancellationToken.None);
                        return;
                    }
                }
                while (!receiveResult.EndOfMessage);
            }
            catch (WebSocketException wsex)
            {
                Db.Db.DbLogWarn($"WebSocket exception beim initialen Empfang von {ip}: {wsex.Message}");
                return;
            }
            catch (IOException ioex)
            {
                Db.Db.DbLogWarn($"IO exception beim initialen Empfang von {ip}: {ioex.Message}");
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var jsonBytes = ms.ToArray();
            var jsonString = Encoding.UTF8.GetString(jsonBytes, 0, jsonBytes.Length);

            if (jsonString?.Trim().Length == 0)
            {
#if DEBUG
                Console.WriteLine("Received empty payload from WebSocket client. Closing connection.");
#endif
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Empty payload", CancellationToken.None);
                return;
            }

#if DEBUG
            Console.WriteLine("Initialnachricht: " + jsonString);
#endif
            JsonTag[]? clientData;
            try
            {
                clientData = JsonSerializer.Deserialize(jsonString ?? string.Empty, AppJsonSerializerContext.Default.JsonTagArray);
            }
            catch (JsonException jex)
            {
                Db.Db.DbLogWarn($"Ungültiges JSON in initialer Nachricht von {ip}: {jex.Message}");
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid initial message", CancellationToken.None);
                return;
            }

            if (clientData is null || clientData?.Length == 0)
            {
#if DEBUG
                Console.WriteLine("WebSocket connection closed or invalid initial message.");
#endif
                return;
            }
            var clientId = Guid.NewGuid();

            // Callback sendet geänderte Werte an den Client
            async Task SendWebsocketCallback(Models.JsonTag[] tagsToSend)
            {
                await SendWebSocketUpdateAsync(webSocket, clientId, tagsToSend);
            }

            if (clientData is not null)
                // Registriere Client und seine Tags beim globalen Manager
                PlcTagManager.Instance.AddOrUpdateClient(clientId, ip, clientData, SendWebsocketCallback);

            // 2. Starte den Haupt-Loop zum Warten auf eingehende Nachrichten
            try
            {
                // Rufe die ausgelagerte Methode auf, die den Socket aktiv hält.
                await ProcessClientMessagesLoop(webSocket, ip, clientId, SendWebsocketCallback, buffer);
            }
            // Der 'finally' Block fängt sowohl den normalen Loop-Exit (durch break) 
            // als auch eine Ausnahme (durch throw im Loop) ab.
            finally
            {
                // Verbindung beendet -> entferne die Tags dieses Clients global
#if DEBUG
                Console.WriteLine($"WebSocket client {clientId} disconnected.");
                Db.Db.DbLogInfo($"WebSocket von Client {PlcTagManager.Instance.ClientInfo.GetValueOrDefault(clientId)} beendet.");
#endif
                PlcTagManager.Instance.RemoveClient(clientId);

                // Schließe den Socket sauber
                if (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
        }


        // Hilfeklasse für Rate-Limiting: Erlaubt nur eine bestimmte Anzahl von Anfragen pro Sekunde pro Client, um Missbrauch zu verhindern.
        internal class RateLimiter(int maxRequestsPerSecond)
        {
            private readonly int _maxRequestsPerSecond = maxRequestsPerSecond;
            private DateTime _windowStart = DateTime.UtcNow;
            private int _requestCount = 0;
            private readonly Lock _lock = new();

            public bool AllowRequest()
            {
                lock (_lock)
                {
                    var now = DateTime.UtcNow;
                    var elapsed = (now - _windowStart).TotalSeconds;

                    if (elapsed >= 1.0)
                    {
                        _windowStart = now;
                        _requestCount = 0;
                    }

                    if (_requestCount >= _maxRequestsPerSecond)
                        return false;

                    _requestCount++;
                    return true;
                }
            }
        }

        // Small IBufferWriter<byte> implementation that writes into a pre-rented byte[].
        // Throws ArgumentException when insufficient space is requested so caller can enlarge buffer.
        internal sealed class PooledArrayBufferWriter(byte[] buffer) : IBufferWriter<byte>
        {
            private readonly byte[] _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            private int _position = 0;

            public void Advance(int count)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(count);
                checked
                {
                    _position += count;
                }
                if (_position > _buffer.Length) throw new ArgumentException("Advanced past the end of the buffer.");
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                int available = _buffer.Length - _position;
                if (sizeHint > available)
                    throw new ArgumentException("Buffer too small for requested memory.");
                return new Memory<byte>(_buffer, _position, available);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                return GetMemory(sizeHint).Span;
            }

            public int WrittenCount => _position;
        }
    }
}