namespace RigSwitch.App.Views;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
