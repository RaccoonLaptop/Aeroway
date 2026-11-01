using Xunit;
using Aeroway.Services;

namespace Aeroway.Tests;

public class AppVersionCompareTests
{
    [Theory]
    [InlineData("1.10.31", "1.10.30", true)]
    [InlineData("1.10.30", "1.10.31", false)]
    [InlineData("1.10.31", "1.10.31", false)]
    [InlineData("1.2", "1.1.9", true)]
    public void IsNewer_compares_release_numbers(string remote, string local, bool newer)
    {
        Assert.Equal(newer, AppVersionCompare.IsNewer(remote, local));
    }
}
