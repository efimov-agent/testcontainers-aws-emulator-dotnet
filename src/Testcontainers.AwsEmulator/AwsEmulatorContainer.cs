using System.Net.Security;
using System.Text;
using Amazon.Runtime;

namespace Testcontainers.AwsEmulator;

/// <summary>A running AWS Emulator container.</summary>
public sealed class AwsEmulatorContainer : DockerContainer
{
    /// <summary>Region the emulator reports by default.</summary>
    public const string DefaultRegion = "us-east-1";

    private readonly AwsEmulatorConfiguration _configuration;
    private X509Certificate2? _certificate;
    private ImmutableCredentials? _credentials;

    /// <summary>Initializes a new instance of the <see cref="AwsEmulatorContainer"/> class.</summary>
    public AwsEmulatorContainer(AwsEmulatorConfiguration configuration)
        : base(configuration)
    {
        _configuration = configuration;
    }

    /// <summary>HTTPS endpoint of the emulator as seen from the test host.</summary>
    public string GetEndpoint()
    {
        return new UriBuilder(Uri.UriSchemeHttps, Hostname, GetMappedPublicPort(AwsEmulatorBuilder.AwsEmulatorPort)).Uri.ToString();
    }

    /// <summary>The self-signed certificate this emulator run generated.</summary>
    public X509Certificate2 GetCertificate()
    {
        return _certificate ?? throw NotStarted();
    }

    /// <summary>
    /// SigV4 credentials generated for this emulator run. With authentication off, placeholder keys are returned
    /// (the emulator accepts any).
    /// </summary>
    public AWSCredentials GetCredentials()
    {
        var credentials = _credentials ?? throw NotStarted();
        return new BasicAWSCredentials(credentials.AccessKey, credentials.SecretKey);
    }

    /// <summary>
    /// An SDK HTTP client factory that trusts exactly this emulator's certificate (pinned by thumbprint), so it
    /// works with any Docker host name and without installing the certificate.
    /// </summary>
    public HttpClientFactory CreateHttpClientFactory()
    {
        return new PinnedCertificateHttpClientFactory(GetCertificate());
    }

    /// <summary>
    /// Points an SDK client configuration at the emulator: endpoint, region, pinned TLS, no host prefix injection
    /// and, for Amazon S3, path-style addressing.
    /// </summary>
    public TConfig Configure<TConfig>(TConfig config)
        where TConfig : ClientConfig
    {
        ArgumentNullException.ThrowIfNull(config);

        config.ServiceURL = GetEndpoint();
        config.AuthenticationRegion = DefaultRegion;
        config.DisableHostPrefixInjection = true;
        config.HttpClientFactory = CreateHttpClientFactory();

        // AmazonS3Config.ForcePathStyle: virtual-hosted buckets would resolve <bucket>.<docker-host>.
        config.GetType().GetProperty("ForcePathStyle")?.SetValue(config, true);
        return config;
    }

    /// <summary>
    /// Creates an SDK client, e.g. <c>container.CreateClient&lt;AmazonS3Client&gt;(new AmazonS3Config())</c>.
    /// </summary>
    public TClient CreateClient<TClient>(ClientConfig config)
        where TClient : AmazonServiceClient
    {
        Configure(config);
        return (TClient)(Activator.CreateInstance(typeof(TClient), GetCredentials(), config)
            ?? throw new InvalidOperationException($"Cannot create {typeof(TClient)}."));
    }

    /// <summary>Reads the credentials and certificate that the emulator wrote on startup.</summary>
    internal async Task LoadStateAsync(CancellationToken ct)
    {
        // The startup callback runs before the wait strategy, so wait for the emulator to write its state files.
        var pem = await ReadStateFileAsync("ca.pem", ct).ConfigureAwait(false);
        _certificate = X509Certificate2.CreateFromPem(Encoding.ASCII.GetString(pem));

        if (_configuration.Authentication == false)
        {
            _credentials = new ImmutableCredentials("emulator", "emulator", null);
            return;
        }

        var file = await ReadStateFileAsync("emulator_credentials", ct).ConfigureAwait(false);
        _credentials = ParseCredentials(Encoding.UTF8.GetString(file));
    }

    private async Task<byte[]> ReadStateFileAsync(string name, CancellationToken ct)
    {
        var path = $"{AwsEmulatorBuilder.StateDirectory}/{name}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));

        while (true)
        {
            try
            {
                var content = await ReadFileAsync(path, timeout.Token).ConfigureAwait(false);
                if (content.Length > 0)
                {
                    return content;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException && !timeout.IsCancellationRequested)
            {
                // Not written yet.
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"AWS Emulator did not write {path} within 1 minute.");
            }
        }
    }

    /// <summary>Parses the <c>[default]</c> profile of an AWS shared credentials file.</summary>
    internal static ImmutableCredentials ParseCredentials(string content)
    {
        string? accessKey = null, secretKey = null, token = null;
        var inDefault = false;

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }

            if (line[0] == '[')
            {
                inDefault = line == "[default]";
                continue;
            }

            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (!inDefault || eq < 0)
            {
                continue;
            }

            var value = line[(eq + 1)..].Trim();
            switch (line[..eq].Trim())
            {
                case "aws_access_key_id": accessKey = value; break;
                case "aws_secret_access_key": secretKey = value; break;
                case "aws_session_token": token = value; break;
            }
        }

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException("The emulator credentials file has no [default] access key.");
        }

        return new ImmutableCredentials(accessKey, secretKey, token);
    }

    private static InvalidOperationException NotStarted() =>
        new("The AWS Emulator container has not been started.");

    private sealed class PinnedCertificateHttpClientFactory(X509Certificate2 certificate) : HttpClientFactory
    {
        private readonly byte[] _rawData = certificate.RawData;

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        {
            var handler = new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (_, cert, _, _) => cert is not null && cert.GetRawCertData().AsSpan().SequenceEqual(_rawData),
                },
            };
            return new HttpClient(handler) { Timeout = clientConfig.Timeout ?? TimeSpan.FromSeconds(100) };
        }

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;

        public override string? GetConfigUniqueString(IClientConfig clientConfig) => null;
    }
}
