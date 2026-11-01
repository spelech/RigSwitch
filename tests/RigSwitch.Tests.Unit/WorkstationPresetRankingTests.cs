using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

namespace RigSwitch.Tests.Unit;

public class WorkstationPresetRankingTests
{
    private static readonly string[] ExpectedFallbackAudio = ["a"];
    private static readonly string[] ExpectedExplicitAudio = ["x", "y"];
    private static readonly string[] ExpectedFallbackMic = ["m1", "m2"];

    [Fact]
    public void EffectiveAudioList_FallsBackToLegacyFields()
    {
        var p = new WorkstationPreset { PrimaryAudioId = "a", SecondaryAudioId = "", FallbackAudioId = "A" };
        Assert.Equal(ExpectedFallbackAudio, p.GetEffectiveAudioPriorityList());
    }

    [Fact]
    public void EffectiveAudioList_PrefersExplicitList()
    {
        var p = new WorkstationPreset { PrimaryAudioId = "legacy", AudioPriorityIds = ["x", "y"] };
        Assert.Equal(ExpectedExplicitAudio, p.GetEffectiveAudioPriorityList());
    }

    [Fact]
    public void EffectiveMicList_FallsBackToLegacyFields()
    {
        var p = new WorkstationPreset { PrimaryMicrophoneId = "m1", FallbackMicrophoneId = "m2" };
        Assert.Equal(ExpectedFallbackMic, p.GetEffectiveMicrophonePriorityList());
    }

    [Fact]
    public void SyncLegacyAudioFieldsFromPriorityList_UpdatesLegacyProperties()
    {
        var p = new WorkstationPreset { AudioPriorityIds = ["dev1", "dev2", "dev3", "dev4"] };
        p.SyncLegacyAudioFieldsFromPriorityList();

        Assert.Equal("dev1", p.PrimaryAudioId);
        Assert.Equal("dev2", p.SecondaryAudioId);
        Assert.Equal("dev3", p.FallbackAudioId);
    }

    [Fact]
    public void MonitorPlacement_StoresPositionResolutionPrimaryAndHdrMode()
    {
        var placement = new MonitorPlacement("MSI4DD0", -3440, 0, 3440, 1440, true, PresetHdrMode.Enable);

        Assert.Equal("MSI4DD0", placement.MonitorId);
        Assert.Equal(-3440, placement.PositionX);
        Assert.Equal(0, placement.PositionY);
        Assert.Equal(3440, placement.Width);
        Assert.Equal(1440, placement.Height);
        Assert.True(placement.IsPrimary);
        Assert.Equal(PresetHdrMode.Enable, placement.HdrMode);
    }
}
