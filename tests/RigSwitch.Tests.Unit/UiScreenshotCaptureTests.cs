namespace RigSwitch.Tests.Unit;

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NSubstitute;
using RigSwitch.App.ViewModels;
using RigSwitch.App.Views;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using Xunit;

[Collection("WPF UI Tests")]
public sealed class UiScreenshotCaptureTests
{
    [Fact]
    public void CaptureUiScreenshots()
    {
        RunInSta(() =>
        {
            if (Application.Current == null)
            {
                try
                {
                    _ = new Application();
                }
                catch (InvalidOperationException)
                {
                    // Ignore if already initialized
                }
            }

            var coordinator = Substitute.For<IProfileSwitchCoordinator>();
            var settingsService = Substitute.For<ISettingsStorageService>();
            var displayService = Substitute.For<IDisplayConfigurationService>();
            var audioDirector = Substitute.For<IAudioEndpointDirector>();
            var hotkeyService = Substitute.For<IGlobalHotkeyService>();
            var trayIconService = new RigSwitch.App.Services.TrayIconService(coordinator, settingsService);

            var userSettings = new UserSettings
            {
                DeskMonitorId = "MON1",
                DeskPrimaryAudioId = "AUD1",
                RigMonitorId = "MON3",
                RigPrimaryAudioId = "AUD2",
                BlacklistedAudioEndpointIds = ["AUD6"]
            };
            userSettings.CustomDeviceNames["MON1"] = "Desk Ultrawide OLED";
            userSettings.CustomDeviceNames["MON3"] = "Sim Rig Odyssey G9";
            userSettings.CustomDeviceNames["AUD1"] = "Desk Pebble Speakers";
            userSettings.CustomDeviceNames["AUD2"] = "Audeze Maxwell Headset";

            userSettings.DeskPresets.Clear();
            userSettings.RigPresets.Clear();

            userSettings.DeskPresets.Add(new WorkstationPreset
            {
                Name = "Workstation Dual Monitor",
                TargetMonitorId = "MON1",
                TargetMonitorIds = ["MON1", "MON2"],
                MonitorPlacements =
                [
                    new MonitorPlacement("MON1", 0, 0, 3440, 1440, true, PresetHdrMode.Enable),
                    new MonitorPlacement("MON2", 3440, 0, 2560, 1440, false, PresetHdrMode.Disable)
                ],
                AudioPriorityIds = ["AUD1", "AUD2", "AUD3"],
                MicrophonePriorityIds = ["MIC1", "MIC2"],
                PrimaryAudioId = "AUD1",
                SecondaryAudioId = "AUD2",
                FallbackAudioId = "AUD3",
                DirectHotkey = "Ctrl+Alt+F1",
                ApplicationHooks =
                [
                    new PresetApplicationHook
                    {
                        ExecutablePath = @"C:\Program Files\Elgato\StreamDeck\StreamDeck.exe",
                        CloseOnSwitchAway = false
                    }
                ]
            });
            userSettings.DeskPresets.Add(new WorkstationPreset
            {
                Name = "Single Focus Ultrawide",
                TargetMonitorId = "MON1",
                TargetMonitorIds = ["MON1"],
                AudioPriorityIds = ["AUD2", "AUD1"]
            });
            userSettings.DeskPresets.Add(new WorkstationPreset
            {
                Name = "Late Night Quiet Mode",
                TargetMonitorId = "MON1",
                TargetMonitorIds = ["MON1"],
                AudioPriorityIds = ["AUD2"]
            });

            userSettings.RigPresets.Add(new WorkstationPreset
            {
                Name = "GT3 Super Ultrawide + Telem",
                TargetMonitorId = "MON3",
                TargetMonitorIds = ["MON3", "MON4"],
                MonitorPlacements =
                [
                    new MonitorPlacement("MON3", 0, 0, 5120, 1440, true, PresetHdrMode.Enable),
                    new MonitorPlacement("MON4", 1280, -1080, 1920, 1080, false, PresetHdrMode.Disable)
                ],
                AudioPriorityIds = ["AUD2", "AUD1"],
                MicrophonePriorityIds = ["MIC1"],
                PrimaryAudioId = "AUD2",
                FallbackAudioId = "AUD1",
                DirectHotkey = "Ctrl+Alt+F5",
                ApplicationHooks =
                [
                    new PresetApplicationHook
                    {
                        ExecutablePath = @"C:\Program Files (x86)\SimHub\SimHubWPF.exe",
                        CloseOnSwitchAway = true
                    }
                ]
            });
            userSettings.RigPresets.Add(new WorkstationPreset
            {
                Name = "VR OpenXR Racing",
                TargetMonitorId = "MON3",
                TargetMonitorIds = ["MON3"],
                AudioPriorityIds = ["AUD4", "AUD2"]
            });
            userSettings.RigPresets.Add(new WorkstationPreset
            {
                Name = "Rally Endurance Setup",
                TargetMonitorId = "MON3",
                TargetMonitorIds = ["MON3"],
                AudioPriorityIds = ["AUD2"]
            });

            settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(userSettings);
            displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>()).Returns([
                new DisplayDeviceInfo("MON1", @"\\.\DISPLAY1", "ASUS ROG PG34WCDM", "NVIDIA RTX 4090", true, true, true, true, 0, 0, 3440, 1440),
                new DisplayDeviceInfo("MON2", @"\\.\DISPLAY2", "LG DualUp 28MQ780", "NVIDIA RTX 4090", false, true, true, false, 3440, 0, 2560, 1440),
                new DisplayDeviceInfo("MON3", @"\\.\DISPLAY3", "Samsung Odyssey G9", "NVIDIA RTX 4090", false, false, true, true, -5120, 0, 5120, 1440),
                new DisplayDeviceInfo("MON4", @"\\.\DISPLAY4", "Sim Pit Overhead Telemetry", "NVIDIA RTX 4090", false, false, false, false, 1280, -1080, 1920, 1080)
            ]);
            audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns([
                new AudioEndpointInfo("AUD1", "Creative Pebble V3 Speakers", "Realtek USB Audio", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback),
                new AudioEndpointInfo("AUD2", "Audeze Maxwell Wireless Gaming Headset", "Audeze USB Dongle", DevicePresenceState.Active, false, true, AudioDeviceFlow.Playback),
                new AudioEndpointInfo("AUD3", "SteelSeries Sonar - Virtual Gaming", "SteelSeries Sonar Virtual Audio", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
                new AudioEndpointInfo("AUD4", "Oculus Virtual Audio Device", "Meta Quest Link Audio", DevicePresenceState.Disabled, false, false, AudioDeviceFlow.Playback),
                new AudioEndpointInfo("AUD5", "Realtek Digital Output (Optical)", "Realtek High Definition Audio", DevicePresenceState.Unplugged, false, false, AudioDeviceFlow.Playback),
                new AudioEndpointInfo("AUD6", "Nobsound USB Bass Shaker Amp", "USB PnP Sound Device", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
                new AudioEndpointInfo("GHOST1", "Old Unreferenced HDMI Monitor Audio", "NVIDIA High Definition Audio", DevicePresenceState.NotPresent, false, false, AudioDeviceFlow.Playback),
                new AudioEndpointInfo("MIC1", "Audeze Maxwell Boom Mic", "Audeze USB Dongle", DevicePresenceState.Active, true, true, AudioDeviceFlow.Capture),
                new AudioEndpointInfo("MIC2", "Shure MV7 XLR/USB Podcast Mic", "Shure Digital Audio", DevicePresenceState.Active, false, false, AudioDeviceFlow.Capture)
            ]);

            var vm = new MainSettingsViewModel(
                coordinator,
                settingsService,
                displayService,
                audioDirector,
                hotkeyService,
                trayIconService);
            vm.LoadAsync().GetAwaiter().GetResult();

            var window = new MainSettingsWindow(vm)
            {
                Width = 1240,
                Height = 840
            };

            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "screenshots");
            Directory.CreateDirectory(outDir);

            window.Show();

            void CaptureView(int index, string filename)
            {
                window.ContentTabControl.SelectedIndex = index;
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                window.UpdateLayout();
                SaveWindowToPng(window, Path.Combine(outDir, filename));
            }

            // 1. Desk 1 Preset View (2D Monitor Topology + Ranked Audio + Mic + App Hooks)
            vm.SelectPresetForNavigation(vm.DeskPresets[0]);
            CaptureView(0, "01_presets_view.png");

            // 2. Sim Rig 1 Preset View (Super Ultrawide + Overhead Telemetry 2D Layout)
            vm.SelectPresetForNavigation(vm.RigPresets[0]);
            CaptureView(1, "02_displays_topology_view.png");

            // 3. Audio & DAC Lockout View (Relevant Devices Filter + Bass Shaker Lockout)
            vm.NavigateToSystemPage(2);
            CaptureView(2, "03_audio_dac_lockout_view.png");

            // 4. Device Nicknames View (+ Add Nickname bar + Configured Nicknames list)
            vm.NavigateToSystemPage(3);
            CaptureView(3, "04_device_nicknames_view.png");

            // 5. Hotkeys & System View
            vm.NavigateToSystemPage(4);
            CaptureView(4, "05_hotkeys_and_system_view.png");

            window.SetExplicitShutdown();
            window.Close();
        });
    }

    private static void SaveWindowToPng(Window window, string filePath)
    {
        int width = (int)window.Width;
        int height = (int)window.Height;
        if (width <= 0) width = 1240;
        if (height <= 0) height = 840;

        var targetElement = window.Content as FrameworkElement ?? window;
        targetElement.Measure(new Size(width, height));
        targetElement.Arrange(new Rect(0, 0, width, height));
        targetElement.UpdateLayout();

        var dpi = 96.0;
        var rtb = new RenderTargetBitmap(
            width,
            height,
            dpi,
            dpi,
            PixelFormats.Pbgra32);

        rtb.Render(targetElement);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
