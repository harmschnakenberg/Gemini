using Gemini.Models;
using Gemini.Services;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Gemini.Middleware
{
    public class WebSocketMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
#if DEBUG
                    //Console.WriteLine("WebSocket wird geöffnet.");
#endif
                    await ReadTagsLoop(webSocket);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
            else
            {
                await _next(context);
            }
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
                    Console.WriteLine("WebSocket to client " + clientId + " is not open anymore.");
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
                Console.WriteLine($"Error in sending to WebSocket client {clientId}. Removing client.\r\n{ex}");
                PlcTagManager.Instance.RemoveClient(clientId);
            }
        }




        /// <summary>
        /// Verarbeitet eingehende Nachrichten vom Client, bis der Socket geschlossen wird.
        /// </summary>
        private static async Task ProcessClientMessagesLoop(
            WebSocket webSocket,
            Guid clientId,
            Func<JsonTag[], Task> sendCallback,
            byte[] buffer)
        {
            // Die 'try' des ursprünglichen Blocks beginnt hier.
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var r = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (r.MessageType == WebSocketMessageType.Close)
                    {
#if DEBUG
                        Console.WriteLine($"Websocket von Client {clientId} geschlossen.");
#endif
                        break;
                    }
                    // Optional: Wenn Client neue Tag-Liste sendet -> re-register
                    else if (r.MessageType == WebSocketMessageType.Text && r.Count > 0)
                    {
                        var incoming = Encoding.UTF8.GetString(buffer, 0, r.Count);

                        // Console.WriteLine($"Received from client {clientId}: \r\n" + incoming);

                        try
                        {
                            // Verwende den AOT-freundlichen JsonSerializerContext
                            var newTags = JsonSerializer.Deserialize(incoming, AppJsonSerializerContext.Default.JsonTagArray);

                            if (newTags != null)
                            {
                                Console.WriteLine($"Updating tags for client {clientId}.");
                                // Registrierung mit dem GLEICHEN Callback und der NEUEN Tag-Liste
                                PlcTagManager.Instance.AddOrUpdateClient(clientId, newTags, sendCallback);
                            }
                        }
                        catch
                        {
                            Console.WriteLine($"Invalid tag payload from client {clientId}.");
                            // Ignoriere ungültige Payload
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Websocket Status: " + r.MessageType);
                    }
                }
            }
            // Der 'finally' des ursprünglichen Blocks wird in ReadTagsLoop beibehalten, 
            // um die Ressourcenfreigabe zu garantieren, auch wenn dieser Loop abstürzt.
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing client messages for {clientId}. Forcing disconnect.\r\n{ex}");
                // WICHTIG: Wenn der Loop abbricht, muss die Verbindung entfernt werden.
                PlcTagManager.Instance.RemoveClient(clientId);
                throw; // Wir werfen die Ausnahme weiter, damit sie im outer finally (ReadTagsLoop) behandelt wird.
            }
        }

        private static async Task ReadTagsLoop(WebSocket webSocket)
        {
            // Empfang der initialen Tags (Code bleibt unverändert)
            var buffer = new byte[1024 * 8];
            // 1. Erster Empfang zur Ermittlung der Tags
            var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (receiveResult.MessageType != WebSocketMessageType.Text || receiveResult.Count <= 0)
            {
                Console.WriteLine("Received invalid initial message from WebSocket client. Closing connection.");
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid initial message", CancellationToken.None);
                return;
            }

            var jsonString = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);

            if (jsonString?.Trim().Length == 0)
            {
                Console.WriteLine("Received empty payload from WebSocket client. Closing connection.");
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Empty payload", CancellationToken.None);
                return;
            }

            //Console.WriteLine("Initialnachricht: " + jsonString);
            JsonTag[]? clientData = JsonSerializer.Deserialize(jsonString ?? string.Empty, AppJsonSerializerContext.Default.JsonTagArray);

            if (clientData is null || clientData?.Length == 0)
            {
                Console.WriteLine("WebSocket connection closed or invalid initial message.");
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
            PlcTagManager.Instance.AddOrUpdateClient(clientId, clientData, SendWebsocketCallback);

            // 2. Starte den Haupt-Loop zum Warten auf eingehende Nachrichten
            try
            {
                // Rufe die ausgelagerte Methode auf, die den Socket aktiv hält.
                await ProcessClientMessagesLoop(webSocket, clientId, SendWebsocketCallback, buffer);
            }
            // Der 'finally' Block fängt sowohl den normalen Loop-Exit (durch break) 
            // als auch eine Ausnahme (durch throw im Loop) ab.
            finally
            {
                // Verbindung beendet -> entferne die Tags dieses Clients global
#if DEBUG
                //Console.WriteLine($"WebSocket client {clientId} disconnected.");
#endif
                PlcTagManager.Instance.RemoveClient(clientId);

                // Schließe den Socket sauber
                if (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
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