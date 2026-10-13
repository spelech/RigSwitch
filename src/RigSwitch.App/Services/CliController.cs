namespace RigSwitch.App.Services;

using System.IO;
using System.Text.Json;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Ipc;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Ipc;

/// <summary>
/// Parses command-line arguments and coordinates CLI execution via IPC client or headless fallback.
/// </summary>
public sealed class CliController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IIpcClient _ipcClient;
    private readonly IpcCommandHandler _headlessHandler;
    private readonly TextWriter _outWriter;
    private readonly TextWriter _errorWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliController"/> class.
    /// </summary>
    /// <param name="ipcClient">The IPC client for communicating with the running GUI process.</param>
    /// <param name="headlessHandler">The fallback handler for executing commands headlessly when the GUI is inactive.</param>
    /// <param name="outWriter">Optional output writer for standard output (defaults to <see cref="Console.Out"/>).</param>
    /// <param name="errorWriter">Optional output writer for error output (defaults to <see cref="Console.Error"/>).</param>
    public CliController(
        IIpcClient ipcClient,
        IpcCommandHandler headlessHandler,
        TextWriter? outWriter = null,
        TextWriter? errorWriter = null)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
        _headlessHandler = headlessHandler ?? throw new ArgumentNullException(nameof(headlessHandler));
        _outWriter = outWriter ?? Console.Out;
        _errorWriter = errorWriter ?? Console.Error;
    }

    /// <summary>
    /// Executes the specified command-line arguments asynchronously.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>Exit code 0 on success, or 1 on error.</returns>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || args.Any(a => a is "--help" or "-h" or "-?"))
        {
            await PrintUsageAsync().ConfigureAwait(false);
            return 0;
        }

        bool jsonOutput = args.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase));
        var cleanArgs = args.Where(a => !string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (cleanArgs.Length == 0)
        {
            await PrintUsageAsync().ConfigureAwait(false);
            return 0;
        }

        string command = cleanArgs[0].ToLowerInvariant();

        return command switch
        {
            IpcCommands.Status => await HandleStatusAsync(jsonOutput, cancellationToken).ConfigureAwait(false),
            IpcCommands.Switch => await HandleSwitchAsync(cleanArgs, jsonOutput, cancellationToken).ConfigureAwait(false),
            IpcCommands.Audio => await HandleAudioAsync(cleanArgs, jsonOutput, cancellationToken).ConfigureAwait(false),
            IpcCommands.Blacklist => await HandleBlacklistAsync(cleanArgs, jsonOutput, cancellationToken).ConfigureAwait(false),
            IpcCommands.Screenshot => await HandleScreenshotAsync(cleanArgs, jsonOutput, cancellationToken).ConfigureAwait(false),
            IpcCommands.Doctor => await HandleDoctorAsync(jsonOutput, cancellationToken).ConfigureAwait(false),
            _ => await HandleUnknownCommandAsync(cleanArgs[0]).ConfigureAwait(false)
        };
    }

    private async Task<int> HandleStatusAsync(bool jsonOutput, CancellationToken cancellationToken)
    {
        var request = new IpcRequest(IpcCommands.Status);
        var response = await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response == null || !response.Success)
        {
            await _errorWriter.WriteLineAsync(response?.Message ?? "Failed to retrieve workstation status.").ConfigureAwait(false);
            return 1;
        }

        if (jsonOutput)
        {
            await WriteJsonAsync(response.Data).ConfigureAwait(false);
            return 0;
        }

        var profile = GetString(response.Data, "profile") ?? "Unknown";
        var preset = GetString(response.Data, "activePresetName") ?? "Default";
        var audioMode = GetString(response.Data, "audioMode") ?? "Unknown";
        bool watchdogActive = GetBool(response.Data, "watchdogActive");
        var displays = GetList(response.Data, "displays");
        var endpoints = GetList(response.Data, "audioEndpoints");
        var blacklisted = GetList(response.Data, "blacklistedEndpoints");

        await _outWriter.WriteLineAsync("RigSwitch Status").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Profile: {profile}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Active Preset: {preset}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Audio Output Mode: {audioMode}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Watchdog Status: {(watchdogActive ? "Active" : "Stopped")}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Displays: {FormatItemList(displays)}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Audio Endpoints: {FormatItemList(endpoints)}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Blacklisted DACs: {FormatItemList(blacklisted)}").ConfigureAwait(false);

        return 0;
    }

    private async Task<int> HandleSwitchAsync(string[] cleanArgs, bool jsonOutput, CancellationToken cancellationToken)
    {
        string? target = cleanArgs.Length > 1 ? cleanArgs[1] : null;

        if (string.IsNullOrWhiteSpace(target) ||
            (!string.Equals(target, "desk", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(target, "rig", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(target, "simrig", StringComparison.OrdinalIgnoreCase)))
        {
            await _errorWriter.WriteLineAsync($"Target workstation profile is required ('desk' or 'rig'). Received: '{target}'.").ConfigureAwait(false);
            return 1;
        }

        var request = new IpcRequest(IpcCommands.Switch, new() { ["target"] = target });
        var response = await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response == null || !response.Success)
        {
            await _errorWriter.WriteLineAsync(response?.Message ?? $"Failed to switch profile to '{target}'.").ConfigureAwait(false);
            return 1;
        }

        if (jsonOutput)
        {
            await WriteJsonAsync(response.Data).ConfigureAwait(false);
            return 0;
        }

        var profile = GetString(response.Data, "profile") ?? target;
        var display = GetString(response.Data, "display") ?? "Default";
        var audio = GetString(response.Data, "audio") ?? "Default";

        await _outWriter.WriteLineAsync($"Switched profile to: {profile} | Display: {display} | Audio: {audio}").ConfigureAwait(false);
        return 0;
    }

    private async Task<int> HandleAudioAsync(string[] cleanArgs, bool jsonOutput, CancellationToken cancellationToken)
    {
        string action = cleanArgs.Length > 1 ? cleanArgs[1].ToLowerInvariant() : "toggle";

        if (action != "toggle" && action != "speakers" && action != "primary" && action != "chat" && action != "secondary")
        {
            await _errorWriter.WriteLineAsync($"Invalid audio action '{action}'. Expected 'toggle', 'speakers', or 'chat'.").ConfigureAwait(false);
            return 1;
        }

        var request = new IpcRequest(IpcCommands.Audio, new() { ["action"] = action });
        var response = await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response == null || !response.Success)
        {
            await _errorWriter.WriteLineAsync(response?.Message ?? "Failed to set audio output mode.").ConfigureAwait(false);
            return 1;
        }

        if (jsonOutput)
        {
            await WriteJsonAsync(response.Data).ConfigureAwait(false);
            return 0;
        }

        var mode = GetString(response.Data, "audioMode") ?? action;
        await _outWriter.WriteLineAsync($"Audio output mode set to: {mode}").ConfigureAwait(false);
        return 0;
    }

    private async Task<int> HandleBlacklistAsync(string[] cleanArgs, bool jsonOutput, CancellationToken cancellationToken)
    {
        string action = cleanArgs.Length > 1 ? cleanArgs[1].ToLowerInvariant() : "list";

        if (action != "list" && action != "add" && action != "remove")
        {
            await _errorWriter.WriteLineAsync($"Invalid blacklist action '{action}'. Expected 'list', 'add', or 'remove'.").ConfigureAwait(false);
            return 1;
        }

        string? endpointId = cleanArgs.Length > 2 ? cleanArgs[2] : null;

        if ((action == "add" || action == "remove") && string.IsNullOrWhiteSpace(endpointId))
        {
            await _errorWriter.WriteLineAsync($"Endpoint ID or name is required for 'blacklist {action}'.").ConfigureAwait(false);
            return 1;
        }

        var requestArgs = new Dictionary<string, string?> { ["action"] = action };
        if (!string.IsNullOrWhiteSpace(endpointId))
        {
            requestArgs["id"] = endpointId;
        }

        var request = new IpcRequest(IpcCommands.Blacklist, requestArgs);
        var response = await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response == null || !response.Success)
        {
            await _errorWriter.WriteLineAsync(response?.Message ?? $"Blacklist {action} operation failed.").ConfigureAwait(false);
            return 1;
        }

        if (jsonOutput)
        {
            await WriteJsonAsync(response.Data).ConfigureAwait(false);
            return 0;
        }

        if (action == "list")
        {
            var list = GetList(response.Data, "blacklistedEndpoints");
            await _outWriter.WriteLineAsync("Blacklisted Audio Endpoints:").ConfigureAwait(false);
            if (list.Count == 0)
            {
                await _outWriter.WriteLineAsync("  (None)").ConfigureAwait(false);
            }
            else
            {
                foreach (var item in list)
                {
                    await _outWriter.WriteLineAsync($"  - {item}").ConfigureAwait(false);
                }
            }
        }
        else
        {
            await _outWriter.WriteLineAsync(response.Message ?? $"Blacklist {action} succeeded for '{endpointId}'.").ConfigureAwait(false);
        }

        return 0;
    }

    private async Task<int> HandleScreenshotAsync(string[] cleanArgs, bool jsonOutput, CancellationToken cancellationToken)
    {
        string? view = null;
        string? outputPath = null;

        for (int i = 1; i < cleanArgs.Length; i++)
        {
            if (string.Equals(cleanArgs[i], "--view", StringComparison.OrdinalIgnoreCase) && i + 1 < cleanArgs.Length)
            {
                view = cleanArgs[++i];
            }
            else if (string.Equals(cleanArgs[i], "--out", StringComparison.OrdinalIgnoreCase) && i + 1 < cleanArgs.Length)
            {
                outputPath = cleanArgs[++i];
            }
        }

        var reqArgs = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(view))
        {
            reqArgs["view"] = view;
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            reqArgs["out"] = outputPath;
        }

        var request = new IpcRequest(IpcCommands.Screenshot, reqArgs);
        var response = await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            await _errorWriter.WriteLineAsync("Screenshot capture requires the RigSwitch GUI application to be running in the system tray.").ConfigureAwait(false);
            return 1;
        }

        if (!response.Success)
        {
            await _errorWriter.WriteLineAsync(response.Message ?? "Failed to capture UI screenshot.").ConfigureAwait(false);
            return 1;
        }

        if (jsonOutput)
        {
            await WriteJsonAsync(response.Data).ConfigureAwait(false);
            return 0;
        }

        var savedPath = GetString(response.Data, "path") ?? outputPath ?? "Unknown";
        await _outWriter.WriteLineAsync($"Screenshot saved to: {savedPath}").ConfigureAwait(false);
        return 0;
    }

    private async Task<int> HandleDoctorAsync(bool jsonOutput, CancellationToken cancellationToken)
    {
        var request = new IpcRequest(IpcCommands.Doctor);
        var response = await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response == null || !response.Success)
        {
            await _errorWriter.WriteLineAsync(response?.Message ?? "Diagnostic self-check failed.").ConfigureAwait(false);
            return 1;
        }

        if (jsonOutput)
        {
            await WriteJsonAsync(response.Data).ConfigureAwait(false);
            return 0;
        }

        var profile = GetString(response.Data, "currentProfile") ?? "Unknown";
        var preset = GetString(response.Data, "activePreset") ?? "Default";
        var audioMode = GetString(response.Data, "audioMode") ?? "Unknown";
        bool watchdogActive = GetBool(response.Data, "watchdogActive");
        var displays = GetList(response.Data, "displays");
        var endpoints = GetList(response.Data, "audioEndpoints");
        var blacklistMatches = GetList(response.Data, "blacklistMatches");

        await _outWriter.WriteLineAsync("RigSwitch Diagnostic Doctor").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Current Profile: {profile}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Active Preset: {preset}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Audio Mode: {audioMode}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Watchdog Status: {(watchdogActive ? "Active" : "Stopped")}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Displays Detected: {displays.Count}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Audio Endpoints Detected: {endpoints.Count}").ConfigureAwait(false);
        await _outWriter.WriteLineAsync($"Blacklist Matches: {blacklistMatches.Count}").ConfigureAwait(false);

        return 0;
    }

    private async Task<int> HandleUnknownCommandAsync(string command)
    {
        await _errorWriter.WriteLineAsync($"Unknown command '{command}'. Run 'rigswitch --help' for usage.").ConfigureAwait(false);
        return 1;
    }

    private async Task<IpcResponse?> ExecuteRequestAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        bool isConnected = false;
        try
        {
            isConnected = await _ipcClient.IsServerRunningAsync(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            isConnected = false;
        }

        if (isConnected)
        {
            try
            {
                return await _ipcClient.SendAsync(request, TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // IPC send failed / disconnected
            }
        }

        if (string.Equals(request.Command, IpcCommands.Screenshot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await _headlessHandler.HandleRequestAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteJsonAsync(object? data)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        await _outWriter.WriteLineAsync(json).ConfigureAwait(false);
    }

    private async Task PrintUsageAsync()
    {
        await _outWriter.WriteLineAsync("""
RigSwitch CLI - Workstation Profile & Audio Routing Controller

Usage:
  rigswitch [command] [subcommand/target] [options]

Commands:
  status                     Display active workstation profile, audio output mode, and watchdog health
  switch <desk|rig>          Switch to Desk or Sim Rig workstation preset
  audio [toggle|speakers|chat]
                             Toggle or set audio output mode (default: toggle)
  blacklist [list|add|remove] [id]
                             Manage excluded haptic transducer DACs
  screenshot                 Render live settings UI to an image file (requires running GUI)
                             Options:
                               --view <presets|displays|audio|nicknames|hotkeys>
                               --out <filepath> (default: %TEMP%\rigswitch-view.png)
  doctor                     Perform comprehensive hardware diagnostic & watchdog health audit

Global Options:
  --json                     Output structured JSON instead of human-readable text
  -h, --help                 Show command usage and exit
""").ConfigureAwait(false);
    }

    private static string? GetString(Dictionary<string, object?>? data, string key)
    {
        if (data == null || !data.TryGetValue(key, out var val) || val == null)
        {
            return null;
        }

        if (val is JsonElement elem)
        {
            return elem.ValueKind switch
            {
                JsonValueKind.String => elem.GetString(),
                _ => elem.ToString()
            };
        }

        return val.ToString();
    }

    private static bool GetBool(Dictionary<string, object?>? data, string key)
    {
        if (data == null || !data.TryGetValue(key, out var val) || val == null)
        {
            return false;
        }

        if (val is bool b)
        {
            return b;
        }

        if (val is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (elem.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (bool.TryParse(elem.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        if (bool.TryParse(val.ToString(), out var strParsed))
        {
            return strParsed;
        }

        return false;
    }

    private static List<string> GetList(Dictionary<string, object?>? data, string key)
    {
        var result = new List<string>();
        if (data == null || !data.TryGetValue(key, out var val) || val == null)
        {
            return result;
        }

        if (val is JsonElement elem && elem.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in elem.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    result.Add(item.GetString() ?? string.Empty);
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    if (item.TryGetProperty("name", out var nameProp))
                    {
                        result.Add(nameProp.GetString() ?? item.ToString());
                    }
                    else if (item.TryGetProperty("Name", out var nameProp2))
                    {
                        result.Add(nameProp2.GetString() ?? item.ToString());
                    }
                    else if (item.TryGetProperty("id", out var idProp))
                    {
                        result.Add(idProp.GetString() ?? item.ToString());
                    }
                    else
                    {
                        result.Add(item.ToString());
                    }
                }
                else
                {
                    result.Add(item.ToString());
                }
            }

            return result;
        }

        if (val is System.Collections.IEnumerable enumerable && val is not string)
        {
            foreach (var item in enumerable)
            {
                if (item is DisplayDeviceInfo d)
                {
                    result.Add($"{d.FriendlyName} ({d.MonitorId})");
                }
                else if (item is AudioEndpointInfo a)
                {
                    result.Add($"{a.Name} ({a.Id})");
                }
                else
                {
                    result.Add(item?.ToString() ?? string.Empty);
                }
            }

            return result;
        }

        result.Add(val.ToString() ?? string.Empty);
        return result;
    }

    private static string FormatItemList(List<string> items)
    {
        if (items.Count == 0)
        {
            return "(None)";
        }

        return string.Join(", ", items);
    }
}
