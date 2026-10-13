namespace RigSwitch.App.Services;

using System.Text.RegularExpressions;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Ipc;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Windows.CoreAudio;

/// <summary>
/// Dispatches incoming IPC requests to live application services and coordinates UI screenshot capture.
/// </summary>
public sealed partial class IpcCommandHandler
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsService;
    private readonly IAudioEndpointWatchdogService _watchdogService;
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly Func<string?, string?, Task<string>>? _screenshotCapturer;

    /// <summary>
    /// Initializes a new instance of the <see cref="IpcCommandHandler"/> class.
    /// </summary>
    /// <param name="coordinator">The workstation profile switch coordinator.</param>
    /// <param name="settingsService">The persistent settings storage service.</param>
    /// <param name="watchdogService">The background CoreAudio endpoint watchdog service.</param>
    /// <param name="displayService">The Windows display configuration service.</param>
    /// <param name="audioDirector">The audio endpoint director service.</param>
    /// <param name="screenshotCapturer">An optional delegate for capturing window screenshots.</param>
    public IpcCommandHandler(
        IProfileSwitchCoordinator coordinator,
        ISettingsStorageService settingsService,
        IAudioEndpointWatchdogService watchdogService,
        IDisplayConfigurationService displayService,
        IAudioEndpointDirector audioDirector,
        Func<string?, string?, Task<string>>? screenshotCapturer = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _watchdogService = watchdogService ?? throw new ArgumentNullException(nameof(watchdogService));
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
        _audioDirector = audioDirector ?? throw new ArgumentNullException(nameof(audioDirector));
        _screenshotCapturer = screenshotCapturer;
    }

    /// <summary>
    /// Handles an incoming IPC request and returns an appropriate IPC response.
    /// </summary>
    /// <param name="request">The incoming IPC request to process.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the asynchronous operation that yields an <see cref="IpcResponse"/>.</returns>
    public async Task<IpcResponse> HandleRequestAsync(IpcRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return IpcResponse.Fail(request.Id, "Command cannot be empty.");
        }

        try
        {
            return request.Command.ToLowerInvariant().Trim() switch
            {
                IpcCommands.Status => await HandleStatusAsync(request, cancellationToken).ConfigureAwait(false),
                IpcCommands.Switch => await HandleSwitchAsync(request, cancellationToken).ConfigureAwait(false),
                IpcCommands.Audio => await HandleAudioAsync(request, cancellationToken).ConfigureAwait(false),
                IpcCommands.Blacklist => await HandleBlacklistAsync(request, cancellationToken).ConfigureAwait(false),
                IpcCommands.Screenshot => await HandleScreenshotAsync(request, cancellationToken).ConfigureAwait(false),
                IpcCommands.Doctor => await HandleDoctorAsync(request, cancellationToken).ConfigureAwait(false),
                _ => IpcResponse.Fail(request.Id, $"Unknown command '{request.Command}'.")
            };
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(request.Id, $"Command execution failed: {ex.Message}");
        }
    }

    private async Task<IpcResponse> HandleStatusAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var settings = await _settingsService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        var displays = await _displayService.EnumerateDisplaysAsync(cancellationToken).ConfigureAwait(false);
        var endpoints = await _audioDirector.EnumerateAudioEndpointsAsync(cancellationToken).ConfigureAwait(false);

        var data = new Dictionary<string, object?>
        {
            ["profile"] = _coordinator.CurrentProfile.ToString(),
            ["audioMode"] = _coordinator.CurrentAudioOutputMode.ToString(),
            ["watchdogActive"] = _watchdogService.IsRunning,
            ["activePresetName"] = _coordinator.CurrentPreset?.Name ?? "Default",
            ["displays"] = displays,
            ["audioEndpoints"] = endpoints,
            ["blacklistedEndpoints"] = settings.BlacklistedAudioEndpointIds ?? []
        };

        return IpcResponse.Ok(request.Id, data, "Status retrieved successfully.");
    }

    private async Task<IpcResponse> HandleSwitchAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        request.Args.TryGetValue("target", out var target);
        if (string.IsNullOrWhiteSpace(target))
        {
            request.Args.TryGetValue("profile", out target);
        }

        ProfileMode targetProfile;
        if (string.Equals(target, "desk", StringComparison.OrdinalIgnoreCase))
        {
            targetProfile = ProfileMode.Desk;
        }
        else if (string.Equals(target, "rig", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(target, "simrig", StringComparison.OrdinalIgnoreCase))
        {
            targetProfile = ProfileMode.SimRig;
        }
        else
        {
            return IpcResponse.Fail(request.Id, $"Invalid or missing switch target '{target}'. Expected 'desk' or 'rig'.");
        }

        bool success = await _coordinator.SwitchProfileAsync(targetProfile, cancellationToken).ConfigureAwait(false);
        if (!success)
        {
            return IpcResponse.Fail(request.Id, $"Failed to switch profile to {targetProfile}.");
        }

        var activePreset = _coordinator.CurrentPreset;
        var data = new Dictionary<string, object?>
        {
            ["profile"] = _coordinator.CurrentProfile.ToString(),
            ["audioMode"] = _coordinator.CurrentAudioOutputMode.ToString(),
            ["activePresetName"] = activePreset?.Name ?? "Default",
            ["display"] = activePreset?.TargetMonitorId,
            ["audio"] = activePreset?.PrimaryAudioId
        };

        return IpcResponse.Ok(request.Id, data, $"Switched to {targetProfile} profile successfully.");
    }

    private async Task<IpcResponse> HandleAudioAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        request.Args.TryGetValue("action", out var action);
        if (string.IsNullOrWhiteSpace(action))
        {
            request.Args.TryGetValue("mode", out action);
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            action = "toggle";
        }

        action = action.ToLowerInvariant().Trim();

        AudioOutputMode newMode;
        if (action == "toggle")
        {
            newMode = await _coordinator.ToggleAudioOutputModeAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (action == "speakers" || action == "primary")
        {
            if (_coordinator.CurrentAudioOutputMode != AudioOutputMode.Primary)
            {
                newMode = await _coordinator.ToggleAudioOutputModeAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                newMode = _coordinator.CurrentAudioOutputMode;
            }
        }
        else if (action == "chat" || action == "secondary")
        {
            if (_coordinator.CurrentAudioOutputMode != AudioOutputMode.Secondary)
            {
                newMode = await _coordinator.ToggleAudioOutputModeAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                newMode = _coordinator.CurrentAudioOutputMode;
            }
        }
        else
        {
            return IpcResponse.Fail(request.Id, $"Invalid audio action '{action}'. Expected 'toggle', 'speakers', or 'chat'.");
        }

        var data = new Dictionary<string, object?>
        {
            ["audioMode"] = newMode.ToString()
        };

        return IpcResponse.Ok(request.Id, data, $"Audio mode set to {newMode}.");
    }

    private async Task<IpcResponse> HandleBlacklistAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        request.Args.TryGetValue("action", out var action);
        if (string.IsNullOrWhiteSpace(action))
        {
            return IpcResponse.Fail(request.Id, "Invalid or missing blacklist action. Expected 'list', 'add', or 'remove'.");
        }

        action = action.ToLowerInvariant().Trim();
        var settings = await _settingsService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        settings.BlacklistedAudioEndpointIds ??= [];

        if (action == "list")
        {
            var listData = new Dictionary<string, object?>
            {
                ["blacklistedEndpoints"] = settings.BlacklistedAudioEndpointIds
            };
            return IpcResponse.Ok(request.Id, listData, "Blacklisted endpoints retrieved.");
        }

        if (action != "add" && action != "remove")
        {
            return IpcResponse.Fail(request.Id, $"Invalid blacklist action '{action}'. Expected 'list', 'add', or 'remove'.");
        }

        request.Args.TryGetValue("id", out var endpointId);
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            request.Args.TryGetValue("endpointId", out endpointId);
        }

        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return IpcResponse.Fail(request.Id, $"Endpoint ID is required for blacklist {action}.");
        }

        if (action == "add")
        {
            if (!settings.BlacklistedAudioEndpointIds.Any(id => MatchesEndpoint(id, endpointId)))
            {
                settings.BlacklistedAudioEndpointIds.Add(endpointId);
                await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
            }

            var addData = new Dictionary<string, object?>
            {
                ["blacklistedEndpoints"] = settings.BlacklistedAudioEndpointIds
            };
            return IpcResponse.Ok(request.Id, addData, $"Added '{endpointId}' to blacklist.");
        }
        else if (action == "remove")
        {
            int removed = settings.BlacklistedAudioEndpointIds.RemoveAll(id => MatchesEndpoint(id, endpointId));
            if (removed > 0)
            {
                await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
            }

            var removeData = new Dictionary<string, object?>
            {
                ["blacklistedEndpoints"] = settings.BlacklistedAudioEndpointIds
            };
            return IpcResponse.Ok(request.Id, removeData, $"Removed '{endpointId}' from blacklist.");
        }
        else
        {
            return IpcResponse.Fail(request.Id, $"Invalid blacklist action '{action}'. Expected 'list', 'add', or 'remove'.");
        }
    }

    private async Task<IpcResponse> HandleScreenshotAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        if (_screenshotCapturer == null)
        {
            return IpcResponse.Fail(request.Id, "UI screenshot capture is not available in headless or non-GUI mode.");
        }

        request.Args.TryGetValue("view", out var view);
        if (string.IsNullOrWhiteSpace(view))
        {
            request.Args.TryGetValue("viewName", out view);
        }

        request.Args.TryGetValue("out", out var outputPath);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            request.Args.TryGetValue("path", out outputPath);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                request.Args.TryGetValue("outputPath", out outputPath);
            }
        }

        try
        {
            string savedPath = await _screenshotCapturer(view, outputPath).ConfigureAwait(false);
            var data = new Dictionary<string, object?>
            {
                ["path"] = savedPath
            };
            return IpcResponse.Ok(request.Id, data, $"Screenshot saved to {savedPath}");
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(request.Id, $"Failed to capture screenshot: {ex.Message}");
        }
    }

    private async Task<IpcResponse> HandleDoctorAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var settings = await _settingsService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        var displays = await _displayService.EnumerateDisplaysAsync(cancellationToken).ConfigureAwait(false);
        var audioEndpoints = await _audioDirector.EnumerateAudioEndpointsAsync(cancellationToken).ConfigureAwait(false);

        var blacklisted = settings.BlacklistedAudioEndpointIds ?? [];
        var blacklistMatches = audioEndpoints
            .Where(e => blacklisted.Any(b => MatchesEndpoint(b, e.Id)))
            .Select(e => new { e.Id, e.Name })
            .ToList();

        var data = new Dictionary<string, object?>
        {
            ["currentProfile"] = _coordinator.CurrentProfile.ToString(),
            ["activePreset"] = _coordinator.CurrentPreset?.Name ?? "Default",
            ["audioMode"] = _coordinator.CurrentAudioOutputMode.ToString(),
            ["watchdogActive"] = _watchdogService.IsRunning,
            ["displays"] = displays,
            ["audioEndpoints"] = audioEndpoints,
            ["blacklistedEndpoints"] = blacklisted,
            ["blacklistMatches"] = blacklistMatches
        };

        return IpcResponse.Ok(request.Id, data, "System diagnostic self-check completed.");
    }

    [GeneratedRegex(@"(?:\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.RightToLeft)]
    private static partial Regex GuidPattern();

    private static bool MatchesEndpoint(string? idA, string? idB)
    {
        if (string.IsNullOrWhiteSpace(idA) || string.IsNullOrWhiteSpace(idB))
        {
            return false;
        }

        if (string.Equals(idA, idB, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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
}
