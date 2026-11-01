using ZapretUI.Services;
using Xunit;

namespace ZapretUI.Tests;

public class AppSelfUpdateManifestSourceTests
{
    [Fact]
    public void Default_manifest_is_repo_root_update_json_on_main()
    {
        Assert.Equal(
            "https://raw.githubusercontent.com/RaccoonLaptop/Aeroway/main/update.json",
            AppSelfUpdateService.DefaultManifestUrl);
        Assert.DoesNotContain(
            "packaging/update.json",
            AppSelfUpdateService.DefaultManifestUrl,
            StringComparison.Ordinal);
    }
}
