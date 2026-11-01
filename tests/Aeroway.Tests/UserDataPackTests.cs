using Xunit;
using Aeroway.Services;

namespace Aeroway.Tests;

public class UserDataPackTests
{
    [Fact]
    public void Export_then_import_restores_custom_bat_and_user_list()
    {
        var root = Path.Combine(Path.GetTempPath(), "zapretui-pack-" + Guid.NewGuid().ToString("N"));
        var other = Path.Combine(Path.GetTempPath(), "zapretui-pack-" + Guid.NewGuid().ToString("N"));
        var zip = Path.Combine(Path.GetTempPath(), "zapretui-pack-" + Guid.NewGuid().ToString("N") + ".zip");
        Directory.CreateDirectory(Path.Combine(root, "lists"));
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(root, "service.bat"), "service");
        File.WriteAllText(Path.Combine(root, "my.bat"), "custom");
        File.WriteAllText(Path.Combine(root, "lists", "list-exclude-user.txt"), "example.com");

        try
        {
            UserDataPack.Export(root, zip);
            var count = UserDataPack.Import(other, zip);

            Assert.Equal(2, count);
            Assert.Equal("custom", File.ReadAllText(Path.Combine(other, "my.bat")));
            Assert.Equal("example.com", File.ReadAllText(Path.Combine(other, "lists", "list-exclude-user.txt")));
            Assert.False(File.Exists(Path.Combine(other, "service.bat")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(other)) Directory.Delete(other, true);
            if (File.Exists(zip)) File.Delete(zip);
        }
    }
}
