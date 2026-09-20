namespace RigSwitch.Tests.Unit;

using RigSwitch.Core.Models;
using Xunit;

public sealed class PresetApplicationHookTests
{
    [Fact]
    public void PresetApplicationHook_DefaultValues_AreCorrect()
    {
        var hook = new PresetApplicationHook();

        Assert.False(string.IsNullOrWhiteSpace(hook.Id));
        Assert.Equal(string.Empty, hook.ExecutablePath);
        Assert.Equal(string.Empty, hook.Arguments);
        Assert.True(hook.CloseOnSwitchAway);
    }

    [Fact]
    public void PresetApplicationHook_CustomValues_RetainedProperly()
    {
        var hook = new PresetApplicationHook
        {
            Id = "test-hook-1",
            ExecutablePath = @"C:\Games\SimHub\SimHubWPF.exe",
            Arguments = "--minimized",
            CloseOnSwitchAway = false
        };

        Assert.Equal("test-hook-1", hook.Id);
        Assert.Equal(@"C:\Games\SimHub\SimHubWPF.exe", hook.ExecutablePath);
        Assert.Equal("--minimized", hook.Arguments);
        Assert.False(hook.CloseOnSwitchAway);
    }

    [Fact]
    public void WorkstationPreset_ApplicationHooks_DefaultsToEmptyList()
    {
        var preset = new WorkstationPreset();

        Assert.NotNull(preset.ApplicationHooks);
        Assert.Empty(preset.ApplicationHooks);
    }

    [Fact]
    public void WorkstationPreset_ApplicationHooks_CanBePopulated()
    {
        var preset = new WorkstationPreset();
        var hook = new PresetApplicationHook
        {
            ExecutablePath = @"C:\Program Files\Moza\PitHouse.exe"
        };

        preset.ApplicationHooks.Add(hook);

        Assert.Single(preset.ApplicationHooks);
        Assert.Equal(@"C:\Program Files\Moza\PitHouse.exe", preset.ApplicationHooks[0].ExecutablePath);
    }
}
