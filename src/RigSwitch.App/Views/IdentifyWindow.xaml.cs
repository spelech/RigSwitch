namespace RigSwitch.App.Views;

using System.Windows;
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

        Left = left;
        Top = top;
        Width = width;
        Height = height;

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
