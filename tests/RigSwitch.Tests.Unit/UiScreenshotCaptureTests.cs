namespace RigSwitch.Tests.Unit;

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NSubstitute;
using RigSwitch.App.ViewModels;
using RigSwitch.App.Views;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using Xunit;

public sealed class UiScreenshotCaptureTests
{
    [Fact]
    public void CaptureUiScreenshots()
    {
        RunInSta(() =>
        {
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
                RigMonitorId = "MON2",
                RigPrimaryAudioId = "AUD2"
            };
            userSettings.DeskPresets.Add(new WorkstationPreset
            {
                Name = "Workstation Dual Ultrawide",
                TargetMonitorId = "MON1",
                PrimaryAudioId = "AUD1",
                FallbackAudioId = "AUD3"
            });
            userSettings.RigPresets.Add(new WorkstationPreset
            {
                Name = "GT3 Triple Screen Rig",
                TargetMonitorId = "MON2",
                PrimaryAudioId = "AUD2",
                FallbackAudioId = "AUD1"
            });

            settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(userSettings);
            displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>()).Returns([
                new DisplayDeviceInfo("MON1", @"\\.\DISPLAY1", "ASUS ROG PG34WCDM", "NVIDIA RTX 4090", true, true),
                new DisplayDeviceInfo("MON2", @"\\.\DISPLAY2", "Samsung Odyssey G9", "NVIDIA RTX 4090", false, true)
            ]);
            audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns([
                new AudioEndpointInfo("AUD1", "Creative Pebble V3 Speakers", "Realtek USB Audio", DevicePresenceState.Active, true, false),
                new AudioEndpointInfo("AUD2", "Audeze Maxwell Wireless Gaming Headset", "Audeze USB Audio", DevicePresenceState.Active, false, true),
                new AudioEndpointInfo("AUD3", "SteelSeries Sonar - Virtual Gaming", "SteelSeries Audio", DevicePresenceState.Active, false, false),
                new AudioEndpointInfo("AUD4", "Oculus Virtual Audio Device", "Oculus Virtual Audio", DevicePresenceState.Disabled, false, false),
                new AudioEndpointInfo("AUD5", "Realtek Digital Output (Optical)", "Realtek Audio", DevicePresenceState.Unplugged, false, false)
            ]);

            var vm = new MainSettingsViewModel(
                coordinator,
                settingsService,
                displayService,
                audioDirector,
                hotkeyService,
                trayIconService);

            var window = new MainSettingsWindow(vm)
            {
                Width = 800,
                Height = 640
            };

            var outDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "screenshots");
            System.IO.Directory.CreateDirectory(outDir);

            window.Show();
            window.Measure(new Size(800, 640));
            window.Arrange(new Rect(0, 0, 800, 640));
            window.UpdateLayout();
            SaveWindowToPng(window, System.IO.Path.Combine(outDir, "profiles_tab.png"));

            // 2. Switch to Device Nicknames Tab (index 1)
            var tabControl = FindVisualChild<TabControl>(window);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 1;
                window.UpdateLayout();
                SaveWindowToPng(window, System.IO.Path.Combine(outDir, "nicknames_tab.png"));

                // 3. Switch to Audio Cleanup Tab (index 2)
                tabControl.SelectedIndex = 2;
                window.UpdateLayout();
                SaveWindowToPng(window, System.IO.Path.Combine(outDir, "audio_cleanup_tab.png"));
            }

            window.SetExplicitShutdown();
            window.Close();
        });
    }

    private static void SaveWindowToPng(Window window, string filePath)
    {
        int width = (int)window.ActualWidth;
        int height = (int)window.ActualHeight;
        if (width <= 0) width = (int)window.Width;
        if (height <= 0) height = (int)window.Height;
        if (width <= 0) width = 800;
        if (height <= 0) height = 640;

        var dpi = 96.0;
        var rtb = new RenderTargetBitmap(
            width,
            height,
            dpi, dpi,
            PixelFormats.Pbgra32);

        rtb.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var stream = System.IO.File.Create(filePath);
        encoder.Save(stream);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
            {
                return descendant;
            }
        }
        return null;
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
