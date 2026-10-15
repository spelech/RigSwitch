namespace RigSwitch.App.Views;

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RigSwitch.App.ViewModels;

/// <summary>
/// Interaction logic for MainSettingsWindow.xaml.
/// </summary>
public partial class MainSettingsWindow : Window
{
    private bool _isExplicitShutdown;
    private bool _isDraggingStageMonitor;
    private Point _dragStartPoint;
    private int _dragStartPosX;
    private int _dragStartPosY;
    private PresetMonitorCanvasItemViewModel? _draggedMonitorItem;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainSettingsWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model driving settings and device control.</param>
    public MainSettingsWindow(MainSettingsViewModel viewModel)
    {
        EnsureThemeResources();
        InitializeComponent();
        DataContext = viewModel;

        try
        {
            if (Environment.ProcessPath != null)
            {
                using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                if (sysIcon != null)
                {
                    Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        sysIcon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
        }
        catch
        {
            // Fallback gracefully without throwing
        }

        Loaded += async (sender, args) =>
        {
            await viewModel.LoadAsync();
        };
    }

    /// <summary>
    /// Marks the application as undergoing explicit shutdown so the window close is not intercepted.
    /// </summary>
    public void SetExplicitShutdown()
    {
        _isExplicitShutdown = true;
    }

    /// <inheritdoc/>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitShutdown)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// Captures a screenshot of the specified settings view tab and writes it to disk as a PNG.
    /// </summary>
    /// <param name="viewName">The name of the view tab ('presets', 'desk-1', 'rig-1', 'displays', 'audio', 'nicknames', 'hotkeys').</param>
    /// <param name="requestedPath">The optional destination file path.</param>
    /// <returns>The absolute path of the saved PNG file.</returns>
    public async Task<string> CaptureScreenshotAsync(string? viewName, string? requestedPath = null)
    {
        return await Dispatcher.InvokeAsync(() =>
        {
            string normalized = viewName?.ToLowerInvariant().Trim() ?? "presets";
            int tabIndex = normalized switch
            {
                "displays" => 1,
                "audio" => 2,
                "nicknames" => 3,
                "hotkeys" => 4,
                _ => 0
            };

            if (DataContext is MainSettingsViewModel vm)
            {
                if (normalized.StartsWith("rig", StringComparison.Ordinal) && vm.RigPresets.Count > 0)
                {
                    int idx = normalized.EndsWith('2') && vm.RigPresets.Count > 1 ? 1
                        : normalized.EndsWith('3') && vm.RigPresets.Count > 2 ? 2 : 0;
                    vm.SelectPresetForNavigation(vm.RigPresets[idx]);
                }
                else if (tabIndex <= 1 && vm.DeskPresets.Count > 0)
                {
                    int idx = normalized.EndsWith('2') && vm.DeskPresets.Count > 1 ? 1
                        : normalized.EndsWith('3') && vm.DeskPresets.Count > 2 ? 2 : 0;
                    vm.SelectPresetForNavigation(vm.DeskPresets[idx]);
                }
                else if (tabIndex >= 2)
                {
                    vm.NavigateToSystemPage(tabIndex);
                }
            }

            if (ContentTabControl != null)
            {
                ContentTabControl.SelectedIndex = tabIndex;
            }

            var effectiveView = normalized switch
            {
                "displays" => "displays",
                "audio" => "audio",
                "nicknames" => "nicknames",
                "hotkeys" => "hotkeys",
                _ => "presets"
            };

            string outPath = !string.IsNullOrWhiteSpace(requestedPath)
                ? requestedPath
                : Path.Combine(Path.GetTempPath(), $"rigswitch-{effectiveView}.png");

            var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            int width = (int)ActualWidth;
            int height = (int)ActualHeight;
            if (width <= 0) width = (int)Width;
            if (height <= 0) height = (int)Height;
            if (width <= 0) width = 1240;
            if (height <= 0) height = 840;

            var targetElement = Content as FrameworkElement ?? this;
            targetElement.Measure(new Size(width, height));
            targetElement.Arrange(new Rect(0, 0, width, height));
            targetElement.UpdateLayout();

            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(targetElement);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using (var stream = File.Create(outPath))
            {
                encoder.Save(stream);
            }

            return Path.GetFullPath(outPath);
        });
    }

    private void StageMonitor_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is PresetMonitorCanvasItemViewModel item)
        {
            _isDraggingStageMonitor = true;
            _draggedMonitorItem = item;
            _dragStartPoint = e.GetPosition(this);
            _dragStartPosX = item.PositionX;
            _dragStartPosY = item.PositionY;
            element.CaptureMouse();
            e.Handled = true;
        }
    }

    private void StageMonitor_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingStageMonitor || _draggedMonitorItem == null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _isDraggingStageMonitor = false;
            _draggedMonitorItem = null;
            return;
        }

        var currentPos = e.GetPosition(this);
        double deltaCanvasX = currentPos.X - _dragStartPoint.X;
        double deltaCanvasY = currentPos.Y - _dragStartPoint.Y;

        // Convert canvas pixel delta (~0.052 scale) to desktop coordinate delta
        int targetDesktopX = _dragStartPosX + (int)Math.Round(deltaCanvasX / 0.052);
        int targetDesktopY = _dragStartPosY + (int)Math.Round(deltaCanvasY / 0.052);

        if (DataContext is MainSettingsViewModel vm && vm.SelectedPreset != null)
        {
            vm.SelectedPreset.MoveMonitorOnStage(_draggedMonitorItem, targetDesktopX, targetDesktopY);
        }
    }

    private void StageMonitor_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingStageMonitor && sender is FrameworkElement element)
        {
            _isDraggingStageMonitor = false;
            _draggedMonitorItem = null;
            element.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && int.TryParse(element.Tag?.ToString(), out int index))
        {
            if (DataContext is MainSettingsViewModel vm)
            {
                if (index >= 2)
                {
                    vm.NavigateToSystemPage(index);
                }
                else
                {
                    vm.SelectedPageIndex = index;
                }
            }

            if (ContentTabControl != null && ContentTabControl.SelectedIndex != index)
            {
                ContentTabControl.SelectedIndex = index;
            }
        }
    }

    private void ContentTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == ContentTabControl)
        {
            if (DataContext is MainSettingsViewModel vm && vm.SelectedPageIndex != ContentTabControl.SelectedIndex)
            {
                vm.SelectedPageIndex = ContentTabControl.SelectedIndex;
            }

            switch (ContentTabControl.SelectedIndex)
            {
                case 0:
                case 1:
                    if (NavAudio != null) NavAudio.IsChecked = false;
                    if (NavNicknames != null) NavNicknames.IsChecked = false;
                    if (NavHotkeys != null) NavHotkeys.IsChecked = false;
                    break;
                case 2:
                    if (NavAudio != null) NavAudio.IsChecked = true;
                    break;
                case 3:
                    if (NavNicknames != null) NavNicknames.IsChecked = true;
                    break;
                case 4:
                    if (NavHotkeys != null) NavHotkeys.IsChecked = true;
                    break;
            }
        }
    }

    private static void EnsureThemeResources()
    {
        try
        {
            if (Application.Current == null)
            {
                _ = new Application();
            }

            if (Application.Current?.Resources != null)
            {
                bool hasTheme = false;
                foreach (var dict in Application.Current.Resources.MergedDictionaries)
                {
                    if (dict.Source != null && dict.Source.OriginalString.Contains("FluentTheme.xaml", StringComparison.OrdinalIgnoreCase))
                    {
                        hasTheme = true;
                        break;
                    }
                }

                if (!hasTheme)
                {
                    Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/RigSwitch.App;component/Styles/FluentTheme.xaml", UriKind.Absolute)
                    });
                }
            }
        }
        catch
        {
            // Fallback gracefully in designer or specialized test contexts
        }
    }
}
