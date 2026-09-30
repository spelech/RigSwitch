namespace RigSwitch.Tests.Unit;

using System.Text.Json;
using RigSwitch.Core.Models;
using Xunit;

public sealed class UserSettingsTests
{
    [Fact]
    public void CachedDeviceNames_DefaultsToEmptyCaseInsensitiveDictionary()
    {
        var settings = new UserSettings();
        Assert.NotNull(settings.CachedDeviceNames);
        Assert.Empty(settings.CachedDeviceNames);

        settings.CachedDeviceNames["MONITOR-1"] = "Main Display";
        Assert.Equal("Main Display", settings.CachedDeviceNames["monitor-1"]);
    }

    [Fact]
    public void CachedDeviceNames_SerializesAndDeserializesCorrectly()
    {
        var settings = new UserSettings();
        settings.CachedDeviceNames["SAM0F12"] = "Samsung Odyssey";
        settings.CachedDeviceNames["{guid-1}"] = "SteelSeries Sonar";

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<UserSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal("Samsung Odyssey", restored.CachedDeviceNames["sam0f12"]);
        Assert.Equal("SteelSeries Sonar", restored.CachedDeviceNames["{guid-1}"]);
    }
}
