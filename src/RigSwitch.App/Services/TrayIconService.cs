namespace RigSwitch.App.Services;

using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Manages the Windows taskbar system tray icon, context menu, and balloon notifications.
/// The <see cref="NotifyIcon"/> is created and owned on a dedicated WinForms STA pump thread
/// so that Windows delivers tray icon messages correctly.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private ISettingsStorageService _settingsStorage;
    private NotifyIcon? _notifyIcon;
    private Control? _invoker;
    private Action? _openSettingsAction;
    private bool _disposed;

    private readonly List<ToolStripMenuItem> _deskPresetMenuItems = [];
    private readonly List<ToolStripMenuItem> _rigPresetMenuItems = [];
    private ToolStripMenuItem? _deskMenuItem;
    private ToolStripMenuItem? _rigMenuItem;

    /// <summary>
    /// Gets or sets a value indicating whether balloon toast notifications are displayed.
    /// </summary>
    public bool ShowToastNotifications { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService"/> class.
    /// </summary>
    /// <param name="coordinator">The workstation profile switch coordinator.</param>
    /// <param name="settingsStorage">The settings storage service for retrieving presets.</param>
    public TrayIconService(IProfileSwitchCoordinator coordinator, ISettingsStorageService settingsStorage)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(settingsStorage);
        _coordinator = coordinator;
        _settingsStorage = settingsStorage;
    }

    /// <summary>
    /// Initializes and displays the system tray icon on a dedicated WinForms STA thread.
    /// </summary>
    /// <param name="openSettingsAction">Action invoked when settings are requested.</param>
    /// <param name="settingsStorage">Optional settings storage service to override or supply if not provided in constructor.</param>
    public void Initialize(Action openSettingsAction, ISettingsStorageService? settingsStorage = null)
    {
        ArgumentNullException.ThrowIfNull(openSettingsAction);
        if (settingsStorage != null)
        {
            _settingsStorage = settingsStorage;
        }
        _openSettingsAction = openSettingsAction;

        using var ready = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            // Hidden Control gives us a thread-affine Invoke handle for cross-thread marshalling.
            _invoker = new Control();
            _invoker.CreateControl();

            var contextMenu = new ContextMenuStrip();

            _deskMenuItem = new ToolStripMenuItem("🖥️ Switch to Desk Setup", null, (s, e) =>
            {
                _ = Task.Run(async () =>
                {
                    await _coordinator.SwitchProfileAsync(ProfileMode.Desk).ConfigureAwait(false);
                });
            });

            _rigMenuItem = new ToolStripMenuItem("🏎️ Switch to Sim Rig Setup", null, (s, e) =>
            {
                _ = Task.Run(async () =>
                {
                    await _coordinator.SwitchProfileAsync(ProfileMode.SimRig).ConfigureAwait(false);
                });
            });

            UserSettings settings;
            try
            {
                settings = _settingsStorage.LoadSettingsAsync().GetAwaiter().GetResult();
            }
            catch
            {
                settings = new UserSettings();
            }

            BuildPresetSubmenus(settings);

            var separator = new ToolStripSeparator();

            var settingsMenuItem = new ToolStripMenuItem("⚙️ Settings...", null, (s, e) =>
            {
                _openSettingsAction?.Invoke();
            });

            var exitMenuItem = new ToolStripMenuItem("❌ Exit RigSwitch", null, (s, e) =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            });

            contextMenu.Items.AddRange(
            [
                _deskMenuItem,
                _rigMenuItem,
                separator,
                settingsMenuItem,
                exitMenuItem
            ]);

            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            _notifyIcon.MouseClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _ = Task.Run(async () =>
                    {
                        var nextProfile = _coordinator.CurrentProfile == ProfileMode.Desk
                            ? ProfileMode.SimRig
                            : ProfileMode.Desk;
                        await _coordinator.SwitchProfileAsync(nextProfile).ConfigureAwait(false);
                    });
                }
            };

            ApplyTrayState(_coordinator.CurrentProfile, _coordinator.CurrentPreset);
            ready.Set();

            // Drive the WinForms message loop so the tray icon receives Windows messages.
            Application.Run();
        })
        {
            IsBackground = true,
            Name = "TrayIconPump"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        // Block until the icon and invoker are fully created on the STA thread.
        ready.Wait();
    }

    /// <summary>
    /// Rebuilds the preset submenu items to reflect updated preset names from user settings.
    /// Thread-safe — marshals to the tray icon's STA thread.
    /// </summary>
    /// <param name="settings">The updated user settings containing presets.</param>
    public void RefreshPresets(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (_invoker == null || _invoker.IsDisposed)
        {
            return;
        }

        if (_invoker.InvokeRequired)
        {
            _invoker.Invoke(() => BuildPresetSubmenus(settings));
            return;
        }

        BuildPresetSubmenus(settings);
    }

    private void BuildPresetSubmenus(UserSettings settings)
    {
        _deskPresetMenuItems.Clear();
        _deskMenuItem?.DropDownItems.Clear();
        for (int i = 0; i < settings.DeskPresets.Count; i++)
        {
            var preset = settings.DeskPresets[i];
            int presetIndex = i;
            var subItem = new ToolStripMenuItem(preset.Name, null, (s, e) =>
            {
                _ = Task.Run(async () =>
                {
                    await _coordinator.SwitchToPresetAsync(ProfileMode.Desk, presetIndex).ConfigureAwait(false);
                });
            })
            {
                Tag = preset.Id
            };
            _deskPresetMenuItems.Add(subItem);
            _deskMenuItem?.DropDownItems.Add(subItem);
        }

        _rigPresetMenuItems.Clear();
        _rigMenuItem?.DropDownItems.Clear();
        for (int i = 0; i < settings.RigPresets.Count; i++)
        {
            var preset = settings.RigPresets[i];
            int presetIndex = i;
            var subItem = new ToolStripMenuItem(preset.Name, null, (s, e) =>
            {
                _ = Task.Run(async () =>
                {
                    await _coordinator.SwitchToPresetAsync(ProfileMode.SimRig, presetIndex).ConfigureAwait(false);
                });
            })
            {
                Tag = preset.Id
            };
            _rigPresetMenuItems.Add(subItem);
            _rigMenuItem?.DropDownItems.Add(subItem);
        }
    }

    /// <summary>
    /// Updates the system tray icon image, tooltip, and submenu checkmarks to reflect the active profile and preset.
    /// Thread-safe — marshals to the tray icon's STA thread.
    /// </summary>
    /// <param name="mode">The active workstation profile mode.</param>
    /// <param name="preset">The active workstation preset, or null to determine from coordinator.</param>
    public void UpdateTrayState(ProfileMode mode, WorkstationPreset? preset = null)
    {
        if (_invoker == null || _notifyIcon == null)
        {
            return;
        }

        if (_invoker.InvokeRequired)
        {
            _invoker.Invoke(() => ApplyTrayState(mode, preset));
            return;
        }

        ApplyTrayState(mode, preset);
    }

    /// <summary>
    /// Displays a Windows system balloon tip notification if enabled in user settings.
    /// Thread-safe — marshals to the tray icon's STA thread.
    /// </summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification body text.</param>
    public void ShowNotification(string title, string message)
    {
        if (!ShowToastNotifications || _notifyIcon == null || _invoker == null)
        {
            return;
        }

        if (_invoker.InvokeRequired)
        {
            _invoker.Invoke(() => _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info));
            return;
        }

        _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_invoker != null && _notifyIcon != null)
        {
            _invoker.Invoke(() =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.ContextMenuStrip?.Dispose();
                _notifyIcon.Icon?.Dispose();
                _notifyIcon.Dispose();
                _notifyIcon = null;
                Application.ExitThread();
            });
        }

        _invoker?.Dispose();
        _invoker = null;
    }

    private void ApplyTrayState(ProfileMode mode, WorkstationPreset? preset = null)
    {
        if (_notifyIcon == null)
        {
            return;
        }

        var oldIcon = _notifyIcon.Icon;
        var newIcon = CreateProfileIcon(mode);

        _notifyIcon.Icon = newIcon;

        var activePreset = preset ?? _coordinator.CurrentPreset;
        var presetName = activePreset?.Name ?? "Default";
        var tooltip = mode == ProfileMode.Desk
            ? $"RigSwitch - Desk Setup ({presetName})"
            : $"RigSwitch - Sim Rig Setup ({presetName})";

        if (tooltip.Length > 63)
        {
            tooltip = tooltip[..63];
        }

        _notifyIcon.Text = tooltip;

        oldIcon?.Dispose();

        if (_deskMenuItem != null)
        {
            _deskMenuItem.Checked = mode == ProfileMode.Desk;
        }

        if (_rigMenuItem != null)
        {
            _rigMenuItem.Checked = mode == ProfileMode.SimRig;
        }

        int activeIndex = _coordinator.CurrentPresetIndex;
        if (preset != null)
        {
            var targetList = mode == ProfileMode.Desk ? _deskPresetMenuItems : _rigPresetMenuItems;
            for (int i = 0; i < targetList.Count; i++)
            {
                if (targetList[i].Tag is string id && string.Equals(id, preset.Id, StringComparison.OrdinalIgnoreCase))
                {
                    activeIndex = i;
                    targetList[i].Text = preset.Name;
                    break;
                }

                if (string.Equals(targetList[i].Text, preset.Name, StringComparison.Ordinal))
                {
                    activeIndex = i;
                    break;
                }
            }
        }

        for (int i = 0; i < _deskPresetMenuItems.Count; i++)
        {
            _deskPresetMenuItems[i].Checked = mode == ProfileMode.Desk && i == activeIndex;
        }

        for (int i = 0; i < _rigPresetMenuItems.Count; i++)
        {
            _rigPresetMenuItems[i].Checked = mode == ProfileMode.SimRig && i == activeIndex;
        }
    }

    private static System.Drawing.Icon CreateProfileIcon(ProfileMode mode)
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            if (mode == ProfileMode.Desk)
            {
                using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(10, 10, 14));
                using var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 200, 83), 1.5f);
                g.FillEllipse(bgBrush, 1, 1, 30, 30);
                g.DrawEllipse(borderPen, 1, 1, 30, 30);

                using var greenPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 200, 83), 2f);
                // Ultrawide curved monitor screen
                g.DrawArc(greenPen, 6, 7, 20, 13, 10, 160);
                g.DrawArc(greenPen, 6, 17, 20, 4, 10, 160);
                g.DrawLine(greenPen, 7, 9, 7, 18);
                g.DrawLine(greenPen, 25, 9, 25, 18);
                // Stand
                g.DrawLine(greenPen, 16, 19, 16, 23);
                g.DrawLine(greenPen, 11, 23, 21, 23);
            }
            else
            {
                using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(10, 10, 14));
                using var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 85, 0), 1.5f);
                g.FillEllipse(bgBrush, 1, 1, 30, 30);
                g.DrawEllipse(borderPen, 1, 1, 30, 30);

                using var orangePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 85, 0), 2f);
                using var orangeBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 85, 0));
                // F1 / GT3 butterfly yoke grips (left and right sculpted handles)
                g.DrawArc(orangePen, 6, 8, 8, 16, 90, 180);
                g.DrawArc(orangePen, 18, 8, 8, 16, 270, 180);
                // Crossbar and center hub
                g.DrawLine(orangePen, 10, 16, 22, 16);
                g.FillEllipse(orangeBrush, 14, 14, 4, 4);
                // Top shift indicator dots
                g.FillRectangle(orangeBrush, 11, 11, 2, 2);
                g.FillRectangle(orangeBrush, 19, 11, 2, 2);
            }
        }

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using var temp = System.Drawing.Icon.FromHandle(hIcon);
            return (System.Drawing.Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
