using Nnrp.NativeBridge;

namespace Nnrp.WireConformance;

public static class WireTargetHostCommand
{
    public static async Task<int> RunAsync(
        ReadOnlyMemory<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            WireTargetHostOptions options = Parse(args.Span);
            if (options.ArtifactRoot is not null)
            {
                Environment.SetEnvironmentVariable(
                    NnrpNativeArtifact.ArtifactRootEnvironmentVariable,
                    options.ArtifactRoot);
            }

            await new WireTargetHost(new NnrpWireTargetSdk()).RunAsync(options, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            error.WriteLine("Wire target was cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            error.WriteLine(exception);
            return 2;
        }
    }

    internal static WireTargetHostOptions Parse(ReadOnlySpan<string> args)
    {
        string? manifestPath = null;
        string? artifactRoot = null;
        string? suitePath = null;
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Expected an option, received: {option}");
            }

            int valueIndex = index + 1;
            if (valueIndex >= args.Length
                || args[valueIndex].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for option: {option}");
            }

            string value = args[valueIndex];
            index = valueIndex;
            switch (option)
            {
                case "--manifest":
                    if (manifestPath is not null)
                    {
                        throw new ArgumentException("Duplicate option: --manifest");
                    }

                    manifestPath = RequirePath(value, option);
                    break;
                case "--artifact-root":
                    if (artifactRoot is not null)
                    {
                        throw new ArgumentException("Duplicate option: --artifact-root");
                    }

                    artifactRoot = RequirePath(value, option);
                    break;
                case "--suite":
                    if (suitePath is not null)
                    {
                        throw new ArgumentException("Duplicate option: --suite");
                    }

                    suitePath = RequirePath(value, option);
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {option}");
            }
        }

        if (manifestPath is null)
        {
            throw new ArgumentException("Missing required option: --manifest");
        }
        if (suitePath is null)
        {
            throw new ArgumentException("Missing required option: --suite");
        }

        return new WireTargetHostOptions(manifestPath, artifactRoot, suitePath);
    }

    private static string RequirePath(string value, string option)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{option} requires a non-empty path.");
        }

        return Path.GetFullPath(value);
    }
}

internal sealed record WireTargetHostOptions(
    string ManifestPath,
    string? ArtifactRoot,
    string SuitePath);
