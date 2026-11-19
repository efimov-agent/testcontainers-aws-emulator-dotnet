using Amazon.Runtime;
using Amazon.S3;
using Xunit;

namespace Testcontainers.AwsEmulator.Tests;

public sealed class BuilderTests
{
    private static AwsEmulatorBuilder NewBuilder() => new("testcontainers-aws-emulator:unit");

    [Theory]
    [InlineData(new[] { false }, true)]
    [InlineData(new[] { false, true }, false)]
    [InlineData(new[] { false, false }, true)]
    [InlineData(new bool[0], false)]
    public async Task AuthOffArgument_ReflectsOnlyLastCall(bool[] calls, bool expectAuthOff)
    {
        var builder = calls.Aggregate(NewBuilder(), (b, enabled) => b.WithAuthentication(enabled));
        // Unrelated calls go through Clone/Merge and must keep the flag.
        await using var container = builder.WithLabel("x", "y").WithEnvironment("A", "B").Build();

        var command = container.Configuration.Command.ToArray();
        Assert.Equal(expectAuthOff ? 1 : 0, command.Count(a => a == "--auth"));
        Assert.Equal(new[] { "-b", "0.0.0.0", "-p", "9297" }, command.Take(4));
    }

    [Fact]
    public async Task TelemetryIsOffByDefault_AndCanBeEnabled()
    {
        await using var off = NewBuilder().Build();
        await using var on = NewBuilder().WithTelemetry(true).Build();

        Assert.Equal("off", off.Configuration.Environments["EMULATOR_TELEMETRY"]);
        Assert.Equal("on", on.Configuration.Environments["EMULATOR_TELEMETRY"]);
    }

    [Fact]
    public async Task AccessorsBeforeStart_ThrowNotStarted()
    {
        await using var container = NewBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => container.GetCredentials());
        Assert.Throws<InvalidOperationException>(() => container.GetCertificate());
    }

    [Fact]
    public async Task CreateClient_WithMismatchedConfig_ThrowsArgumentException()
    {
        await using var container = NewBuilder().Build();

        Assert.Throws<ArgumentException>(() => container.CreateClient<AmazonS3Client>(new Amazon.SQS.AmazonSQSConfig()));
    }

    [Theory]
    [InlineData("LTS/latest", "lts-latest")]
    [InlineData("Preview/1.2.3", "preview-1.2.3")]
    public void TagFor_IsValidDockerTag(string channel, string tag)
    {
        Assert.Equal(tag, AwsEmulatorImage.TagFor(channel));
    }
}
