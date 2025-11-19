using Gemini.Models;
using Gemini.Services;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

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

        private static async Task ReadTagsLoop(WebSocket webSocket)
        {
            // Empfang der initialen Tags (erwartet: JsonTag[]; N im Format "ip|address")
            var buffer = new byte[1024 * 8];
            var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (receiveResult.MessageType == WebSocketMessageType.Text && receiveResult.Count > 0)
            {
                var jsonString = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);

                if (jsonString?.Trim().Length == 0)
                {
                    // Leere Payload => schließe Verbindung
                    Console.WriteLine("Received empty payload from WebSocket client. Closing connection.");
                    await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Empty payload", CancellationToken.None);
                    return;
                }

                JsonTag[]? clientData = JsonSerializer.Deserialize(jsonString ?? string.Empty, AppJsonSerializerContext.Default.JsonTagArray);
                //Console.WriteLine("WebSocket Client connected with tags: \r\n" + jsonString);

                if (clientData != null && clientData.Length > 0)
                {
                    var clientId = Guid.NewGuid();

                    // Callback zum Senden von Updates an diesen WebSocket
                    Func<Models.JsonTag[], Task> sendCallback = async (tagsToSend) =>
                    {
                        try
                        {
                            if (webSocket.State != WebSocketState.Open)
                            {
                                Console.WriteLine("WebSocket to client " + clientId + " is not open anymore.");
                                return;
                            }

                            // Serialize into pooled buffer using Utf8JsonWriter via an IBufferWriter wrapper over the rented array.
                            var pool = ArrayPool<byte>.Shared;
                            byte[]? rented = null;
                            int bufferSize = 4096;

                            try
                            {
                                while (true)
                                {
                                    rented = pool.Rent(bufferSize);
                                    var bufferWriter = new PooledArrayBufferWriter(rented);

                                    try
                                    {
                                        var writerOptions = new JsonWriterOptions { Indented = false, SkipValidation = false };
                                        using var jsonWriter = new Utf8JsonWriter(bufferWriter, writerOptions);

                                        JsonSerializer.Serialize(jsonWriter, tagsToSend, AppJsonSerializerContext.Default.JsonTagArray);
                                        jsonWriter.Flush();

                                        int bytesUsed = bufferWriter.WrittenCount;
                                        if (bytesUsed == 0)
                                        {
                                            // nothing serialized -> return
                                            break;
                                        }

                                        // Send only the used portion
                                        await webSocket.SendAsync(new ArraySegment<byte>(rented, 0, bytesUsed), WebSocketMessageType.Text, true, CancellationToken.None);
                                        break;
                                    }
                                    catch (ArgumentException)
                                    {
                                        // buffer too small for writer -> retry with larger buffer
                                        // ensure returned to pool and try larger size
                                        pool.Return(rented, clearArray: true);
                                        rented = null;
                                        bufferSize *= 2;
                                        if (bufferSize > 8 * 1024 * 1024) // safety cap 8MB
                                            throw;
                                        continue;
                                    }
                                    finally
                                    {
                                        // nothing here: rented returned in outer finally
                                    }
                                }
                            }
                            finally
                            {
                                if (rented != null)
                                {
                                    try { pool.Return(rented, clearArray: true); } catch { }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Fehler beim Senden => entferne Client
                            Console.WriteLine("Error in Callback sending to WebSocket client " + clientId + ". Removing client.\r\n" + ex);
                            PlcTagManager.Instance.RemoveClient(clientId);
                        }
                    };

                    // Registriere Client und seine Tags beim globalen Manager
                    PlcTagManager.Instance.AddOrUpdateClient(clientId, clientData, sendCallback);

                    // Warte, bis der Socket geschlossen wird. Wenn der Client Nachrichten schickt, ignorieren wir sie hier oder
                    // können sie optional verarbeiten / re-registerieren.
                    try
                    {
                        while (webSocket.State == WebSocketState.Open)
                        {
                            var r = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            if (r.MessageType == WebSocketMessageType.Close)
                            {
                                Console.WriteLine($"Websocket von Client geschlossen.");
                                break;
                            }
                            else
                                Console.WriteLine($"Websocket Status: " + r.MessageType);

                            // Optional: Wenn Client neue Tag-Liste sendet -> re-register
                            if (r.MessageType == WebSocketMessageType.Text && r.Count > 0)
                            {
                                var incoming = Encoding.UTF8.GetString(buffer, 0, r.Count);

                                Console.WriteLine("Received from client " + clientId + ": \r\n" + incoming);

                                try
                                {
                                    var newTags = JsonSerializer.Deserialize(incoming, AppJsonSerializerContext.Default.JsonTagArray);
                                    if (newTags != null)
                                    {
                                        Console.WriteLine("Updating tags for client " + clientId);
                                        PlcTagManager.Instance.AddOrUpdateClient(clientId, newTags, sendCallback);
                                    }
                                }
                                catch
                                {
                                    Console.WriteLine("Invalid tag payload from client " + clientId);
                                    // ignoriere ungültige Payload
                                }
                            }
                        }
                    }
                    finally
                    {
                        // Verbindung beendet -> entferne die Tags dieses Clients global

                        Console.WriteLine("WebSocket client " + clientId + " disconnected.");
                        PlcTagManager.Instance.RemoveClient(clientId);
                        if (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        }
                    }
                }

                Console.WriteLine("WebSocket connection closed or invalid initial message.");
            }
        }

        // Small IBufferWriter<byte> implementation that writes into a pre-rented byte[].
        // Throws ArgumentException when insufficient space is requested so caller can enlarge buffer.
        private sealed class PooledArrayBufferWriter(byte[] buffer) : IBufferWriter<byte>
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