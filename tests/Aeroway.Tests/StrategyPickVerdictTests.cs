using Aeroway.Helpers;
using Aeroway.Models;
using Aeroway.Services;
using Xunit;

namespace Aeroway.Tests;

public class StrategyPickVerdictTests
{
    [Fact]
    public void Open_site_that_was_closed_is_unblocked_by_bypass()
    {
        var assessment = StrategyPickVerdict.Assess(
            [
                Site("DiscordMain", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:SSL"),
                Site("YouTubeWeb", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")
            ],
            [
                Site("DiscordMain", "HTTP:OK", "TLS1.2:OK", "TLS1.3:ERROR"),
                Site("YouTubeWeb", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")
            ],
            "✓");

        Assert.Equal(PickVerdict.Works, assessment.Verdict);
        Assert.Equal(PickReason.UnblockedByBypass, assessment.Reason);
        Assert.Equal("✓", assessment.Glyph);
        Assert.Equal(1, assessment.UnblockedCount);
        Assert.Equal(1, assessment.AlreadyOpenCount);
        Assert.Equal(TargetDeltaKind.Unblocked, assessment.DeltaFor("Discord Main"));
        Assert.Equal(TargetDeltaKind.AlreadyOpen, assessment.DeltaFor("YouTubeWeb"));
    }

    [Fact]
    public void Sites_that_were_already_open_are_not_credited_to_bypass()
    {
        var assessment = StrategyPickVerdict.Assess(
            [Site("DiscordMain", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")],
            [Site("DiscordMain", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")],
            "✓");

        Assert.Equal(PickVerdict.Works, assessment.Verdict);
        Assert.Equal(PickReason.AlreadyOpen, assessment.Reason);
        Assert.Equal(0, assessment.UnblockedCount);
        Assert.Equal(1, assessment.AlreadyOpenCount);
    }

    [Fact]
    public void Mixed_open_and_closed_targets_are_partial()
    {
        var assessment = StrategyPickVerdict.Assess(
            [
                Site("DiscordMain", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:ERROR"),
                Site("YouTubeWeb", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:ERROR")
            ],
            [
                Site("DiscordMain", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK"),
                Site("YouTubeWeb", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:ERROR")
            ],
            "✗");

        Assert.Equal(PickVerdict.Partial, assessment.Verdict);
        Assert.Equal(PickReason.Mixed, assessment.Reason);
        Assert.Equal("≈", assessment.Glyph);
        Assert.Equal(TargetDeltaKind.StillClosed, assessment.DeltaFor("YouTubeWeb"));
    }

    [Fact]
    public void Nothing_opened_is_a_failure_even_with_a_clean_score_glyph()
    {
        var assessment = StrategyPickVerdict.Assess(
            [Site("DiscordMain", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:ERROR")],
            [Site("DiscordMain", "HTTP:SSL", "TLS1.2:ERROR", "TLS1.3:ERROR")],
            "✓");

        Assert.Equal(PickVerdict.Failed, assessment.Verdict);
        Assert.Equal(PickReason.NoImprovement, assessment.Reason);
        Assert.Equal("✗", assessment.Glyph);
    }

    [Fact]
    public void Regression_without_a_new_open_site_fails()
    {
        var assessment = StrategyPickVerdict.Assess(
            [Site("DiscordMain", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")],
            [Site("DiscordMain", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:ERROR")],
            "✗");

        Assert.Equal(PickVerdict.Failed, assessment.Verdict);
        Assert.Equal(1, assessment.RegressedCount);
        Assert.Equal(TargetDeltaKind.Regressed, assessment.DeltaFor("DiscordMain"));
    }

    [Fact]
    public void Without_baseline_the_glyph_does_not_claim_a_bypass()
    {
        var assessment = StrategyPickVerdict.Assess(
            null,
            [Site("DiscordMain", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")],
            "✓");

        Assert.Equal(PickVerdict.Works, assessment.Verdict);
        Assert.Equal(PickReason.ScoreOnly, assessment.Reason);
        Assert.False(assessment.UsedBaseline);
        Assert.Equal(0, assessment.UnblockedCount);
    }

    [Fact]
    public void Ping_only_rows_do_not_change_the_verdict()
    {
        var assessment = StrategyPickVerdict.Assess(
            [
                Site("DiscordMain", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:ERROR"),
                Ping("CloudflareDNS1111", "Timeout")
            ],
            [
                Site("DiscordMain", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:ERROR"),
                Ping("CloudflareDNS1111", "12 ms")
            ],
            "≈");

        Assert.Equal(PickVerdict.Failed, assessment.Verdict);
        Assert.Equal(0, assessment.UnblockedCount);
    }

    [Fact]
    public void Queue_moves_pending_to_running_to_done_and_keeps_the_best_unblocked()
    {
        var board = new StrategyPickBoard();
        board.Seed(
        [
            new StrategyItem { FileName = "general.bat", DisplayName = "general" },
            new StrategyItem { FileName = "alt.bat", DisplayName = "alt" }
        ]);
        board.SetBaseline(
        [
            Site("DiscordMain", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:ERROR"),
            Site("YouTubeWeb", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")
        ]);

        board.SyncLive("general.bat", [Site("DiscordMain", "HTTP:…", "TLS1.2:…", "TLS1.3:…")], []);
        Assert.Equal(PickQueueState.Running, board.Find("general")!.State);
        Assert.Equal(PickQueueState.Pending, board.Find("alt.bat")!.State);

        board.SyncLive(
            "alt.bat",
            [Site("YouTubeWeb", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")],
            [
                new StrategyPickCompletion
                {
                    FileName = "general.bat",
                    Targets =
                    [
                        Site("DiscordMain", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK"),
                        Site("YouTubeWeb", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")
                    ],
                    ScoreGlyph = "✓",
                    RankScore = 10
                },
                new StrategyPickCompletion
                {
                    FileName = "alt.bat",
                    Targets = [Site("YouTubeWeb", "HTTP:OK", "TLS1.2:OK", "TLS1.3:OK")],
                    ScoreGlyph = "✓",
                    RankScore = 90
                }
            ]);

        Assert.Equal(PickQueueState.Done, board.Find("general.bat")!.State);
        Assert.Equal(PickReason.UnblockedByBypass, board.Find("general.bat")!.Assessment!.Reason);
        Assert.Equal(PickReason.AlreadyOpen, board.Find("alt.bat")!.Assessment!.Reason);
        Assert.Equal("general.bat", board.Best()!.FileName);

        board.Phase = PickPhase.Finished;
        Assert.True(board.TrySelect("alt.bat"));
        Assert.False(board.TrySelect("missing.bat"));
        board.Phase = PickPhase.Testing;
        Assert.False(board.TrySelect("general.bat"));
    }

    [Fact]
    public void Stopping_leaves_unstarted_candidates_pending()
    {
        var board = new StrategyPickBoard();
        board.Seed(
        [
            new StrategyItem { FileName = "general.bat", DisplayName = "general" },
            new StrategyItem { FileName = "alt.bat", DisplayName = "alt" }
        ]);
        board.SetBaseline([Site("DiscordMain", "HTTP:ERROR", "TLS1.2:ERROR", "TLS1.3:ERROR")]);
        board.SyncLive(
            "general.bat",
            [Site("DiscordMain", "HTTP:OK", "TLS1.2:…", "TLS1.3:…")],
            []);

        board.Finish(stopped: true);

        Assert.Equal(PickPhase.Stopped, board.Phase);
        Assert.Equal(PickQueueState.Done, board.Find("general.bat")!.State);
        Assert.Equal(PickReason.UnblockedByBypass, board.Find("general.bat")!.Assessment!.Reason);
        Assert.Equal(PickQueueState.Pending, board.Find("alt.bat")!.State);
    }

    [Fact]
    public void A_finished_run_marks_strategies_the_script_skipped()
    {
        var board = new StrategyPickBoard();
        board.Seed([new StrategyItem { FileName = "general.bat", DisplayName = "general" }]);
        board.Finish(stopped: false);

        var only = Assert.Single(board.Candidates);
        Assert.Equal(PickQueueState.Done, only.State);
        Assert.Equal(PickReason.Skipped, only.Assessment!.Reason);
        Assert.Equal("✗", only.Glyph);
        Assert.Null(board.Best());
    }

    [Fact]
    public void Target_file_keeps_urls_and_ping_only_rows()
    {
        var root = Path.Combine(Path.GetTempPath(), "aeroway-pick-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "utils"));
        try
        {
            File.WriteAllText(Path.Combine(root, "utils", "targets.txt"), """
                # comment
                DiscordMain = "https://discord.com/app"
                Dns = "PING:1.1.1.1"
                """);

            var paths = new ZapretPaths(root);
            var endpoints = TestTargetsLoader.LoadEndpoints(paths);
            Assert.Equal(2, endpoints.Count);
            Assert.Equal("https://discord.com/app", endpoints[0].Url);
            Assert.Equal("discord.com", endpoints[0].PingHost);
            Assert.False(endpoints[0].PingOnly);
            Assert.True(endpoints[1].PingOnly);
            Assert.Equal("1.1.1.1", endpoints[1].PingHost);

            var definitions = TestTargetsLoader.LoadDefinitions(paths);
            Assert.False(definitions[0].PingOnly);
            Assert.True(definitions[1].PingOnly);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Missing_targets_file_keeps_the_built_in_names()
    {
        var root = Path.Combine(Path.GetTempPath(), "aeroway-pick-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var endpoints = TestTargetsLoader.LoadEndpoints(new ZapretPaths(root));
            Assert.Contains(endpoints, endpoint => endpoint.Name == "DiscordMain" && endpoint.Url == "https://discord.com");
            Assert.Contains(endpoints, endpoint => endpoint.Name == "Quad9DNS9999" && endpoint.PingOnly && endpoint.PingHost == "9.9.9.9");
            Assert.Equal(endpoints.Count, TestTargetsLoader.LoadDefinitions(new ZapretPaths(root)).Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, "", "OK")]
    [InlineData(28, "timeout", "ERROR")]
    [InlineData(35, "", "UNSUP")]
    [InlineData(1, "TLS 1.3 not supported", "UNSUP")]
    [InlineData(60, "SSL certificate problem: self signed", "SSL")]
    [InlineData(6, "Could not resolve host", "SSL")]
    public void Curl_tokens_follow_the_standard_test_script(int exitCode, string stderr, string expected) =>
        Assert.Equal(expected, BaselineCurlTokens.FromCurl(exitCode, stderr));

    [Fact]
    public void Strategy_pick_is_closed_outside_dev_builds()
    {
#if DEBUG
        Assert.True(AppBuild.IsDevBuild);
#else
        Assert.False(AppBuild.IsDevBuild);
        Assert.False(StrategyPickWindow.TryShow(null!, new ZapretPaths(Path.GetTempPath()), null!, null!));
#endif
    }

    private static TestTargetRow Site(string name, string http, string tls12, string tls13) => new()
    {
        Name = name,
        Http = http,
        Tls12 = tls12,
        Tls13 = tls13,
        Ping = "10 ms"
    };

    private static TestTargetRow Ping(string name, string ping) => new()
    {
        Name = name,
        PingOnly = true,
        Http = "…",
        Tls12 = "…",
        Tls13 = "…",
        Ping = ping
    };
}
