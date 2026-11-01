namespace RigSwitch.Tests.Unit;

using System.Text.Json;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

public sealed class AudioPresetModelsTests
{
    [Fact]
    public void AudioVolumeSettings_DefaultsAndClamping_WorkCorrectly()
    {
        var settings = new AudioVolumeSettings();

        // Default behavior: Retain system volume, 50% baseline if enabled, not muted
        Assert.Equal(PresetVolumeBehavior.Retain, settings.Mode);
        Assert.Equal(50, settings.VolumePercent);
        Assert.False(settings.IsMuted);

        // Clamping lower bound
        settings.VolumePercent = -20;
        Assert.Equal(0, settings.VolumePercent);

        // Clamping upper bound
        settings.VolumePercent = 140;
        Assert.Equal(100, settings.VolumePercent);

        // Valid range
        settings.VolumePercent = 75;
        Assert.Equal(75, settings.VolumePercent);
    }

    [Fact]
    public void WorkstationPreset_MicrophoneAndVolumeDefaults_AreConfigured()
    {
        var preset = new WorkstationPreset();

        Assert.Equal(string.Empty, preset.PrimaryMicrophoneId);
        Assert.Equal(string.Empty, preset.FallbackMicrophoneId);
        Assert.NotNull(preset.PlaybackVolume);
        Assert.Equal(PresetVolumeBehavior.Retain, preset.PlaybackVolume.Mode);
        Assert.NotNull(preset.MicrophoneVolume);
        Assert.Equal(PresetVolumeBehavior.Retain, preset.MicrophoneVolume.Mode);
    }

    [Fact]
    public void WorkstationPreset_SerializesAndRestoresMicrophoneAndVolumeSettings()
    {
        var preset = new WorkstationPreset
        {
            Name = "Sim Rig GT3",
            PrimaryMicrophoneId = "{0.0.1.00000000}.{BOOM_MIC_GUID}",
            FallbackMicrophoneId = "{0.0.1.00000000}.{VR_MIC_GUID}",
            PlaybackVolume = new AudioVolumeSettings
            {
                Mode = PresetVolumeBehavior.Custom,
                VolumePercent = 85,
                IsMuted = false
            },
            MicrophoneVolume = new AudioVolumeSettings
            {
                Mode = PresetVolumeBehavior.Custom,
                VolumePercent = 100,
                IsMuted = false
            }
        };

        var json = JsonSerializer.Serialize(preset);
        var restored = JsonSerializer.Deserialize<WorkstationPreset>(json);

        Assert.NotNull(restored);
        Assert.Equal("{0.0.1.00000000}.{BOOM_MIC_GUID}", restored.PrimaryMicrophoneId);
        Assert.Equal("{0.0.1.00000000}.{VR_MIC_GUID}", restored.FallbackMicrophoneId);
        Assert.Equal(PresetVolumeBehavior.Custom, restored.PlaybackVolume.Mode);
        Assert.Equal(85, restored.PlaybackVolume.VolumePercent);
        Assert.Equal(100, restored.MicrophoneVolume.VolumePercent);
    }

    [Fact]
    public void UserSettings_DeskAndRigMicrophoneProperties_MapToActivePresets()
    {
        var settings = new UserSettings();

        settings.DeskPrimaryMicrophoneId = "{DESK_MIC}";
        Assert.Equal("{DESK_MIC}", settings.GetActivePreset(ProfileMode.Desk).PrimaryMicrophoneId);
        Assert.Equal("{DESK_MIC}", settings.DeskPrimaryMicrophoneId);

        settings.RigPrimaryMicrophoneId = "{RIG_MIC}";
        Assert.Equal("{RIG_MIC}", settings.GetActivePreset(ProfileMode.SimRig).PrimaryMicrophoneId);
        Assert.Equal("{RIG_MIC}", settings.RigPrimaryMicrophoneId);
    }
}
