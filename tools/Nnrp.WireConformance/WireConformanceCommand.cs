namespace Nnrp.WireConformance;

public static class WireConformanceCommand
{
    public static Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Contains("--scenario", StringComparer.Ordinal))
        {
            return WireHostRouteCommand.RunAsync(
                args,
                error,
                cancellationToken);
        }

        if (args.Length != 0 && string.Equals(args[0], "serve-target", StringComparison.Ordinal))
        {
            return WireTargetHostCommand.RunAsync(
                args.AsMemory(1),
                output,
                error,
                cancellationToken);
        }

        return Task.FromResult(WireTargetManifestCommand.Run(args, output, error));
    }
}
