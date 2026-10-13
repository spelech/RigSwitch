namespace RigSwitch.Tests.Unit;

using System.Text.Json;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

public sealed class UserSettingsAudioTests
{
    [Fact]
    public void WorkstationPreset_HasDefaultSecondaryAudioProperties()
    {
        var preset = new WorkstationPreset();
        Assert.Equal(string.Empty, preset.SecondaryAudioId);
        Assert.Equal(string.Empty, preset.SecondaryMicrophoneId);
    }

    [Fact]
    public void UserSettings_HasDefaultAudioExclusionAndToggleSettings()
    {
        var settings = new UserSettings();
        Assert.Empty(settings.BlacklistedAudioEndpointIds);
        Assert.Equal("Ctrl+Alt+A", settings.AudioToggleHotkey);
        Assert.True(settings.EnforcePrimaryAudio);
        Assert.Equal(AudioOutputMode.Primary, settings.ActiveAudioOutputMode);
    }

    [Fact]
    public void UserSettings_JsonSerialization_RoundtripsNewAudioProperties()
    {
        var original = new UserSettings
        {
            AudioToggleHotkey = "Ctrl+Shift+A",
            EnforcePrimaryAudio = false,
            ActiveAudioOutputMode = AudioOutputMode.Secondary,
            BlacklistedAudioEndpointIds = ["{DAC-1}", "{DAC-2}"]
        };
        original.DeskPresets[0].SecondaryAudioId = "{SONAR-HEADSET}";
        original.DeskPresets[0].SecondaryMicrophoneId = "{BOOM-MIC}";

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<UserSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal("Ctrl+Shift+A", restored.AudioToggleHotkey);
        Assert.False(restored.EnforcePrimaryAudio);
        Assert.Equal(AudioOutputMode.Secondary, restored.ActiveAudioOutputMode);
        Assert.Equal(["{DAC-1}", "{DAC-2}"], restored.BlacklistedAudioEndpointIds);
        Assert.Equal("{SONAR-HEADSET}", restored.DeskPresets[0].SecondaryAudioId);
        Assert.Equal("{BOOM-MIC}", restored.DeskPresets[0].SecondaryMicrophoneId);
    }
}
