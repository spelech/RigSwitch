namespace RigSwitch.App;

using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RigSwitch.App.Services;
using RigSwitch.App.ViewModels;
using RigSwitch.App.Views;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Core.Services;
using RigSwitch.Infrastructure.Ipc;
using RigSwitch.Infrastructure.Services;
using RigSwitch.Infrastructure.Storage;
using RigSwitch.Infrastructure.Windows.Ccd;
using RigSwitch.Infrastructure.Windows.CoreAudio;
using RigSwitch.Infrastructure.Windows.Hotkeys;
using RigSwitch.Infrastructure.Windows.Processes;
using WinForms = System.Windows.Forms;
using Application = System.Windows.Application;

/// <summary>
/// Interaction logic for App.xaml and composition root for RigSwitch.
/// </summary>
public partial class App : Application
{
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    private IHost? _host;
    private TrayIconService? _trayIconService;
    private MainSettingsWindow? _mainWindow;

    /// <inheritdoc/>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            bool isCli = e.Args.Length > 0 &&
                !e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                                a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                                a.Equals("--settings", StringComparison.OrdinalIgnoreCase) ||
                                a.Equals("--show", StringComparison.OrdinalIgnoreCase));

            if (isCli)
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                var stdOut = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(stdOut);
                var stdError = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
                Console.SetError(stdError);
            }
            else
            {
                WinForms.Application.EnableVisualStyles();
            }

            var builder = Host.CreateApplicationBuilder(e.Args);
            if (isCli)
            {
                builder.Logging.ClearProviders();
            }

            builder.Services.AddSingleton<ISettingsStorageService>(_ => new JsonSettingsStorageService());
            builder.Services.AddSingleton<IDisplayConfigurationService>(_ => new WindowsDisplayConfigurationService());
            builder.Services.AddSingleton<INativeAudioProvider>(_ => new WindowsNativeAudioProvider());
            builder.Services.AddSingleton<IAudioEndpointDirector>(sp => new CoreAudioEndpointDirector(sp.GetRequiredService<INativeAudioProvider>()));
            builder.Services.AddSingleton<IAudioEndpointWatchdogService, AudioEndpointWatchdogService>();
            builder.Services.AddSingleton<IGlobalHotkeyService>(_ => new WindowsGlobalHotkeyService());
            builder.Services.AddSingleton<IApplicationLifecycleHookService>(_ => new WindowsApplicationLifecycleHookService());
            builder.Services.AddSingleton<IUpdateCheckService>(_ => new UpdateCheckService());
            builder.Services.AddSingleton<IProfileSwitchCoordinator>(sp =>
                new ProfileSwitchCoordinator(
                    sp.GetRequiredService<IDisplayConfigurationService>(),
                    sp.GetRequiredService<IAudioEndpointDirector>(),
                    sp.GetRequiredService<ISettingsStorageService>(),
                    appHookService: sp.GetRequiredService<IApplicationLifecycleHookService>(),
                    onIntentionalAudioSwitch: sp.GetRequiredService<IAudioEndpointWatchdogService>().NotifyIntentionalSwitch));

            builder.Services.AddSingleton<TrayIconService>();
            builder.Services.AddSingleton<MainSettingsViewModel>();
            builder.Services.AddSingleton<MainSettingsWindow>();

            builder.Services.AddSingleton<IIpcServer>(_ => new NamedPipeIpcServer());
            builder.Services.AddSingleton<IIpcClient>(_ => new NamedPipeIpcClient());
            builder.Services.AddSingleton<IpcCommandHandler>(sp =>
                new IpcCommandHandler(
                    sp.GetRequiredService<IProfileSwitchCoordinator>(),
                    sp.GetRequiredService<ISettingsStorageService>(),
                    sp.GetRequiredService<IAudioEndpointWatchdogService>(),
                    sp.GetRequiredService<IDisplayConfigurationService>(),
                    sp.GetRequiredService<IAudioEndpointDirector>(),
                    (view, path) =>
                    {
                        var window = Dispatcher.Invoke(() => _mainWindow ??= sp.GetRequiredService<MainSettingsWindow>());
                        return window.CaptureScreenshotAsync(view, path);
                    }));
            builder.Services.AddSingleton<CliController>();

            _host = builder.Build();
            await _host.StartAsync();

            if (isCli)
            {
                var cliController = _host.Services.GetRequiredService<CliController>();
                int exitCode = await cliController.RunAsync(e.Args);
                Shutdown(exitCode);
                return;
            }

            var settingsStorage = _host.Services.GetRequiredService<ISettingsStorageService>();
            var coordinator = _host.Services.GetRequiredService<IProfileSwitchCoordinator>();
            var viewModel = _host.Services.GetRequiredService<MainSettingsViewModel>();
            _trayIconService = _host.Services.GetRequiredService<TrayIconService>();

            var watchdog = _host.Services.GetRequiredService<IAudioEndpointWatchdogService>();
            watchdog.Start();

            var ipcServer = _host.Services.GetRequiredService<IIpcServer>();
            var commandHandler = _host.Services.GetRequiredService<IpcCommandHandler>();
            ipcServer.RegisterHandler(req => commandHandler.HandleRequestAsync(req));

            try
            {
                await ipcServer.StartAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to start IPC server: {ex.Message}");
            }

            _trayIconService.Initialize(ShowSettingsWindow, settingsStorage);

            var settings = await settingsStorage.LoadSettingsAsync();
            _trayIconService.ShowToastNotifications = settings.ShowToastNotifications;

            // Synchronize profile on startup: detect active display or fallback to LastActiveProfile
            var activeProfile = settings.LastActiveProfile;
            try
            {
                var displayService = _host.Services.GetRequiredService<IDisplayConfigurationService>();
                var displays = await displayService.EnumerateDisplaysAsync();
                var activeDisplay = displays.FirstOrDefault(d => d.IsActive && d.IsPrimary)
                    ?? displays.FirstOrDefault(d => d.IsActive);

                if (activeDisplay != null)
                {
                    if (activeDisplay.Matches(settings.RigMonitorId))
                    {
                        activeProfile = ProfileMode.SimRig;
                    }
                    else if (activeDisplay.Matches(settings.DeskMonitorId))
                    {
                        activeProfile = ProfileMode.Desk;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Startup display detection failed: {ex.Message}");
            }

            if (coordinator.CurrentProfile != activeProfile)
            {
                coordinator.SetCurrentProfile(activeProfile);
            }

            _trayIconService.UpdateTrayState(coordinator.CurrentProfile, coordinator.CurrentPreset);

            viewModel.RegisterGlobalHotkeys(settings);

            coordinator.ProfileChanged += (sender, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (args.Success)
                    {
                        _trayIconService.UpdateTrayState(args.NewProfile, args.ActivePreset);
                        _trayIconService.ShowNotification("RigSwitch", $"Switched to {args.NewProfile} setup [{args.ActivePreset?.Name ?? "Default"}].");
                    }
                    else
                    {
                        _trayIconService.ShowNotification("RigSwitch Error", args.ErrorMessage ?? $"Failed to switch to {args.NewProfile}.");
                    }
                });
            };

            var cmdArgs = Environment.GetCommandLineArgs();
            bool isExplicitMinimized = cmdArgs.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
            bool isExplicitShow = cmdArgs.Any(a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase) || a.Equals("--show", StringComparison.OrdinalIgnoreCase));

            if (!isExplicitMinimized || isExplicitShow)
            {
                ShowSettingsWindow();
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await viewModel.CheckForUpdatesAutoAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning($"Startup update check failed: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"A critical error occurred while starting RigSwitch:\n\n{ex.Message}",
                "RigSwitch Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    private void ShowSettingsWindow()
    {
        if (_host == null)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            _mainWindow ??= _host.Services.GetRequiredService<MainSettingsWindow>();
            MainWindow = _mainWindow;
            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
            _mainWindow.Focus();
        });
    }

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_mainWindow != null)
        {
            _mainWindow.SetExplicitShutdown();
            _mainWindow.Close();
        }

        var watchdog = _host?.Services.GetService<IAudioEndpointWatchdogService>();
        watchdog?.Stop();
        watchdog?.Dispose();

        var hotkeyService = _host?.Services.GetService<IGlobalHotkeyService>();
        hotkeyService?.UnregisterAll();

        _trayIconService?.Dispose();

        var ipcServer = _host?.Services.GetService<IIpcServer>();
        if (ipcServer != null)
        {
            try
            {
                ipcServer.StopAsync().GetAwaiter().GetResult();
                ipcServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Error stopping IPC server: {ex.Message}");
            }
        }

        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();

        base.OnExit(e);
    }
}
