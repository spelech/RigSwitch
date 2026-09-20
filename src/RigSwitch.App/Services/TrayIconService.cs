namespace RigSwitch.App.Services;

using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;

/// <summary>
/// Manages the Windows taskbar system tray icon, context menu, and balloon notifications.
/// The <see cref="NotifyIcon"/> is created and owned on a dedicated WinForms STA pump thread
/// so that Windows delivers tray icon messages correctly.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private NotifyIcon? _notifyIcon;
    private Control? _invoker;
    private Action? _openSettingsAction;
    private bool _disposed;

    /// <summary>
    /// Gets or sets a value indicating whether balloon toast notifications are displayed.
    /// </summary>
    public bool ShowToastNotifications { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService"/> class.
    /// </summary>
    /// <param name="coordinator">The workstation profile switch coordinator.</param>
    public TrayIconService(IProfileSwitchCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        _coordinator = coordinator;
    }

    /// <summary>
    /// Initializes and displays the system tray icon on a dedicated WinForms STA thread.
    /// </summary>
    /// <param name="openSettingsAction">Action invoked when settings are requested.</param>
    public void Initialize(Action openSettingsAction)
    {
        ArgumentNullException.ThrowIfNull(openSettingsAction);
        _openSettingsAction = openSettingsAction;

        using var ready = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            // Hidden Control gives us a thread-affine Invoke handle for cross-thread marshalling.
            _invoker = new Control();
            _invoker.CreateControl();

            var contextMenu = new ContextMenuStrip();

            var deskMenuItem = new ToolStripMenuItem("🖥️ Switch to Desk Setup", null, (s, e) =>
            {
                _ = Task.Run(async () =>
                {
                    await _coordinator.SwitchProfileAsync(ProfileMode.Desk).ConfigureAwait(false);
                });
            });

            var rigMenuItem = new ToolStripMenuItem("🏎️ Switch to Sim Rig Setup", null, (s, e) =>
            {
                _ = Task.Run(async () =>
                {
                    await _coordinator.SwitchProfileAsync(ProfileMode.SimRig).ConfigureAwait(false);
                });
            });

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
                deskMenuItem,
                rigMenuItem,
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

            ApplyTrayState(_coordinator.CurrentProfile);
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
    /// Updates the system tray icon image and tooltip to reflect the active profile.
    /// Thread-safe — marshals to the tray icon's STA thread.
    /// </summary>
    /// <param name="mode">The active workstation profile mode.</param>
    public void UpdateTrayState(ProfileMode mode)
    {
        if (_invoker == null || _notifyIcon == null)
        {
            return;
        }

        if (_invoker.InvokeRequired)
        {
            _invoker.Invoke(() => ApplyTrayState(mode));
            return;
        }

        ApplyTrayState(mode);
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

    private void ApplyTrayState(ProfileMode mode)
    {
        if (_notifyIcon == null)
        {
            return;
        }

        var oldIcon = _notifyIcon.Icon;
        var newIcon = CreateProfileIcon(mode);

        _notifyIcon.Icon = newIcon;
        _notifyIcon.Text = mode == ProfileMode.Desk
            ? "RigSwitch - Desk Setup"
            : "RigSwitch - Sim Rig Setup";

        oldIcon?.Dispose();
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
                using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 122, 204));
                g.FillEllipse(bgBrush, 1, 1, 30, 30);

                using var whitePen = new System.Drawing.Pen(System.Drawing.Color.White, 2f);
                g.DrawRectangle(whitePen, 7, 7, 18, 12);
                g.DrawLine(whitePen, 16, 19, 16, 23);
                g.DrawLine(whitePen, 11, 23, 21, 23);
            }
            else
            {
                using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(232, 78, 27));
                g.FillEllipse(bgBrush, 1, 1, 30, 30);

                using var whitePen = new System.Drawing.Pen(System.Drawing.Color.White, 2f);
                using var whiteBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                g.DrawEllipse(whitePen, 6, 6, 20, 20);
                g.FillEllipse(whiteBrush, 14, 14, 4, 4);
                g.DrawLine(whitePen, 6, 16, 14, 16);
                g.DrawLine(whitePen, 18, 16, 26, 16);
                g.DrawLine(whitePen, 16, 18, 16, 26);
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
