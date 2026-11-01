namespace RigSwitch.Tests.Unit;

using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

public sealed class HdrPresetModelsTests
{
    [Fact]
    public void WorkstationPreset_DefaultHdrMode_IsRetain()
    {
        var preset = new WorkstationPreset();
        Assert.Equal(PresetHdrMode.Retain, preset.HdrMode);
    }

    [Theory]
    [InlineData(PresetHdrMode.Retain)]
    [InlineData(PresetHdrMode.Enable)]
    [InlineData(PresetHdrMode.Disable)]
    public void WorkstationPreset_CanSetHdrMode(PresetHdrMode mode)
    {
        var preset = new WorkstationPreset { HdrMode = mode };
        Assert.Equal(mode, preset.HdrMode);
    }

    [Fact]
    public void DisplayDeviceInfo_DefaultsSupportsAndEnabledHdrToFalse()
    {
        var info = new DisplayDeviceInfo("MON1", @"\\.\DISPLAY1", "Monitor", "GPU", true, true);
        Assert.False(info.SupportsHdr);
        Assert.False(info.IsHdrEnabled);
    }

    [Fact]
    public void DisplayDeviceInfo_WithHdrProperties_PreservesValues()
    {
        var info = new DisplayDeviceInfo(
            "MON1",
            @"\\.\DISPLAY1",
            "Monitor",
            "GPU",
            isActive: true,
            isPrimary: true,
            supportsHdr: true,
            isHdrEnabled: true);

        Assert.True(info.SupportsHdr);
        Assert.True(info.IsHdrEnabled);
    }

    [Fact]
    public void DisplayHdrInfo_StoresAllColorMetadata()
    {
        var hdrInfo = new DisplayHdrInfo(
            MonitorId: "MSI4DD0",
            SupportsHdr: true,
            IsHdrEnabled: true,
            WideColorEnforced: false,
            ColorEncoding: DisplayColorEncoding.Rgb,
            BitsPerColorChannel: 10);

        Assert.Equal("MSI4DD0", hdrInfo.MonitorId);
        Assert.True(hdrInfo.SupportsHdr);
        Assert.True(hdrInfo.IsHdrEnabled);
        Assert.False(hdrInfo.WideColorEnforced);
        Assert.Equal(DisplayColorEncoding.Rgb, hdrInfo.ColorEncoding);
        Assert.Equal(10, hdrInfo.BitsPerColorChannel);
    }
}
