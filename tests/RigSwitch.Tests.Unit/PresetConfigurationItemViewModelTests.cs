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
}
