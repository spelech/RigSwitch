namespace RigSwitch.App.ViewModels;

using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;

/// <summary>
/// Provides matching and resolution for audio and display device selection options.
/// </summary>
internal static partial class DeviceOptionResolver
{
    [GeneratedRegex(@"(?:\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.RightToLeft)]
    private static partial Regex GuidPattern();

    /// <summary>
    /// Determines whether two audio endpoint identifiers refer to the same physical device,
    /// comparing either direct strings or extracted GUID components.
    /// </summary>
    public static bool MatchesEndpoint(string? idA, string? idB)
    {
        if (string.IsNullOrWhiteSpace(idA) || string.IsNullOrWhiteSpace(idB)) return false;
        if (string.Equals(idA, idB, StringComparison.OrdinalIgnoreCase)) return true;

        var matchA = GuidPattern().Match(idA);
        var matchB = GuidPattern().Match(idB);

        if (matchA.Success && matchB.Success &&
            Guid.TryParse(matchA.Value, out var guidA) &&
            Guid.TryParse(matchB.Value, out var guidB))
        {
            return guidA == guidB;
        }

        return false;
    }

    /// <summary>
    /// Filters an audio endpoint item based on search text and presence state filter selection.
    /// </summary>
    public static bool MatchesAudioFilter(AudioEndpointVisibilityItemViewModel item, string searchText, string filterSelection)
    {
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            bool nameMatch = item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            bool adapterMatch = item.Adapter.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            if (!nameMatch && !adapterMatch) return false;
        }

        if (string.Equals(filterSelection, "Active", StringComparison.OrdinalIgnoreCase))
            return item.State == DevicePresenceState.Active;

        if (string.Equals(filterSelection, "Inactive", StringComparison.OrdinalIgnoreCase))
            return item.State != DevicePresenceState.Active;

        return true;
    }

    /// <summary>
    /// Formats an audio endpoint label for an offline or disconnected device.
    /// </summary>
    public static string FormatOfflineAudioLabel(
        string id,
        string? detectedName,
        IDictionary<string, string>? customNames,
        IDictionary<string, string>? cachedNames)
    {
        string nick = string.Empty;
        if (customNames != null && customNames.TryGetValue(id, out var custom) && !string.IsNullOrWhiteSpace(custom))
        {
            nick = custom;
        }

        string baseName = detectedName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseName) && cachedNames != null && cachedNames.TryGetValue(id, out var cached))
        {
            baseName = cached;
        }

        if (!string.IsNullOrWhiteSpace(nick) && !string.IsNullOrWhiteSpace(baseName))
            return $"{nick} ({baseName}) (Disconnected)";
        if (!string.IsNullOrWhiteSpace(nick))
            return $"{nick} (Disconnected)";
        if (!string.IsNullOrWhiteSpace(baseName))
            return $"{baseName} (Disconnected)";

        return $"{id} (Disconnected)";
    }

    /// <summary>
    /// Formats a display monitor label for an offline or disconnected display.
    /// </summary>
    public static string FormatOfflineDisplayLabel(
        string monitorId,
        IDictionary<string, string>? customNames,
        IDictionary<string, string>? cachedNames)
    {
        string nick = string.Empty;
        if (customNames != null && customNames.TryGetValue(monitorId, out var custom) && !string.IsNullOrWhiteSpace(custom))
        {
            nick = custom;
        }

        string baseName = string.Empty;
        if (cachedNames != null && cachedNames.TryGetValue(monitorId, out var cached) && !string.IsNullOrWhiteSpace(cached))
        {
            baseName = cached;
        }

        if (!string.IsNullOrWhiteSpace(nick) && !string.IsNullOrWhiteSpace(baseName))
            return $"{nick} ({baseName}) [{monitorId}] (Disconnected)";
        if (!string.IsNullOrWhiteSpace(nick))
            return $"{nick} [{monitorId}] (Disconnected)";
        if (!string.IsNullOrWhiteSpace(baseName))
            return $"{baseName} [{monitorId}] (Disconnected)";

        return $"{monitorId} (Disconnected)";
    }

    /// <summary>
    /// Resolves a target audio endpoint ID against detected endpoints, adding an unlisted entry to options if missing.
    /// </summary>
    public static string ResolveAudioOption(
        string targetId,
        IEnumerable<AudioEndpointInfo> endpoints,
        ObservableCollection<DeviceSelectionOption> options,
        IDictionary<string, string>? customNames = null,
        IDictionary<string, string>? cachedNames = null)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return string.Empty;

        var match = endpoints.FirstOrDefault(a => MatchesEndpoint(a.Id, targetId));
        if (match != null)
        {
            if (!options.Any(opt => opt.Id == match.Id))
            {
                var label = FormatOfflineAudioLabel(match.Id, match.Name, customNames, cachedNames);
                options.Add(new DeviceSelectionOption(match.Id, label));
            }
            return match.Id;
        }

        if (!options.Any(opt => opt.Id == targetId))
        {
            var label = FormatOfflineAudioLabel(targetId, null, customNames, cachedNames);
            options.Add(new DeviceSelectionOption(targetId, label));
        }

        return targetId;
    }

    /// <summary>
    /// Resolves a target audio endpoint ID against detected endpoints, adding an unlisted entry to options if missing.
    /// </summary>
    public static string ResolveAudioOption(
        string targetId,
        IEnumerable<AudioEndpointInfo> endpoints,
        ObservableCollection<DeviceSelectionOption> options)
        => ResolveAudioOption(targetId, endpoints, options, null, null);

    /// <summary>
    /// Ensures that a saved display monitor ID is present in the selectable options collection.
    /// </summary>
    public static void EnsureDisplayOption(
        string monitorId,
        ObservableCollection<DeviceSelectionOption> options,
        IDictionary<string, string>? customNames = null,
        IDictionary<string, string>? cachedNames = null)
    {
        if (!string.IsNullOrWhiteSpace(monitorId) && !options.Any(opt => opt.Id == monitorId))
        {
            var label = FormatOfflineDisplayLabel(monitorId, customNames, cachedNames);
            options.Add(new DeviceSelectionOption(monitorId, label));
        }
    }

    /// <summary>
    /// Ensures that a saved display monitor ID is present in the selectable options collection.
    /// </summary>
    public static void EnsureDisplayOption(string monitorId, ObservableCollection<DeviceSelectionOption> options)
        => EnsureDisplayOption(monitorId, options, null, null);
}
