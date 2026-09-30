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
    /// Resolves a target audio endpoint ID against detected endpoints, adding an unlisted entry to options if missing.
    /// </summary>
    public static string ResolveAudioOption(
        string targetId,
        IEnumerable<AudioEndpointInfo> endpoints,
        ObservableCollection<DeviceSelectionOption> options)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return string.Empty;

        var match = endpoints.FirstOrDefault(a => MatchesEndpoint(a.Id, targetId));
        if (match != null) return match.Id;

        if (!options.Any(opt => opt.Id == targetId))
            options.Add(new DeviceSelectionOption(targetId, $"{targetId} (Saved / Disconnected)"));

        return targetId;
    }

    /// <summary>
    /// Ensures that a saved display monitor ID is present in the selectable options collection.
    /// </summary>
    public static void EnsureDisplayOption(string monitorId, ObservableCollection<DeviceSelectionOption> options)
    {
        if (!string.IsNullOrWhiteSpace(monitorId) && !options.Any(opt => opt.Id == monitorId))
            options.Add(new DeviceSelectionOption(monitorId, $"{monitorId} (Saved / Disconnected)"));
    }
}
