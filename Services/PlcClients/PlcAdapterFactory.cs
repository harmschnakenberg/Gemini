//using S7.Net;
//using System;

//namespace Gemini.Services.PlcClients
//{
//    internal static class PlcAdapterFactory
//    {
//        // Creates adapter based on configuration values. key is the PLC name (e.g. "A1").
//        public static IPlcAdapter CreateAdapter(string key, string hostOrEndpoint, string? cpu = null, short rack = 0, short slot = 0)
//        {
//            if (string.IsNullOrWhiteSpace(hostOrEndpoint)) throw new ArgumentNullException(nameof(hostOrEndpoint));

//            if (hostOrEndpoint.StartsWith("opc.tcp", StringComparison.OrdinalIgnoreCase) ||
//                hostOrEndpoint.StartsWith("opc.https", StringComparison.OrdinalIgnoreCase))
//            {
//                return new OpcUaPlcAdapter(key, hostOrEndpoint);
//            }

//            // Fallback: S7
//            var cpuType = cpu switch
//            {
//                "S71200" => CpuType.S71200,
//                "S7400" => CpuType.S7400,
//                "S7300" => CpuType.S7300,
//                _ => CpuType.S71500
//            };

//            return new S7PlcAdapter(key, cpuType, hostOrEndpoint, rack, slot);
//        }
//    }
//}