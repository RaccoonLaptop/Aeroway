using Aeroway.Services;
using Xunit;

namespace Aeroway.Tests;

public class SingleInstanceCompatTests
{
    [Fact]
    public void Mutex_and_pipe_names_still_match_installed_zapretui_builds()
    {
        Assert.Contains("ZapretUI_SingleInstance", SingleInstanceService.InstanceMutexName, StringComparison.Ordinal);
        Assert.Contains("ZapretUI.ShowWindow", SingleInstanceService.ActivationPipeName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Second_launch_activates_the_existing_process()
    {
        var name = "aeroway-instance-" + Guid.NewGuid().ToString("N");
        var seen = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = SingleInstanceService.Acquire(name, name, waitForRelease: false);
        try
        {
            first.CommandReceived += command => seen.TrySetResult(command);
            using var second = SingleInstanceService.Acquire(name, name, waitForRelease: false);
            Assert.True(first.IsFirstInstance);
            Assert.False(second.IsFirstInstance);
            Assert.True(SingleInstanceService.TryActivateExisting(name));
            var completed = await Task.WhenAny(seen.Task, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.Same(seen.Task, completed);
            Assert.Equal("SHOW", await seen.Task);
        }
        finally
        {
            first.Dispose();
        }

        using var third = SingleInstanceService.Acquire(name, name, waitForRelease: false);
        Assert.True(third.IsFirstInstance);
    }

    [Fact]
    public void Activate_returns_false_when_no_instance_is_listening()
    {
        var name = "aeroway-missing-" + Guid.NewGuid().ToString("N");
        Assert.False(SingleInstanceService.TryActivateExisting(name));
    }

    [Fact]
    public async Task Waiting_acquire_takes_over_after_the_first_instance_exits()
    {
        var name = "aeroway-wait-" + Guid.NewGuid().ToString("N");
        var holder = SingleInstanceService.Acquire(name, name, waitForRelease: false);
        var release = Task.Run(async () =>
        {
            await Task.Delay(200);
            holder.Dispose();
        });

        using var waited = SingleInstanceService.Acquire(name, name, waitForRelease: true);
        Assert.True(waited.IsFirstInstance);
        await release.WaitAsync(TimeSpan.FromSeconds(3));
    }
}
