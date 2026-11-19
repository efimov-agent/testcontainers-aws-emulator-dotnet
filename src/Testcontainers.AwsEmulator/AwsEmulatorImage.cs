namespace Testcontainers.AwsEmulator;

/// <summary>Builds a local AWS Emulator image from the official Dockerfile (AWS publishes none).</summary>
public static class AwsEmulatorImage
{
    /// <summary>Repository name of the built image.</summary>
    public const string DefaultName = "testcontainers-aws-emulator";

    /// <summary>
    /// Builds (or rebuilds from the Docker layer cache) the emulator image and returns it for
    /// <see cref="AwsEmulatorBuilder"/>. The first build downloads the emulator package; later builds reuse the
    /// cached layer and need no network. The image is kept after the test run.
    /// </summary>
    /// <param name="channel">Release channel path on downloads.aws.com, e.g. <c>LTS/latest</c>. A cached layer
    /// keeps the version it downloaded first; remove the image to pick up a newer release.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<IImage> BuildAsync(string channel = "LTS/latest", CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        // A private, unique build context: no races between parallel test runs, nothing for other users to swap in.
        var context = Directory.CreateTempSubdirectory("testcontainers-aws-emulator-");
        try
        {
            await using (var resource = typeof(AwsEmulatorImage).Assembly.GetManifestResourceStream("Testcontainers.AwsEmulator.Dockerfile")
                ?? throw new InvalidOperationException("Embedded Dockerfile is missing."))
            await using (var file = File.Create(Path.Combine(context.FullName, "Dockerfile")))
            {
                await resource.CopyToAsync(file, ct).ConfigureAwait(false);
            }

            var image = new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(context.FullName)
                .WithDockerfile("Dockerfile")
                .WithName($"{DefaultName}:{TagFor(channel)}")
                .WithBuildArgument("EMULATOR_CHANNEL", channel)
                .WithDeleteIfExists(false)
                .WithCleanUp(false)
                .Build();

            await image.CreateAsync(ct).ConfigureAwait(false);
            return image;
        }
        finally
        {
            context.Delete(recursive: true);
        }
    }

    /// <summary>Docker tag for a channel, e.g. <c>LTS/latest</c> → <c>lts-latest</c>.</summary>
    internal static string TagFor(string channel)
    {
        var chars = channel.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
