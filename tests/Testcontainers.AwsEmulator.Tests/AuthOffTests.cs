using Amazon.Runtime;
using Amazon.S3;
using Xunit;

namespace Testcontainers.AwsEmulator.Tests;

public sealed class AuthOffTests(EmulatorFixture fixture)
{
    [Fact]
    public async Task WithAuthenticationOff_AnyCredentialsWork()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var emulator = new AwsEmulatorBuilder(fixture.Image).WithAuthentication(false).Build();
        await emulator.StartAsync(ct);

        using var s3 = new AmazonS3Client(new BasicAWSCredentials("anything", "anything"), emulator.Configure(new AmazonS3Config()));
        await s3.PutBucketAsync("open-bucket", ct);

        Assert.Contains((await s3.ListBucketsAsync(ct)).Buckets, b => b.BucketName == "open-bucket");
    }
}
