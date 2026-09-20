namespace RigSwitch.Tests.Unit;

using RigSwitch.Core.Enums;
using RigSwitch.Core.Events;
using RigSwitch.Core.Models;
using Xunit;

public sealed class WorkstationPresetTests
{
    [Fact]
    public void WorkstationPreset_DefaultConstructor_InitializesWithValidDefaults()
    {
        // Act
        var preset = new WorkstationPreset();

        // Assert
        Assert.NotNull(preset.Id);
        Assert.True(Guid.TryParse(preset.Id, out _));
        Assert.Equal("Default Preset", preset.Name);
        Assert.Equal(string.Empty, preset.TargetMonitorId);
        Assert.Equal(string.Empty, preset.PrimaryAudioId);
        Assert.Equal(string.Empty, preset.FallbackAudioId);
        Assert.Equal(string.Empty, preset.DirectHotkey);
        Assert.NotNull(preset.LaunchApplicationPaths);
        Assert.Empty(preset.LaunchApplicationPaths);
    }

    [Fact]
    public void WorkstationPreset_CustomProperties_RetainAssignedValues()
    {
        // Arrange & Act
        var preset = new WorkstationPreset
        {
            Name = "Custom Preset",
            TargetMonitorId = "MON_1",
            PrimaryAudioId = "AUDIO_1",
            FallbackAudioId = "AUDIO_2",
            DirectHotkey = "Ctrl+Shift+1",
            LaunchApplicationPaths = ["C:\\app.exe"]
        };

        // Assert
        Assert.Equal("Custom Preset", preset.Name);
        Assert.Equal("MON_1", preset.TargetMonitorId);
        Assert.Equal("AUDIO_1", preset.PrimaryAudioId);
        Assert.Equal("AUDIO_2", preset.FallbackAudioId);
        Assert.Equal("Ctrl+Shift+1", preset.DirectHotkey);
        Assert.Single(preset.LaunchApplicationPaths, "C:\\app.exe");
    }

    [Fact]
    public void UserSettings_Defaults_ContainsThreeDeskAndThreeRigPresets()
    {
        // Act
        var settings = new UserSettings();

        // Assert
        Assert.Equal(0, settings.ActiveDeskPresetIndex);
        Assert.Equal(0, settings.ActiveRigPresetIndex);

        Assert.Equal(3, settings.DeskPresets.Count);
        Assert.Equal("Work / Primary", settings.DeskPresets[0].Name);
        Assert.Equal("Media / Casual", settings.DeskPresets[1].Name);
        Assert.Equal("Clean Desk", settings.DeskPresets[2].Name);

        Assert.Equal(3, settings.RigPresets.Count);
        Assert.Equal("GT3 / Circuit", settings.RigPresets[0].Name);
        Assert.Equal("Rally / Drift", settings.RigPresets[1].Name);
        Assert.Equal("Flight / Space", settings.RigPresets[2].Name);
    }

    [Fact]
    public void UserSettings_GetActivePreset_ReturnsExpectedPresetForMode()
    {
        // Arrange
        var settings = new UserSettings
        {
            ActiveDeskPresetIndex = 1,
            ActiveRigPresetIndex = 2
        };

        // Act & Assert
        var activeDeskPreset = settings.GetActivePreset(ProfileMode.Desk);
        var activeRigPreset = settings.GetActivePreset(ProfileMode.SimRig);

        Assert.Same(settings.DeskPresets[1], activeDeskPreset);
        Assert.Equal("Media / Casual", activeDeskPreset.Name);

        Assert.Same(settings.RigPresets[2], activeRigPreset);
        Assert.Equal("Flight / Space", activeRigPreset.Name);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(3)]
    [InlineData(99)]
    public void UserSettings_GetActivePreset_BoundsChecking_FallsBackToIndexZero(int outOfBoundsIndex)
    {
        // Arrange
        var settings = new UserSettings
        {
            ActiveDeskPresetIndex = outOfBoundsIndex,
            ActiveRigPresetIndex = outOfBoundsIndex
        };

        // Act & Assert
        var deskPreset = settings.GetActivePreset(ProfileMode.Desk);
        var rigPreset = settings.GetActivePreset(ProfileMode.SimRig);

        Assert.Same(settings.DeskPresets[0], deskPreset);
        Assert.Same(settings.RigPresets[0], rigPreset);
    }

    [Fact]
    public void UserSettings_GetActivePreset_WhenPresetsEmptyOrNull_ReturnsNonNullFallback()
    {
        // Arrange
        var settings = new UserSettings
        {
            DeskPresets = [],
            RigPresets = null!
        };

        // Act
        var deskPreset = settings.GetActivePreset(ProfileMode.Desk);
        var rigPreset = settings.GetActivePreset(ProfileMode.SimRig);

        // Assert
        Assert.NotNull(deskPreset);
        Assert.NotNull(rigPreset);
        Assert.NotEmpty(settings.DeskPresets);
        Assert.NotNull(settings.RigPresets);
        Assert.NotEmpty(settings.RigPresets);
    }

    [Fact]
    public void UserSettings_BackwardCompatibilityProperties_RouteToAndFromActivePreset()
    {
        // Arrange
        var settings = new UserSettings();

        // Act - set via legacy properties
        settings.DeskMonitorId = "DESK_MON_A";
        settings.DeskPrimaryAudioId = "DESK_AUD_PRI";
        settings.DeskFallbackAudioId = "DESK_AUD_FALL";

        settings.RigMonitorId = "RIG_MON_A";
        settings.RigPrimaryAudioId = "RIG_AUD_PRI";

        // Assert - active preset 0 updated
        Assert.Equal("DESK_MON_A", settings.DeskPresets[0].TargetMonitorId);
        Assert.Equal("DESK_AUD_PRI", settings.DeskPresets[0].PrimaryAudioId);
        Assert.Equal("DESK_AUD_FALL", settings.DeskPresets[0].FallbackAudioId);

        Assert.Equal("RIG_MON_A", settings.RigPresets[0].TargetMonitorId);
        Assert.Equal("RIG_AUD_PRI", settings.RigPresets[0].PrimaryAudioId);

        // Assert - read back via legacy properties
        Assert.Equal("DESK_MON_A", settings.DeskMonitorId);
        Assert.Equal("DESK_AUD_PRI", settings.DeskPrimaryAudioId);
        Assert.Equal("DESK_AUD_FALL", settings.DeskFallbackAudioId);
        Assert.Equal("RIG_MON_A", settings.RigMonitorId);
        Assert.Equal("RIG_AUD_PRI", settings.RigPrimaryAudioId);

        // Act - change active preset index and verify legacy property tracks the new active preset
        settings.ActiveDeskPresetIndex = 1;
        settings.DeskPresets[1].TargetMonitorId = "DESK_MON_B";
        settings.DeskPresets[1].PrimaryAudioId = "DESK_AUD_B";

        Assert.Equal("DESK_MON_B", settings.DeskMonitorId);
        Assert.Equal("DESK_AUD_B", settings.DeskPrimaryAudioId);

        // Act - modify via legacy property while on preset 1
        settings.DeskMonitorId = "DESK_MON_B_MODIFIED";
        Assert.Equal("DESK_MON_B_MODIFIED", settings.DeskPresets[1].TargetMonitorId);
        Assert.Equal("DESK_MON_A", settings.DeskPresets[0].TargetMonitorId); // preset 0 unchanged
    }

    [Fact]
    public void ProfileChangedEventArgs_IncludesActivePresetProperty()
    {
        // Arrange
        var preset = new WorkstationPreset { Name = "Sprint Race" };

        // Act
        var argsWithPreset = new ProfileChangedEventArgs(
            ProfileMode.Desk,
            ProfileMode.SimRig,
            preset,
            success: true,
            errorMessage: null);

        var argsWithoutPreset = new ProfileChangedEventArgs(
            ProfileMode.Desk,
            ProfileMode.SimRig,
            success: true);

        // Assert
        Assert.Same(preset, argsWithPreset.ActivePreset);
        Assert.Null(argsWithoutPreset.ActivePreset);
    }
}
