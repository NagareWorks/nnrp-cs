using System;
using System.Globalization;
using System.Threading;
using Nnrp.Client;
using Nnrp.Core;

namespace Nnrp.Quic.Net8Sample
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (!SampleOptions.TryParse(args, out var options, out var error))
            {
                Console.Error.WriteLine(error);
                SampleOptions.WriteUsage(Console.Error);
                return 1;
            }

            if (options.ShowHelp)
            {
                SampleOptions.WriteUsage(Console.Out);
                return 0;
            }

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));

                await using var transport = await SystemNetQuicMessageTransport.ConnectAsync(
                    options.Host,
                    options.Port,
                    options.TlsServerName,
                    options.AcceptAnyServerCertificate,
                    timeout.Token);

                var profile = new ClientProfile
                {
                    TransportProfile = NnrpTransportProfile.Quic,
                };
                var client = new NnrpClient(profile, transport);

                var connectResult = await client.ConnectAsync(
                    requestedSessionId: options.RequestedSessionId,
                    traceId: options.TraceId,
                    cancellationToken: timeout.Token);
                if (!connectResult.IsConnected)
                {
                    Console.Error.WriteLine($"CONNECT failed: {connectResult.Failure}");
                    return 2;
                }

                double? pingMilliseconds = null;
                if (!options.SkipPing)
                {
                    var pingElapsed = await client.PingAsync(traceId: options.TraceId + 1, cancellationToken: timeout.Token);
                    pingMilliseconds = pingElapsed.TotalMilliseconds;
                }

                var submitMessage = SmokePackets.CreateSmokeFrameSubmitMessage(
                    sessionId: client.NegotiatedSessionId,
                    frameId: options.FrameId,
                    viewId: 0,
                    traceId: options.TraceId + 2);
                var result = await client.SubmitAsync(submitMessage, timeout.Token);
                await client.CloseAsync(reason: "sample_close", traceId: options.TraceId + 3, cancellationToken: timeout.Token);

                Console.WriteLine(
                    $"CONNECTED session_id={client.NegotiatedSessionId} requested_model={options.RequestedModel} transport_profile={profile.TransportProfile}");
                if (pingMilliseconds.HasValue)
                {
                    Console.WriteLine($"PING pong_rtt_ms={pingMilliseconds.Value:F2}");
                }

                Console.WriteLine(
                    $"RESULT frame_id={result.Header.FrameId} tile_count={result.Metadata.TileCount} result_status={result.Metadata.StatusCode}");
                return 0;
            }
            catch (PlatformNotSupportedException ex)
            {
                Console.Error.WriteLine($"QUIC sample is not supported on this platform: {ex.Message}");
                return 3;
            }
        }

        private sealed class SampleOptions
        {
            private SampleOptions()
            {
            }

            public string Host { get; private set; } = "127.0.0.1";

            public int Port { get; private set; } = 50072;

            public string TlsServerName { get; private set; } = "localhost";

            public string RequestedModel { get; private set; } = "engine-sr";

            public uint RequestedSessionId { get; private set; } = 41;

            public uint FrameId { get; private set; } = 303;

            public ulong TraceId { get; private set; } = 1;

            public int TimeoutSeconds { get; private set; } = 30;

            public bool AcceptAnyServerCertificate { get; private set; } = true;

            public bool SkipPing { get; private set; }

            public bool ShowHelp { get; private set; }

            public static bool TryParse(string[] args, out SampleOptions options, out string error)
            {
                options = new SampleOptions();
                error = string.Empty;

                for (var index = 0; index < args.Length; index++)
                {
                    var argument = args[index];
                    switch (argument)
                    {
                        case "--help":
                        case "-h":
                            options.ShowHelp = true;
                            return true;
                        case "--host":
                            if (!TryReadValue(args, ref index, out var host, out error))
                            {
                                return false;
                            }

                            options.Host = host;
                            break;
                        case "--port":
                            if (!TryReadValue(args, ref index, out var portValue, out error)
                                || !int.TryParse(portValue, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
                                || port <= 0
                                || port > 65535)
                            {
                                error = "--port must be an integer between 1 and 65535.";
                                return false;
                            }

                            options.Port = port;
                            break;
                        case "--tls-server-name":
                            if (!TryReadValue(args, ref index, out var tlsServerName, out error))
                            {
                                return false;
                            }

                            options.TlsServerName = tlsServerName;
                            break;
                        case "--requested-model":
                            if (!TryReadValue(args, ref index, out var requestedModel, out error))
                            {
                                return false;
                            }

                            options.RequestedModel = requestedModel;
                            break;
                        case "--requested-session-id":
                            if (!TryReadValue(args, ref index, out var requestedSessionIdValue, out error)
                                || !uint.TryParse(requestedSessionIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedSessionId))
                            {
                                error = "--requested-session-id must be a non-negative integer.";
                                return false;
                            }

                            options.RequestedSessionId = requestedSessionId;
                            break;
                        case "--frame-id":
                            if (!TryReadValue(args, ref index, out var frameIdValue, out error)
                                || !uint.TryParse(frameIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var frameId))
                            {
                                error = "--frame-id must be a non-negative integer.";
                                return false;
                            }

                            options.FrameId = frameId;
                            break;
                        case "--trace-id":
                            if (!TryReadValue(args, ref index, out var traceIdValue, out error)
                                || !ulong.TryParse(traceIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var traceId))
                            {
                                error = "--trace-id must be a non-negative integer.";
                                return false;
                            }

                            options.TraceId = traceId;
                            break;
                        case "--timeout-seconds":
                            if (!TryReadValue(args, ref index, out var timeoutValue, out error)
                                || !int.TryParse(timeoutValue, NumberStyles.None, CultureInfo.InvariantCulture, out var timeoutSeconds)
                                || timeoutSeconds <= 0)
                            {
                                error = "--timeout-seconds must be a positive integer.";
                                return false;
                            }

                            options.TimeoutSeconds = timeoutSeconds;
                            break;
                        case "--verify-certificate":
                            options.AcceptAnyServerCertificate = false;
                            break;
                        case "--skip-ping":
                            options.SkipPing = true;
                            break;
                        default:
                            error = $"Unknown argument: {argument}";
                            return false;
                    }
                }

                return true;
            }

            public static void WriteUsage(TextWriter writer)
            {
                writer.WriteLine("Usage: dotnet run --project samples/Nnrp.Quic.Net8Sample -- [options]");
                writer.WriteLine("Options:");
                writer.WriteLine("  --host <value>                  Runtime host. Default: 127.0.0.1");
                writer.WriteLine("  --port <value>                  Runtime QUIC port. Default: 50072");
                writer.WriteLine("  --tls-server-name <value>       TLS server name. Default: localhost");
                writer.WriteLine("  --requested-model <value>       Requested model string. Default: engine-sr");
                writer.WriteLine("  --requested-session-id <value>  Requested session id. Default: 41");
                writer.WriteLine("  --frame-id <value>              Smoke frame id. Default: 303");
                writer.WriteLine("  --trace-id <value>              Base trace id. Default: 1");
                writer.WriteLine("  --timeout-seconds <value>       End-to-end timeout. Default: 30");
                writer.WriteLine("  --verify-certificate            Require certificate validation instead of accepting any cert.");
                writer.WriteLine("  --skip-ping                     Skip the sample ping/pong round-trip.");
                writer.WriteLine("  --help, -h                      Show this help message.");
            }

            private static bool TryReadValue(string[] args, ref int index, out string value, out string error)
            {
                if (index + 1 >= args.Length)
                {
                    value = string.Empty;
                    error = $"Missing value for {args[index]}.";
                    return false;
                }

                index++;
                value = args[index];
                error = string.Empty;
                return true;
            }
        }
    }
}