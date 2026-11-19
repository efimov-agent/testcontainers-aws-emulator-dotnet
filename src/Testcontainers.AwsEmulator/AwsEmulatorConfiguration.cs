namespace Testcontainers.AwsEmulator;

/// <summary>Configuration of an <see cref="AwsEmulatorContainer"/>.</summary>
public sealed class AwsEmulatorConfiguration : ContainerConfiguration
{
    /// <summary>Initializes a new instance of the <see cref="AwsEmulatorConfiguration"/> class.</summary>
    /// <param name="authentication">Whether the emulator requires SigV4-signed requests.</param>
    public AwsEmulatorConfiguration(bool? authentication = null)
    {
        Authentication = authentication;
    }

    /// <inheritdoc cref="AwsEmulatorConfiguration(bool?)"/>
    public AwsEmulatorConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    /// <inheritdoc cref="AwsEmulatorConfiguration(bool?)"/>
    public AwsEmulatorConfiguration(IContainerConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    /// <inheritdoc cref="AwsEmulatorConfiguration(bool?)"/>
    public AwsEmulatorConfiguration(AwsEmulatorConfiguration resourceConfiguration)
        : this(new AwsEmulatorConfiguration(), resourceConfiguration)
    {
    }

    /// <inheritdoc cref="AwsEmulatorConfiguration(bool?)"/>
    public AwsEmulatorConfiguration(AwsEmulatorConfiguration oldValue, AwsEmulatorConfiguration newValue)
        : base(oldValue, newValue)
    {
        Authentication = BuildConfiguration.Combine(oldValue.Authentication, newValue.Authentication);
    }

    /// <summary>Whether the emulator checks SigV4 signatures (default <c>true</c>).</summary>
    public bool? Authentication { get; }
}
