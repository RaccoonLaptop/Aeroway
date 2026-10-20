using Xunit;
using ZapretUI.Helpers;
using ZapretUI.Models;

namespace ZapretUI.Tests;

public class DpiResultTests
{
    [Fact]
    public void Dpi_headers_keep_each_host_when_provider_is_shared()
    {
        Assert.True(TestRunParser.TryParseDpiTargetHeader(
            "=== [US][Cloudflare] US.CF-01 ===", out var first));
        Assert.True(TestRunParser.TryParseDpiTargetHeader(
            "=== [US][Cloudflare] US.CF-02 ===", out var second));

        Assert.NotEqual(first, second);
        Assert.Contains("US.CF-01", first, StringComparison.Ordinal);
        Assert.Contains("US.CF-02", second, StringComparison.Ordinal);
    }

    [Fact]
    public void Dpi_run_keeps_every_target_line_in_the_result()
    {
        var tracker = new TestRunTracker();
        tracker.BeginRun(PresetTestKind.DpiFreeze);

        tracker.Feed("""
            [1/2] general (ALT).bat
            === [US][Cloudflare] US.CF-01 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=FAIL
            [TLS1.3] code=200 buf_up=1 bytes (0.1 KB) buf_down=0 bytes (0 KB) time=5s status=LIKELY_BLOCKED
            === [US][Cloudflare] US.CF-02 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.3] code=000 buf_up=0 bytes (0 KB) buf_down=0 bytes (0 KB) time=0.1s status=UNSUPPORTED
            general (ALT).bat : OK:   3, FAIL:   1, UNSUP:   1, BLOCKED:   1
            [2/2] general (ALT2).bat
            """ + "\n");

        Assert.True(tracker.TryGetSnapshot("general (ALT).bat", out var snapshot));
        Assert.Equal(2, snapshot.Targets.Count);
        Assert.Contains(snapshot.Targets, row => row.Name.Contains("US.CF-01", StringComparison.Ordinal));
        Assert.Contains(snapshot.Targets, row => row.Name.Contains("US.CF-02", StringComparison.Ordinal));

        var score = Assert.Single(tracker.Scores, row => row.FileName.Equals("general (ALT).bat", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("OK: 3, FAIL: 1, BLOCKED: 1, UNSUP: 1", score.Detail);
        Assert.DoesNotContain("US.CF-01", score.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Dpi_score_is_a_summary_without_target_rows()
    {
        var score = PresetScoreCalculator.FromDpiTargets("general (ALT).bat",
        [
            Row("US.CF-01", "HTTP:OK", "TLS1.2:FAIL", "TLS1.3:BLOCKED"),
            Row("US.CF-02", "HTTP:OK", "TLS1.2:OK", "TLS1.3:UNSUP")
        ]);

        Assert.Equal("OK: 3, FAIL: 1, BLOCKED: 1, UNSUP: 1", score.Detail);
        Assert.DoesNotContain('\n', score.Detail);
    }

    [Fact]
    public void Repeated_config_header_does_not_drop_the_rest_of_the_targets()
    {
        var tracker = new TestRunTracker();
        tracker.BeginRun(PresetTestKind.DpiFreeze);

        tracker.Feed("""
            [1/2] general (ALT).bat
            === [US][Cloudflare] US.CF-01 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.3] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [1/2] general (ALT).bat
            === [US][Cloudflare] US.CF-02 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=FAIL
            [TLS1.3] code=000 buf_up=0 bytes (0 KB) buf_down=0 bytes (0 KB) time=0.1s status=UNSUPPORTED
            [2/2] Niko_ALT11.bat
            === [DE][Hetzner] DE.HE-01 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.3] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            """ + "\n");
        tracker.EndRun(0);

        Assert.True(tracker.TryGetSnapshot("general (ALT).bat", out var first));
        Assert.Equal(2, first.Targets.Count);
        Assert.Contains(first.Targets, row => row.Name.Contains("US.CF-01", StringComparison.Ordinal));
        Assert.Contains(first.Targets, row => row.Name.Contains("US.CF-02", StringComparison.Ordinal));

        Assert.True(tracker.TryGetSnapshot("Niko_ALT11.bat", out var second));
        Assert.Single(second.Targets);
    }

    private static TestTargetRow Row(string name, string http, string tls12, string tls13) =>
        new()
        {
            Name = name,
            Http = http,
            Tls12 = tls12,
            Tls13 = tls13,
            Ping = "n/a"
        };
}
