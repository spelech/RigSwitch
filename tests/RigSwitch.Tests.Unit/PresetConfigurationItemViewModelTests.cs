namespace RigSwitch.Tests.Unit;

using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

public sealed class PresetConfigurationItemViewModelTests
{
    [Fact]
    public void PresetConfigurationItemViewModel_BindsMicrophonePropertiesCorrectly()
    {
        var preset = new WorkstationPreset
        {
            PrimaryMicrophoneId = "{MIC-PRIMARY}",
            FallbackMicrophoneId = "{MIC-FALLBACK}"
        };

        var vm = new PresetConfigurationItemViewModel(preset, 0, "DeskGroup", true);

        Assert.Equal("{MIC-PRIMARY}", vm.PrimaryMicrophoneId);
        Assert.Equal("{MIC-FALLBACK}", vm.FallbackMicrophoneId);

        vm.PrimaryMicrophoneId = "{NEW-MIC-1}";
        vm.FallbackMicrophoneId = "{NEW-MIC-2}";

        var target = new WorkstationPreset();
        vm.ApplyTo(target);

        Assert.Equal("{NEW-MIC-1}", target.PrimaryMicrophoneId);
        Assert.Equal("{NEW-MIC-2}", target.FallbackMicrophoneId);
    }

    [Fact]
    public void PresetConfigurationItemViewModel_BindsVolumePropertiesCorrectly()
    {
        var preset = new WorkstationPreset
        {
            PlaybackVolume = new AudioVolumeSettings(PresetVolumeBehavior.Custom, 75, isMuted: true),
            MicrophoneVolume = new AudioVolumeSettings(PresetVolumeBehavior.Retain, 50, isMuted: false)
        };

        var vm = new PresetConfigurationItemViewModel(preset, 0, "DeskGroup", true);

        Assert.Equal(PresetVolumeBehavior.Custom, vm.PlaybackVolumeMode);
        Assert.True(vm.IsCustomPlaybackVolume);
        Assert.Equal(75, vm.PlaybackVolumePercent);
        Assert.True(vm.PlaybackVolumeIsMuted);

        Assert.Equal(PresetVolumeBehavior.Retain, vm.MicrophoneVolumeMode);
        Assert.False(vm.IsCustomMicrophoneVolume);

        // Modify values
        vm.PlaybackVolumePercent = 120; // Clamped to 100
        Assert.Equal(100, vm.PlaybackVolumePercent);

        vm.PlaybackVolumePercent = -10; // Clamped to 0
        Assert.Equal(0, vm.PlaybackVolumePercent);

        vm.MicrophoneVolumeMode = PresetVolumeBehavior.Custom;
        Assert.True(vm.IsCustomMicrophoneVolume);
        vm.MicrophoneVolumePercent = 85;
        vm.MicrophoneVolumeIsMuted = true;

        var target = new WorkstationPreset();
        vm.ApplyTo(target);

        Assert.Equal(PresetVolumeBehavior.Custom, target.PlaybackVolume.Mode);
        Assert.Equal(0, target.PlaybackVolume.VolumePercent);
        Assert.True(target.PlaybackVolume.IsMuted);

        Assert.Equal(PresetVolumeBehavior.Custom, target.MicrophoneVolume.Mode);
        Assert.Equal(85, target.MicrophoneVolume.VolumePercent);
        Assert.True(target.MicrophoneVolume.IsMuted);
    }

    [Fact]
    public void PresetConfigurationItemViewModel_BindsHdrModeCorrectly()
    {
        var preset = new WorkstationPreset
        {
            HdrMode = PresetHdrMode.Enable
        };

        var vm = new PresetConfigurationItemViewModel(preset, 0, "DeskGroup", true);

        Assert.Equal(PresetHdrMode.Enable, vm.HdrMode);

        vm.HdrMode = PresetHdrMode.Disable;

        var target = new WorkstationPreset();
        vm.ApplyTo(target);

        Assert.Equal(PresetHdrMode.Disable, target.HdrMode);
    }

    [Fact]
    public void PresetConfigurationItemViewModel_BindsSecondaryAudioAndMicrophonePropertiesCorrectly()
    {
        var preset = new WorkstationPreset
        {
            PrimaryAudioId = "{SPK-1}",
            SecondaryAudioId = "{HEADPHONES-1}",
            PrimaryMicrophoneId = "{MIC-1}",
            SecondaryMicrophoneId = "{HEADSET-MIC-1}"
        };

        var vm = new PresetConfigurationItemViewModel(preset, 0, "DeskGroup", true);

        Assert.Equal("{SPK-1}", vm.PrimaryAudioId);
        Assert.Equal("{HEADPHONES-1}", vm.SecondaryAudioId);
        Assert.Equal("{MIC-1}", vm.PrimaryMicrophoneId);
        Assert.Equal("{HEADSET-MIC-1}", vm.SecondaryMicrophoneId);

        vm.SecondaryAudioId = "{NEW-SEC-AUDIO}";
        vm.SecondaryMicrophoneId = "{NEW-SEC-MIC}";

        var target = new WorkstationPreset();
        vm.ApplyTo(target);

        Assert.Equal("{NEW-SEC-AUDIO}", target.SecondaryAudioId);
        Assert.Equal("{NEW-SEC-MIC}", target.SecondaryMicrophoneId);
    }

    [Fact]
    public void PresetConfigurationItemViewModel_UpdateFrom_SynchronizesAllProperties()
    {
        var initial = new WorkstationPreset { Name = "Old Name" };
        var vm = new PresetConfigurationItemViewModel(initial, 0, "DeskGroup", true);

        var updated = new WorkstationPreset
        {
            Name = "New Racing Rig",
            TargetMonitorId = "MON-RIG-1",
            TargetMonitorIds = ["MON-RIG-1", "MON-RIG-2"],
            PrimaryAudioId = "{RIG-SPK}",
            SecondaryAudioId = "{RIG-HEADPHONES}",
            FallbackAudioId = "{RIG-FALLBACK}",
            PrimaryMicrophoneId = "{RIG-MIC}",
            SecondaryMicrophoneId = "{RIG-HEADSET-MIC}",
            FallbackMicrophoneId = "{RIG-FALLBACK-MIC}",
            HdrMode = PresetHdrMode.Enable,
            DirectHotkey = "Ctrl+Shift+1",
            PlaybackVolume = new AudioVolumeSettings(PresetVolumeBehavior.Custom, 80, false),
            MicrophoneVolume = new AudioVolumeSettings(PresetVolumeBehavior.Custom, 90, false)
        };

        vm.UpdateFrom(updated);

        Assert.Equal("New Racing Rig", vm.Name);
        Assert.Equal("MON-RIG-1", vm.TargetMonitorId);
        Assert.Equal(["MON-RIG-1", "MON-RIG-2"], vm.TargetMonitorIds);
        Assert.Equal("{RIG-SPK}", vm.PrimaryAudioId);
        Assert.Equal("{RIG-HEADPHONES}", vm.SecondaryAudioId);
        Assert.Equal("{RIG-FALLBACK}", vm.FallbackAudioId);
        Assert.Equal("{RIG-MIC}", vm.PrimaryMicrophoneId);
        Assert.Equal("{RIG-HEADSET-MIC}", vm.SecondaryMicrophoneId);
        Assert.Equal("{RIG-FALLBACK-MIC}", vm.FallbackMicrophoneId);
        Assert.Equal(PresetHdrMode.Enable, vm.HdrMode);
        Assert.Equal("Ctrl+Shift+1", vm.DirectHotkey);
        Assert.Equal(80, vm.PlaybackVolumePercent);
        Assert.Equal(90, vm.MicrophoneVolumePercent);
    }
}
