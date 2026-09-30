namespace RigSwitch.App.Views;

using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

/// <summary>
/// Window overlay displaying the Windows monitor identification number over a display screen.
/// </summary>
public partial class IdentifyWindow : Window
{
    private readonly DispatcherTimer _timer;

    public IdentifyWindow(int number, string name, double left, double top, double width, double height)
    {
        InitializeComponent();

        NumberText.Text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        NameText.Text = name;

        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        Left = left / dpiX;
        Top = top / dpiY;
        Width = width / dpiX;
        Height = height / dpiY;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _timer.Tick += (s, e) =>
        {
            _timer.Stop();
            Close();
        };
        _timer.Start();
    }
}
