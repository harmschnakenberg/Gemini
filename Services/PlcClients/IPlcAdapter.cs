//using System.Threading;
//using System.Threading.Tasks;

//namespace Gemini.Services.PlcClients
//{
//    internal interface IPlcAdapter
//    {
//        // Eindeutiger Key (z.B. PLC-Name wie "A1")
//        string Key { get; }

//        // Endpoint / Host / URI (z.B. "192.168.0.10" oder "opc.tcp://10.0.0.5:4840")
//        string Endpoint { get; }

//        // Aktueller Verbindungsstatus (soweit verfügbar)
//        bool IsConnected { get; }

//        // Verbindung öffnen / sicherstellen
//        Task ConnectAsync(CancellationToken ct = default);

//        // Block-Read für S7-ähnliche Geräte; für Adapter, die das nicht unterstützen, kann NotSupportedException geworfen werden.
//        Task<byte[]?> ReadBytesAsync(int db, int start, int count, CancellationToken ct = default);

//        // Schließen / Aufräumen
//        Task CloseAsync();
//    }
//}