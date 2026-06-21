using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RotationsPlus.Api.Modules.Marketplace;

namespace RotationsPlus.Api.Tests;

/// <summary>
/// Offline coverage for the Azure image store's read-SAS construction and connection-string parsing —
/// the parts DEV won't exercise until real images are migrated in. No Azure calls: building a SAS URL
/// is pure (HMAC over the request fields with the account key), so a well-formed fake key is enough.
/// </summary>
public class AzureBlobProgramImageStoreTests
{
    private static AzureBlobProgramImageStore Store(int ttlMinutes = 60)
    {
        // AccountKey must be valid base64 for the shared-key SAS signature; the value is otherwise irrelevant.
        var key = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake-account-key-fake-account-key"));
        var connectionString = $"DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey={key};EndpointSuffix=core.windows.net";
        var options = Options.Create(new ProgramImageOptions
        {
            ConnectionString = connectionString,
            ContainerName = "program-images",
            SasTtlMinutes = ttlMinutes,
        });
        return new AzureBlobProgramImageStore(options, TimeProvider.System);
    }

    [Fact]
    public void GetReadUrl_returns_null_when_there_is_no_blob()
    {
        var store = Store();
        store.GetReadUrl(null).Should().BeNull();
        store.GetReadUrl("   ").Should().BeNull();
    }

    [Fact]
    public void GetReadUrl_builds_a_signed_read_only_sas_url_for_the_blob()
    {
        var url = Store().GetReadUrl("abc/def.jpg");

        url.Should().NotBeNull();
        url!.Should().StartWith("https://teststorage.blob.core.windows.net/program-images/abc/def.jpg?");
        url.Should().Contain("sig=");  // signature present → the URL is actually signed
        url.Should().Contain("se=");   // expiry present → it is time-limited
        url.Should().Contain("sp=r");  // read-only permission
    }

    [Fact]
    public void Ctor_throws_when_the_connection_string_lacks_an_account_key()
    {
        var options = Options.Create(new ProgramImageOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=only;EndpointSuffix=core.windows.net",
        });

        var act = () => new AzureBlobProgramImageStore(options, TimeProvider.System);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ctor_throws_when_no_connection_string_is_configured()
    {
        var options = Options.Create(new ProgramImageOptions { ConnectionString = null });

        var act = () => new AzureBlobProgramImageStore(options, TimeProvider.System);

        act.Should().Throw<InvalidOperationException>();
    }
}
