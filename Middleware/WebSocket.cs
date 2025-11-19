using Gemini.Models;
using Gemini.Services;
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
 
        private async Task ReadTagsLoop(WebSocket webSocket)
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

                            var responseJson = JsonSerializer.Serialize(tagsToSend, AppJsonSerializerContext.Default.JsonTagArray);
                            var responseBytes = Encoding.UTF8.GetBytes(responseJson);

                            //Console.WriteLine("Sending to client " + clientId + ": \r\n" + responseJson);

                            await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
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
    
    }
}
