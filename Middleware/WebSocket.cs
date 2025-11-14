using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Gemini.Middleware
{
    public class WebSocketMiddleware(RequestDelegate next)
    {
        //private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/ws")
            {
                // Prüfen, ob es sich um eine WebSocket-Anfrage handelt
                if (context.WebSockets.IsWebSocketRequest)
                {
                    // WebSocket akzeptieren
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                    // Pufferspeicher für eingehende Nachricht
                    var buffer = new byte[1024 * 4];
                    var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    // Prüfen, ob eine Nachricht empfangen wurde und es sich um Text handelt
                    if (receiveResult.MessageType == WebSocketMessageType.Text && receiveResult.Count > 0)
                    {
                        // Eingehendes JSON-Objekt deserialisieren (Native AOT-freundlich)
                        var jsonString = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);

                        // Verwende die von der Source-Generation bereitgestellte TypeInfo
                        // Da wir nun 'JsonSerializer.Deserialize<ClientData>(...)' verwenden können, ist dies sauberer.
                        var clientData = JsonSerializer.Deserialize(jsonString, AppJsonSerializerContext.Default.ClientData);

                        if (clientData != null)
                        {
                            // Hauptschleife: Aktualisierungen senden, solange der Socket offen ist
                            while (webSocket.State == WebSocketState.Open)
                            {
                                // Objekt aktualisieren
                                clientData.Counter++;
                                clientData.ServerTime = DateTimeOffset.UtcNow;

                                // Aktualisiertes Objekt serialisieren (Native AOT-freundlich)
                                var responseJson = JsonSerializer.Serialize(clientData, AppJsonSerializerContext.Default.ClientData);
                                var responseBytes = Encoding.UTF8.GetBytes(responseJson);

                                // Nachricht senden
                                await webSocket.SendAsync(
                                    new ArraySegment<byte>(responseBytes, 0, responseBytes.Length),
                                    WebSocketMessageType.Text,
                                    endOfMessage: true,
                                    CancellationToken.None);

                                // 1 Sekunde warten
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
            else
            {
                // Wenn der Pfad nicht /ws ist, führe die nächste Middleware aus
                await next(context);
            }
        }
    }
}
