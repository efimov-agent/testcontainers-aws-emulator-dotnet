using Xunit;

namespace Testcontainers.AwsEmulator.Tests;

public sealed class CredentialsParserTests
{
    [Fact]
    public void ParsesDefaultProfile_IgnoringOtherProfilesAndComments()
    {
        const string file = "# generated\n[other]\naws_access_key_id = NOPE\n[default]\naws_access_key_id = AKIA123\r\naws_secret_access_key=secret/xyz=\n";

        var creds = AwsEmulatorContainer.ParseCredentials(file);

        Assert.Equal("AKIA123", creds.AccessKey);
        Assert.Equal("secret/xyz=", creds.SecretKey);
    }

    [Fact]
    public void Throws_WhenDefaultProfileMissing()
    {
        Assert.Throws<InvalidOperationException>(() => AwsEmulatorContainer.ParseCredentials("[other]\naws_access_key_id=x\n"));
    }
}
