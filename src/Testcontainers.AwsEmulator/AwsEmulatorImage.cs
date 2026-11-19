using System.Runtime.InteropServices;

namespace Testcontainers.AwsEmulator;

/// <summary>Builds a local AWS Emulator image from the official Dockerfile (AWS publishes none).</summary>
public static class AwsEmulatorImage
{
    /// <summary>Default local tag for the built image.</summary>
    public const string DefaultName = "testcontainers-aws-emulator";

    /// <summary>
    /// Creates a future image from the embedded official Dockerfile. Pass it to <see cref="AwsEmulatorBuilder"/>
    /// after <c>await image.CreateAsync()</c>. The image is kept between runs, so later builds hit the Docker cache.
    /// </summary>
    /// <param name="channel">Release channel path on downloads.aws.com, e.g. <c>LTS/latest</c>.</param>
    /// <param name="tag">Image tag.</param>
    public static IFutureDockerImage Create(string channel = "LTS/latest", string tag = "lts")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "aarch64" : "x86_64";

        return new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(WriteDockerfile())
            .WithDockerfile("Dockerfile")
            .WithName($"{DefaultName}:{tag}-{arch}")
            .WithBuildArgument("EMULATOR_ARCH", arch)
            .WithBuildArgument("EMULATOR_CHANNEL", channel)
            .WithDeleteIfExists(false)
            .WithCleanUp(false)
            .Build();
    }

    private static string WriteDockerfile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testcontainers-aws-emulator");
        Directory.CreateDirectory(directory);

        using var resource = typeof(AwsEmulatorImage).Assembly.GetManifestResourceStream("Testcontainers.AwsEmulator.Dockerfile")
            ?? throw new InvalidOperationException("Embedded Dockerfile is missing.");
        using var reader = new StreamReader(resource);
        File.WriteAllText(Path.Combine(directory, "Dockerfile"), reader.ReadToEnd());
        return directory;
    }
}
