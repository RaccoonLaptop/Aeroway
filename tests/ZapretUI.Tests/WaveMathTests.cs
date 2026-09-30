using Xunit;
using ZapretUI.Controls.Backgrounds;

namespace ZapretUI.Tests;

public class WaveMathTests
{
    [Theory]
    [InlineData(0, 7.2, 1.5)]
    [InlineData(100, 93.6, 43.312)]
    [InlineData(1000, 93.6, 43.312)]
    public void Crest_and_line_at_flow(double flow, double crest, double line)
    {
        Assert.Equal(crest, WaveMath.Crest(flow, 0, animate: true), 3);
        Assert.Equal(line, WaveMath.Line(flow, 0, animate: true), 3);
    }

    [Fact]
    public void Reduced_motion_skips_the_sine_pump()
    {
        Assert.Equal(130, WaveMath.Crest(1, 0, animate: false), 3);
        Assert.Equal(4 + 130 * 0.42, WaveMath.Line(1, 0, animate: false), 3);
        Assert.Equal(1.5, WaveMath.Line(0, 0, animate: false), 3);
    }
}
