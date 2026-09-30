using Xunit;
using ZapretUI.Controls.Backgrounds;

namespace ZapretUI.Tests;

public class BandwidthScaleTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(9.9, 10)]
    [InlineData(10, 25)]
    [InlineData(50, 100)]
    [InlineData(99.9, 100)]
    [InlineData(100, 250)]
    [InlineData(250, 500)]
    [InlineData(1000, 2500)]
    public void Ceiling_steps_up_when_the_peak_reaches_it(double peakMegabits, double ceiling)
    {
        Assert.Equal(ceiling, BandwidthScale.CeilingMegabits(peakMegabits));
    }

    [Fact]
    public void Format_uses_megabits_until_a_gigabit()
    {
        Assert.Equal("100 Мбит", BandwidthScale.Format(100));
        Assert.Equal("250 Мбит", BandwidthScale.Format(250));
        Assert.Equal("1 Гбит", BandwidthScale.Format(1000));
        Assert.Equal("2.5 Гбит", BandwidthScale.Format(2500));
    }

    [Theory]
    [InlineData(0, "0 Мбит")]
    [InlineData(0.4, "0.4 Мбит")]
    [InlineData(18.6, "18.6 Мбит")]
    [InlineData(50, "50 Мбит")]
    public void FormatLive_shows_the_current_speed(double megabits, string text)
    {
        Assert.Equal(text, BandwidthScale.FormatLive(megabits));
    }
}
