using Gemini.Models;
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
                        JsonTag[] clientData = JsonSerializer.Deserialize(jsonString, AppJsonSerializerContext.Default.JsonTagArray);

                        int counter = 100;
                        if (clientData != null)
                        {
                            // Hauptschleife: Aktualisierungen senden, solange der Socket offen ist
                            while (webSocket.State == WebSocketState.Open)
                            {
                                

                                // Objekt aktualisieren                        
                                JsonTag[] serverData = GetServerData(ref clientData, --counter < 0);

                                //Console.Write($"{counter}|{serverData?.Length} ");

                                if (serverData?.Length > 0)
                                {
                                    // Aktualisiertes Objekt serialisieren (Native AOT-freundlich)
                                    var responseJson = JsonSerializer.Serialize(serverData, AppJsonSerializerContext.Default.JsonTagArray);
                                    var responseBytes = Encoding.UTF8.GetBytes(responseJson);

                                    // Nachricht senden
                                    await webSocket.SendAsync(
                                        new ArraySegment<byte>(responseBytes, 0, responseBytes.Length),
                                        WebSocketMessageType.Text,
                                        endOfMessage: true,
                                        CancellationToken.None);
                                }

                                #region Reset Tag Refresh                                
                                if (counter < 0)
                                    counter = 100;
                                #endregion

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

        /// <summary>
        /// Retrieves the latest server-side tag data corresponding to the specified client tags.
        /// </summary>
        /// <remarks>If <paramref name="refresh"/> is <see langword="true"/>, the method updates each
        /// tag's value before returning the data. The returned data reflects the current state of the server-side tags
        /// at the time of the call.</remarks>
        /// <param name="cD">An array of client tag objects for which to retrieve updated server data. The array must not be null.</param>
        /// <param name="refresh">A value indicating whether to refresh the server-side tag values before retrieval. If <see
        /// langword="true"/>, each tag's value is refreshed prior to being returned.</param>
        /// <returns>An array of <see cref="JsonTag"/> objects containing the server-side data for each tag specified in
        /// <paramref name="cD"/>. The array will contain one element for each input tag.</returns>
        private JsonTag[] GetServerData(ref JsonTag[] cD, bool refresh)
        {
            List<JsonTag> serverData = [];
            for (int i = 0; i < cD.Length; i++)
            {
                if (TagCollection.Tags.TryAdd(cD[i].N, new Tag(cD[i].N)))
                    Console.WriteLine("Tag in Abfrageliste hinzugefügt: " + cD[i].N);

                var oVal = (double)(cD[i].V ?? 0.0);
                var nVal = (double)(TagCollection.Tags[cD[i].N].Value ?? 0.0) + DateTime.Now.Second;

                if (Math.Abs(nVal - oVal) > 0.05)
                {
                    cD[i].V = nVal;
                    serverData.Add(new JsonTag(cD[i].N, nVal, DateTime.Now));
                    Console.WriteLine($"{cD[i].N} = {nVal} ({oVal})");
                }
                //else
                //    Console.WriteLine("keine Wertänderung: " + cD[i].N);

                if (refresh)
                    TagCollection.Tags[cD[i].N].Refresh();


            }


            //foreach (JsonTag tag in cD)
            //{
            //    if(TagCollection.Tags.TryAdd(tag.N, new Tag(tag.N)))
            //        Console.WriteLine("Tag in Abfrageliste hinzugefügt: " + tag.N);

            //    var oVal = (double)(tag.V ?? 0.0);
            //    var nVal = (double)(TagCollection.Tags[tag.N].Value ?? 0.0);

            //     if (Math.Abs(nVal - oVal) > 0.05) {               
            //        tag.V = nVal;
            //        serverData.Add(new JsonTag(tag.N, nVal, DateTime.Now));
            //        Console.WriteLine($"{tag.N} = {nVal} ({oVal})");
            //    }

            //    if (refresh)
            //        TagCollection.Tags[tag.N].Refresh();
            //}
            return [.. serverData];
        }
    }
}
