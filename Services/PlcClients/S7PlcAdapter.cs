//using S7.Net;
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Gemini.Services.PlcClients
//{
//    internal sealed class S7PlcAdapter : IPlcAdapter, IDisposable
//    {
//        private readonly Plc _plc;
//        private readonly SemaphoreSlim _sync = new(1, 1);
//        public string Key { get; }
//        public string Endpoint => _plc.IP;
//        public bool IsConnected => _plc.IsConnected;

//        public S7PlcAdapter(string key, CpuType cpu, string ip, short rack = 0, short slot = 0)
//        {
//            Key = key ?? throw new ArgumentNullException(nameof(key));
//            _plc = new Plc(cpu, ip, rack, slot);
//        }

//        public async Task ConnectAsync(CancellationToken ct = default)
//        {
//            await _sync.WaitAsync(ct);
//            try
//            {
//                if (!_plc.IsConnected)
//                    await Task.Run(() => _plc.Open(), ct);
//            }
//            finally { _sync.Release(); }
//        }

//        public async Task<byte[]?> ReadBytesAsync(int db, int start, int count, CancellationToken ct = default)
//        {
//            await _sync.WaitAsync(ct);
//            try
//            {
//                return await Task.Run(() => _plc.ReadBytes(DataType.DataBlock, db, start, count), ct);
//            }
//            finally { _sync.Release(); }
//        }

//        public async Task CloseAsync()
//        {
//            await _sync.WaitAsync();
//            try { _plc.Close(); }
//            finally { _sync.Release(); }
//        }

//        public void Dispose()
//        {
//            try { _plc.Close(); } catch { }
//            _sync.Dispose();
//            try { (_plc as IDisposable)?.Dispose(); } catch { }
//        }
//    }
//}