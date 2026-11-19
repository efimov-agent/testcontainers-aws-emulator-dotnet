using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Xunit;

namespace Testcontainers.AwsEmulator.Tests;

public sealed class ServiceTests(EmulatorFixture fixture)
{
    private readonly AwsEmulatorContainer _emulator = fixture.Container;

    [Fact]
    public async Task S3_PutThenGet_ReturnsSameContent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var s3 = _emulator.CreateClient<AmazonS3Client>(new AmazonS3Config());
        var bucket = "bucket-" + Guid.NewGuid().ToString("N")[..12];

        await s3.PutBucketAsync(bucket, ct);
        await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = "a/b.txt", ContentBody = "hello emulator" }, ct);
        using var response = await s3.GetObjectAsync(bucket, "a/b.txt", ct);
        using var reader = new StreamReader(response.ResponseStream);

        Assert.Equal("hello emulator", await reader.ReadToEndAsync(ct));
    }

    [Fact]
    public async Task Sqs_SendThenReceive_ReturnsMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sqs = _emulator.CreateClient<AmazonSQSClient>(new AmazonSQSConfig());

        var queue = await sqs.CreateQueueAsync("q-" + Guid.NewGuid().ToString("N")[..12], ct);
        await sqs.SendMessageAsync(queue.QueueUrl, "ping", ct);
        var received = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest { QueueUrl = queue.QueueUrl, WaitTimeSeconds = 2 }, ct);

        Assert.Equal("ping", Assert.Single(received.Messages).Body);
    }

    [Fact]
    public async Task DynamoDb_PutThenGet_ReturnsItem()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ddb = _emulator.CreateClient<AmazonDynamoDBClient>(new AmazonDynamoDBConfig());
        var table = "t-" + Guid.NewGuid().ToString("N")[..12];

        await ddb.CreateTableAsync(new CreateTableRequest
        {
            TableName = table,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST,
        }, ct);
        while ((await ddb.DescribeTableAsync(table, ct)).Table.TableStatus != TableStatus.ACTIVE)
        {
            await Task.Delay(100, ct); // CreateTable is asynchronous, as on AWS.
        }

        await ddb.PutItemAsync(table, new Dictionary<string, AttributeValue> { ["pk"] = new("k1"), ["v"] = new("42") }, ct);
        var item = await ddb.GetItemAsync(table, new Dictionary<string, AttributeValue> { ["pk"] = new("k1") }, ct);

        Assert.Equal("42", item.Item["v"].S);
    }

    [Fact]
    public async Task WrongCredentials_AreRejected_SoGeneratedOnesAreReallyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = _emulator.Configure(new AmazonS3Config());
        using var s3 = new AmazonS3Client(new BasicAWSCredentials("AKIAWRONGWRONGWRONG0", "wrong-secret"), config);

        var ex = await Assert.ThrowsAnyAsync<AmazonServiceException>(() => s3.ListBucketsAsync(ct));

        Assert.True(ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, $"status {ex.StatusCode}");
    }

    [Fact]
    public async Task UnpinnedClient_FailsTls_PinnedFactoryIsWhatMakesItWork()
    {
        var ct = TestContext.Current.CancellationToken;
        using var plain = new HttpClient();

        await Assert.ThrowsAsync<HttpRequestException>(() => plain.GetAsync(new Uri(new Uri(_emulator.GetEndpoint()), "/__admin/health"), ct));
    }

    [Fact]
    public async Task Telemetry_IsOffByDefault()
    {
        var (stdout, stderr) = await _emulator.GetLogsAsync(ct: TestContext.Current.CancellationToken);

        Assert.Contains("telemetry: disabled", stdout + stderr, StringComparison.Ordinal);
    }
}
