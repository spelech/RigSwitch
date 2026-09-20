namespace RigSwitch.App;

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RigSwitch.App.Services;
using RigSwitch.App.ViewModels;
using RigSwitch.App.Views;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Services;
using RigSwitch.Infrastructure.Storage;
using RigSwitch.Infrastructure.Windows.Ccd;
using RigSwitch.Infrastructure.Windows.CoreAudio;
using RigSwitch.Infrastructure.Windows.Hotkeys;
using Application = System.Windows.Application;

/// <summary>
/// Interaction logic for App.xaml and composition root for RigSwitch.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private TrayIconService? _trayIconService;
    private MainSettingsWindow? _mainWindow;

    /// <inheritdoc/>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var builder = Host.CreateApplicationBuilder(e.Args);

        builder.Services.AddSingleton<ISettingsStorageService>(_ => new JsonSettingsStorageService());
        builder.Services.AddSingleton<IDisplayConfigurationService>(_ => new WindowsDisplayConfigurationService());
        builder.Services.AddSingleton<IAudioEndpointDirector>(_ => new CoreAudioEndpointDirector());
        builder.Services.AddSingleton<IGlobalHotkeyService>(_ => new WindowsGlobalHotkeyService());
        builder.Services.AddSingleton<IProfileSwitchCoordinator>(sp =>
            new ProfileSwitchCoordinator(
                sp.GetRequiredService<IDisplayConfigurationService>(),
                sp.GetRequiredService<IAudioEndpointDirector>(),
                sp.GetRequiredService<ISettingsStorageService>()));

        builder.Services.AddSingleton<TrayIconService>();
        builder.Services.AddSingleton<MainSettingsViewModel>();
        builder.Services.AddSingleton<MainSettingsWindow>();

        _host = builder.Build();
        await _host.StartAsync();

        var settingsStorage = _host.Services.GetRequiredService<ISettingsStorageService>();
        var coordinator = _host.Services.GetRequiredService<IProfileSwitchCoordinator>();
        var viewModel = _host.Services.GetRequiredService<MainSettingsViewModel>();
        _mainWindow = _host.Services.GetRequiredService<MainSettingsWindow>();
        _trayIconService = _host.Services.GetRequiredService<TrayIconService>();

        _trayIconService.Initialize(ShowSettingsWindow);

        var settings = await settingsStorage.LoadSettingsAsync();
        _trayIconService.ShowToastNotifications = settings.ShowToastNotifications;
        _trayIconService.UpdateTrayState(coordinator.CurrentProfile);

        viewModel.RegisterGlobalHotkeys(settings);

        coordinator.ProfileChanged += (sender, args) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (args.Success)
                {
                    _trayIconService.UpdateTrayState(args.NewProfile);
                    _trayIconService.ShowNotification("RigSwitch", $"Switched to {args.NewProfile} setup.");
                }
                else
                {
                    _trayIconService.ShowNotification("RigSwitch Error", args.ErrorMessage ?? $"Failed to switch to {args.NewProfile}.");
                }
            });
        };

        bool startMinimized = settings.StartMinimizedToTray;
        if (!startMinimized)
        {
            ShowSettingsWindow();
        }
    }

    private void ShowSettingsWindow()
    {
        if (_mainWindow == null)
        {
            return;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_mainWindow != null)
        {
            _mainWindow.SetExplicitShutdown();
            _mainWindow.Close();
        }

        var hotkeyService = _host?.Services.GetService<IGlobalHotkeyService>();
        hotkeyService?.UnregisterAll();

        _trayIconService?.Dispose();

        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();

        base.OnExit(e);
    }
}
