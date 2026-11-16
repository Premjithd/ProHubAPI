using ServiceProviderAPI.Data;

namespace ServiceProviderAPI.Tests;

public class FieldEncryptionTests
{
    [Fact]
    public void Converter_RoundTrips_WhenKeyConfigured()
    {
        FieldEncryption.Configure(Convert.ToBase64String(new byte[32])); // valid 32-byte key

        var encrypted = (string)FieldEncryption.Converter.ConvertToProvider("1234567890")!;
        Assert.StartsWith("enc:v1:", encrypted);
        Assert.NotEqual("1234567890", encrypted);

        var decrypted = (string)FieldEncryption.Converter.ConvertFromProvider(encrypted)!;
        Assert.Equal("1234567890", decrypted);
    }

    [Fact]
    public void Converter_PassesThroughLegacyPlaintext()
    {
        // A stored value without the version prefix is pre-encryption data — read it as-is.
        var result = (string)FieldEncryption.Converter.ConvertFromProvider("SBIN0001234")!;
        Assert.Equal("SBIN0001234", result);
    }
}
