using System.Text;
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

    [Fact]
    public void Three_strategies_keep_targets_when_the_middle_header_repeats()
    {
        var tracker = new TestRunTracker();
        tracker.BeginRun(PresetTestKind.DpiFreeze);

        tracker.Feed("""
            [1/3] general (ALT).bat
            === [US][Cloudflare] US.CF-01 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.3] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [1/3] general (ALT).bat
            === [DE][Hetzner] DE.HE-01 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=FAIL
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.3] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [2/3] general (ALT2).bat
            === [US][AWS] US.AWS-01 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.3] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=BLOCKED
            [2/3] general (ALT2).bat
            === [SE][Akamai] SE.AKM-01 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.3] code=000 buf_up=0 bytes (0 KB) buf_down=0 bytes (0 KB) time=0.1s status=UNSUPPORTED
            [3/3] general (ALT11).bat
            === [NL][Scaleway] NL.SW-01 ===
            [HTTP] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.2] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            [TLS1.3] code=200 buf_up=1 bytes (0.1 KB) buf_down=1 bytes (0.1 KB) time=0.2s status=OK
            """ + "\n");
        tracker.EndRun(0);

        Assert.True(tracker.TryGetSnapshot("general (ALT).bat", out var first));
        Assert.Equal(2, first.Targets.Count);
        Assert.True(tracker.TryGetSnapshot("general (ALT2).bat", out var middle));
        Assert.Equal(2, middle.Targets.Count);
        Assert.Contains(middle.Targets, row => row.Name.Contains("US.AWS-01", StringComparison.Ordinal));
        Assert.Contains(middle.Targets, row => row.Name.Contains("SE.AKM-01", StringComparison.Ordinal));
        Assert.True(tracker.TryGetSnapshot("general (ALT11).bat", out var last));
        Assert.Single(last.Targets);
    }

    [Fact]
    public void Standard_run_keeps_sites_when_its_header_repeats()
    {
        var tracker = new TestRunTracker();
        tracker.BeginRun(PresetTestKind.Standard);

        tracker.Feed("""
            [1/2] general (ALT).bat
            Discord HTTP:OK TLS1.2:OK TLS1.3:OK | Ping: 12 ms
            [1/2] general (ALT).bat
            YouTube HTTP:OK TLS1.2:FAIL TLS1.3:OK | Ping: 20 ms
            [2/2] general (ALT2).bat
            Discord HTTP:OK TLS1.2:OK TLS1.3:OK | Ping: 11 ms
            YouTube HTTP:OK TLS1.2:OK TLS1.3:OK | Ping: 18 ms
            """ + "\n");
        tracker.EndRun(0);

        Assert.True(tracker.TryGetSnapshot("general (ALT).bat", out var first));
        Assert.Equal(2, first.Targets.Count);
        Assert.Contains(first.Targets, row => row.Name.Equals("Discord", StringComparison.Ordinal));
        Assert.Contains(first.Targets, row => row.Name.Equals("YouTube", StringComparison.Ordinal));
        Assert.True(tracker.TryGetSnapshot("general (ALT2).bat", out var second));
        Assert.Equal(2, second.Targets.Count);
    }

    [Fact]
    public void Split_color_codes_do_not_drop_dpi_targets()
    {
        var raw = new StringBuilder();
        raw.Append("\u001b[33m[1/2] general (ALT).bat\u001b[0m\r\n");
        string[] ids =
        [
            "US.GH-HPRN", "PL.AKM-01", "SE.AKM-01", "CA.CF-01", "CA.CF-02",
            "FR.CNTB-01", "DE.DO-01", "UK.DO-01", "UK.DO-02", "CA.FST-01",
            "LU.GCORE-01", "US.GC-01", "DE.HE-01", "FI.HE-01", "US.MBCOM-01",
            "FR.OVH-01"
        ];
        foreach (var id in ids)
        {
            raw.Append($"\u001b[36m=== [\U0001F1F8\U0001F1EA][Akamai] {id} ===\u001b[0m\r\n");
            raw.Append("\u001b[32m[HTTP] code=000 buf_up=0 bytes (0 KB) buf_down=0 bytes (0 KB) time=3s status=FAIL\u001b[0m\r\n");
            raw.Append("\u001b[31m[TLS1.2] code=000 buf_up=0 bytes (0 KB) buf_down=0 bytes (0 KB) time=3s status=FAIL\u001b[0m\r\n");
            raw.Append("\u001b[31m[TLS1.3] code=000 buf_up=0 bytes (0 KB) buf_down=0 bytes (0 KB) time=3s status=FAIL\u001b[0m\r");
        }

        raw.Append("\n\u001b[33m[2/2] Niko_ALT11.bat\u001b[0m\r\n");
        raw.Append("\u001b[36m=== [\U0001F1F8\U0001F1EA][Akamai] SE.AKM-01 ===\u001b[0m\r");
        raw.Append("\u001b[31m[HTTP] code=000 buf_up=0 bytes (0 KB) buf_down=0 bytes (0 KB) time=3s status=FAIL\u001b[0m\r\n");
        raw.Append("[TLS1.2] code=000 buf_up=0 bytes (0 KB) buf_down=0 bytes (0 KB) time=3s status=FAIL\r\n");
        raw.Append("[TLS1.3] code=000 buf_up=0 bytes (0 KB) buf_down=0 bytes (0 KB) time=3s status=FAIL\r\n");

        var tracker = new TestRunTracker();
        tracker.BeginRun(PresetTestKind.DpiFreeze);
        var text = raw.ToString();
        for (var i = 0; i < text.Length; i += 5)
        {
            var take = Math.Min(5, text.Length - i);
            tracker.Feed(text.Substring(i, take));
        }

        tracker.EndRun(0);

        Assert.True(tracker.TryGetSnapshot("general (ALT).bat", out var first));
        Assert.Equal(ids.Length, first.Targets.Count);
        Assert.Contains(first.Targets, row => row.Name.Contains("SE.AKM-01", StringComparison.Ordinal));
        Assert.Contains(first.Targets, row => row.Name.Contains("CA.CF-01", StringComparison.Ordinal));
        Assert.All(first.Targets, row => Assert.Equal("HTTP:FAIL", row.Http));

        Assert.True(tracker.TryGetSnapshot("Niko_ALT11.bat", out var second));
        Assert.Single(second.Targets);
        Assert.Contains("SE.AKM-01", second.Targets[0].Name, StringComparison.Ordinal);
        Assert.Equal("HTTP:FAIL", second.Targets[0].Http);
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
