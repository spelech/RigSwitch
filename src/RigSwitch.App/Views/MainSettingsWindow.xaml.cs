namespace RigSwitch.App.Views;

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RigSwitch.App.ViewModels;

/// <summary>
/// Interaction logic for MainSettingsWindow.xaml.
/// </summary>
public partial class MainSettingsWindow : Window
{
    private bool _isExplicitShutdown;

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
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
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
    /// <param name="viewName">The name of the view tab ('presets', 'displays', 'audio', 'nicknames', 'hotkeys').</param>
    /// <param name="requestedPath">The optional destination file path.</param>
    /// <returns>The absolute path of the saved PNG file.</returns>
    public async Task<string> CaptureScreenshotAsync(string? viewName, string? requestedPath = null)
    {
        return await Dispatcher.InvokeAsync(() =>
        {
            int tabIndex = (viewName?.ToLowerInvariant().Trim()) switch
            {
                "displays" => 1,
                "audio" => 2,
                "nicknames" => 3,
                "hotkeys" => 4,
                _ => 0
            };

            if (ContentTabControl != null)
            {
                ContentTabControl.SelectedIndex = tabIndex;
            }

            var effectiveView = (viewName?.ToLowerInvariant().Trim()) switch
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
            if (width <= 0) width = 960;
            if (height <= 0) height = 700;

            if (ActualWidth <= 0 || ActualHeight <= 0)
            {
                Measure(new Size(width, height));
                Arrange(new Rect(0, 0, width, height));
            }
            UpdateLayout();

            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(this);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using (var stream = File.Create(outPath))
            {
                encoder.Save(stream);
            }

            return Path.GetFullPath(outPath);
        });
    }

    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && int.TryParse(element.Tag?.ToString(), out int index) && ContentTabControl != null)
        {
            ContentTabControl.SelectedIndex = index;
        }
    }

    private void ContentTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == ContentTabControl)
        {
            switch (ContentTabControl.SelectedIndex)
            {
                case 0: if (NavPresets != null) NavPresets.IsChecked = true; break;
                case 1: if (NavDisplays != null) NavDisplays.IsChecked = true; break;
                case 2: if (NavAudio != null) NavAudio.IsChecked = true; break;
                case 3: if (NavNicknames != null) NavNicknames.IsChecked = true; break;
                case 4: if (NavHotkeys != null) NavHotkeys.IsChecked = true; break;
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
