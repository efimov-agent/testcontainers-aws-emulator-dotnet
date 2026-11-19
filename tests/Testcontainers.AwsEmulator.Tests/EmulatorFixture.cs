using Testcontainers.AwsEmulator;
using Xunit;

[assembly: AssemblyFixture(typeof(Testcontainers.AwsEmulator.Tests.EmulatorFixture))]

namespace Testcontainers.AwsEmulator.Tests;

/// <summary>Builds the emulator image once and shares one running emulator across the test assembly.</summary>
public sealed class EmulatorFixture : IAsyncLifetime
{
    public DotNet.Testcontainers.Images.IImage Image { get; private set; } = null!;

    public AwsEmulatorContainer Container { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Image = await AwsEmulatorImage.BuildAsync();
        Container = new AwsEmulatorBuilder(Image).Build();
        await Container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
