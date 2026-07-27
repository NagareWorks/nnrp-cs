using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Nnrp.NativeBridge
{
    internal static class NnrpNativeTransportDefaults
    {
        private static readonly (string AssemblyName, string TypeName)[] FirstPartyProviders =
        {
            ("Nnrp.Transport.Tcp", "Nnrp.Transport.Tcp.NnrpNativeTcpTransportProvider"),
            ("Nnrp.Transport.Quic", "Nnrp.Transport.Quic.NnrpNativeQuicTransportProvider"),
            ("Nnrp.Transport.Ipc", "Nnrp.Transport.Ipc.NnrpNativeIpcTransportProvider"),
            ("Nnrp.Transport.WebSocket", "Nnrp.Transport.WebSocket.NnrpNativeWebSocketTransportProvider"),
        };

        internal static IReadOnlyList<INnrpNativeTransportProvider> Discover()
        {
            var providers = new List<INnrpNativeTransportProvider>();
            foreach (var entry in FirstPartyProviders)
            {
                Assembly assembly;
                try
                {
                    assembly = Assembly.Load(new AssemblyName(entry.AssemblyName));
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
                var providerType = assembly.GetType(entry.TypeName, throwOnError: false, ignoreCase: false);
                var instance = providerType?
                    .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?
                    .GetValue(null) as INnrpNativeTransportProvider;
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        $"Installed transport package {entry.AssemblyName} does not expose its frozen provider instance.");
                }

                providers.Add(instance);
            }

            return providers.AsReadOnly();
        }
    }
}
