namespace Testcontainers.AwsEmulator;

/// <summary>Builds an <see cref="AwsEmulatorContainer"/> running the official AWS Emulator.</summary>
/// <remarks>
/// AWS does not publish a prebuilt image. Use <see cref="AwsEmulatorImage.Create"/> to build one from the
/// official Dockerfile, or pass the name of an image you built yourself.
/// </remarks>
public sealed class AwsEmulatorBuilder : ContainerBuilder<AwsEmulatorBuilder, AwsEmulatorContainer, AwsEmulatorConfiguration>
{
    /// <summary>Port the emulator listens on inside the container.</summary>
    public const ushort AwsEmulatorPort = 9297;

    /// <summary>Directory inside the container where the emulator writes its credentials and certificate.</summary>
    public const string StateDirectory = "/home/emulator/.aws/emulator/9297";

    /// <summary>Initializes a new builder for an existing image, e.g. one built by <see cref="AwsEmulatorImage"/>.</summary>
    public AwsEmulatorBuilder(string image)
        : this(new DockerImage(image))
    {
    }

    /// <summary>Initializes a new builder for an image, e.g. the result of <see cref="AwsEmulatorImage.Create"/>.</summary>
    public AwsEmulatorBuilder(IImage image)
        : this(new AwsEmulatorConfiguration())
    {
        DockerResourceConfiguration = Init().WithImage(image).DockerResourceConfiguration;
    }

    private AwsEmulatorBuilder(AwsEmulatorConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        DockerResourceConfiguration = resourceConfiguration;
    }

    /// <inheritdoc />
    protected override AwsEmulatorConfiguration DockerResourceConfiguration { get; }

    /// <summary>Turns SigV4 signature checks on (default) or off (<c>--auth off</c>).</summary>
    /// <remarks>With authentication off any credentials are accepted.</remarks>
    public AwsEmulatorBuilder WithAuthentication(bool enabled)
    {
        var builder = Merge(DockerResourceConfiguration, new AwsEmulatorConfiguration(authentication: enabled));
        return enabled ? builder : builder.WithCommand("--auth", "off");
    }

    /// <summary>Turns the emulator's usage telemetry on or off. The module turns it off by default.</summary>
    public AwsEmulatorBuilder WithTelemetry(bool enabled)
    {
        return WithEnvironment("EMULATOR_TELEMETRY", enabled ? "on" : "off");
    }

    /// <inheritdoc />
    public override AwsEmulatorContainer Build()
    {
        Validate();
        return new AwsEmulatorContainer(DockerResourceConfiguration);
    }

    /// <inheritdoc />
    protected override AwsEmulatorBuilder Init()
    {
        return base.Init()
            .WithEntrypoint("aws-emulator")
            .WithCommand("-b", "0.0.0.0", "-p", AwsEmulatorPort.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .WithEnvironment("EMULATOR_TELEMETRY", "off")
            .WithPortBinding(AwsEmulatorPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("emulator server information")
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort(AwsEmulatorPort)
                    .ForPath("/__admin/health")
                    .UsingTls()
                    .UsingHttpMessageHandler(new HttpClientHandler
                    {
                        // Readiness probe only; the SDK client created by the container pins the emulator's certificate.
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                    })))
            .WithStartupCallback((container, ct) => container.LoadStateAsync(ct));
    }

    /// <inheritdoc />
    protected override AwsEmulatorBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new AwsEmulatorConfiguration(resourceConfiguration));
    }

    /// <inheritdoc />
    protected override AwsEmulatorBuilder Clone(IContainerConfiguration resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new AwsEmulatorConfiguration(resourceConfiguration));
    }

    /// <inheritdoc />
    protected override AwsEmulatorBuilder Merge(AwsEmulatorConfiguration oldValue, AwsEmulatorConfiguration newValue)
    {
        return new AwsEmulatorBuilder(new AwsEmulatorConfiguration(oldValue, newValue));
    }
}
