using Amazon.Runtime;
using Amazon.S3;
using Xunit;

namespace Testcontainers.AwsEmulator.Tests;

public sealed class SecondContainerTests(EmulatorFixture fixture)
{
    [Fact]
    public async Task WithAuthenticationOff_AnyCredentialsWork_AndUserStartupCallbackKeepsState()
    {
        var ct = TestContext.Current.CancellationToken;
        var callbackRan = false;
        await using var emulator = new AwsEmulatorBuilder(fixture.Image)
            .WithAuthentication(false)
            .WithStartupCallback((_, _) => { callbackRan = true; return Task.CompletedTask; })
            .Build();
        await emulator.StartAsync(ct);

        using var s3 = new AmazonS3Client(new BasicAWSCredentials("anything", "anything"), emulator.Configure(new AmazonS3Config()));
        await s3.PutBucketAsync("open-bucket", ct);

        Assert.True(callbackRan);
        Assert.NotNull(emulator.GetCertificate());
        Assert.Contains((await s3.ListBucketsAsync(ct)).Buckets, b => b.BucketName == "open-bucket");

        // Pinning: a client pinned to the shared emulator's certificate must refuse this emulator's certificate.
        var foreign = new AmazonS3Config { ServiceURL = emulator.GetEndpoint(), ForcePathStyle = true, AuthenticationRegion = "us-east-1" };
        foreign.HttpClientFactory = fixture.Container.CreateHttpClientFactory();
        using var pinnedElsewhere = new AmazonS3Client(new BasicAWSCredentials("anything", "anything"), foreign);
        await Assert.ThrowsAnyAsync<Exception>(() => pinnedElsewhere.ListBucketsAsync(ct));
    }
}
