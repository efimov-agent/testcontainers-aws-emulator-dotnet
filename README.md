# Testcontainers.AwsEmulator

[Testcontainers for .NET](https://dotnet.testcontainers.org/) module for the official
[AWS Emulator](https://github.com/aws/aws-emulator) (released by AWS in November 2026).

Running the emulator in Docker by hand means: building the image yourself (AWS publishes none), binding to
`0.0.0.0`, turning telemetry off, then copying the per-run credentials and the self-signed certificate out of the
container and wiring them into the SDK. This module does all of it.

Community module; not affiliated with AWS or the Testcontainers project.

```sh
dotnet add package AwsEmulator.Testcontainers
```

The package ID is `AwsEmulator.Testcontainers` (the `Testcontainers.*` prefix on nuget.org is reserved for official
modules); the namespace is `Testcontainers.AwsEmulator`, following the Testcontainers module convention.

```csharp
var image = await AwsEmulatorImage.BuildAsync();   // official Dockerfile; daemon's x86_64 or aarch64

await using var emulator = new AwsEmulatorBuilder(image).Build();
await emulator.StartAsync();

using var s3 = emulator.CreateClient<AmazonS3Client>(new AmazonS3Config());
await s3.PutBucketAsync("my-bucket");
```

What you get from a started `AwsEmulatorContainer`:

| Member | Purpose |
|---|---|
| `CreateClient<TClient>(config)` | Any `AmazonServiceClient`, pointed at the emulator with its credentials |
| `Configure(config)` | Sets endpoint, signing region `us-east-1`, pinned TLS, no host-prefix injection, S3 path-style |
| `GetCredentials()` | SigV4 keys the emulator generated for this run |
| `GetCertificate()` / `CreateHttpClientFactory()` | The run's self-signed certificate; an SDK HTTP factory that trusts exactly it (pinned, works with remote Docker hosts) |
| `GetEndpoint()` | `https://<docker-host>:<mapped-port>/` |

Builder options:

- `WithAuthentication(false)` — `--auth off`, any credentials accepted.
- `WithTelemetry(true)` — telemetry is **off** by default in this module (`EMULATOR_TELEMETRY=off`).
- `new AwsEmulatorBuilder("my-registry/aws-emulator:1.0")` — use an image you built and pushed yourself
  (it must contain the `aws-emulator` binary; the module sets the entrypoint and arguments).

## Tests

`dotnet test` runs the suite against a real emulator (Docker required): S3, SQS, DynamoDB, rejected wrong
credentials, TLS pinning (including refusing another emulator's certificate), telemetry, auth off, builder
flags. The first run downloads the emulator package (~570 MB image); later runs rebuild from the Docker layer
cache in seconds, offline. The cached layer keeps the emulator version it first downloaded — remove the
`testcontainers-aws-emulator` image to update.

## Notes

- AWS Emulator is for development and testing only; see its
  [security notes](https://docs.aws.amazon.com/emulator/latest/userguide/security.html).
- Fidelity varies per service; check the [release notes](https://github.com/aws/aws-emulator/releases).

## License

MIT
