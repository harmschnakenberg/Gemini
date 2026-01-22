//using Opc.Ua;
//using Opc.Ua.Client;
//using Opc.Ua.Configuration;
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Gemini.Services.PlcClients
//{
//    // Minimaler OPC UA Adapter (Basis: anonymous session). Produktion: security, certs, credentials anpassen.
//    internal sealed class OpcUaPlcAdapter : IPlcAdapter, IDisposable
//    {
//        private readonly string _endpoint;
//        private Session? _session;
//        private readonly object _sync = new();
//        public string Key { get; }
//        public string Endpoint => _endpoint;
//        public bool IsConnected => _session != null && _session.Connected;

//        public OpcUaPlcAdapter(string key, string endpoint)
//        {
//            Key = key ?? throw new ArgumentNullException(nameof(key));
//            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
//        }

//        public async Task ConnectAsync(CancellationToken ct = default)
//        {
//            lock (_sync)
//            {
//                if (_session != null && _session.Connected) return;
//            }

//            var config = new ApplicationConfiguration()
//            {
//                ApplicationName = "GeminiOpcClient",
//                ApplicationType = ApplicationType.Client,
//                SecurityConfiguration = new SecurityConfiguration { ApplicationCertificate = new CertificateIdentifier() }
//            };

//            await config.ValidateAsync(ApplicationType.Client, ct);
//            ITelemetryContext telemetry = null!;

//            var selectedEndpoint = await CoreClientUtils.SelectEndpointAsync(config, _endpoint, false, telemetry, ct);
//            var endpointConfig = EndpointConfiguration.Create(config);
//            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

//            var userIdentity = new UserIdentity(new AnonymousIdentityToken());

//            var session = await Session.Create(config, endpoint, false, Key, 60000, userIdentity, null);
//            //var session = await ISessionFactory.CreateSessionAsync(config, endpoint, false, Key, 60000, userIdentity, null, ct);
//            lock (_sync) { _session = session; }
//        }

//        public Task<byte[]?> ReadBytesAsync(int db, int start, int count, CancellationToken ct = default)
//        {
//            // OPC UA arbeitet mit NodeIds; dieser Adapter unterstützt kein S7-Block-Read ohne Mapping.
//            throw new NotSupportedException("ReadBytesAsync is not supported for OPC UA adapter. Use node-based reads via specific mapping.");
//        }

//        public Task CloseAsync()
//        {
//            lock (_sync)
//            {
//                try { _session?.CloseAsync(); _session?.Dispose(); _session = null; } catch { }
//            }
//            return Task.CompletedTask;
//        }

//        public void Dispose()
//        {
//            try { _session?.Dispose(); } catch { }
//        }
//    }
//}