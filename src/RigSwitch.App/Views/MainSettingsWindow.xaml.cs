namespace RigSwitch.App.Views;

using System.ComponentModel;
using System.Windows;
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
}
