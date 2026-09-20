namespace RigSwitch.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RigSwitch.Core.Services;

/// <summary>
/// An interactive control for recording keyboard hotkey combinations.
/// </summary>
public partial class HotkeyRecorderControl : UserControl
{
    private static readonly SolidColorBrush AccentBrush = CreateFrozenBrush(0x00, 0x7A, 0xCC);
    private static readonly SolidColorBrush NormalBorderBrush = CreateFrozenBrush(0x3E, 0x3E, 0x42);
    private static readonly SolidColorBrush NormalTextBrush = CreateFrozenBrush(0xEC, 0xEC, 0xEC);
    private static readonly SolidColorBrush MutedTextBrush = CreateFrozenBrush(0x8E, 0x8E, 0x93);

    /// <summary>
    /// Identifies the <see cref="Hotkey"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(string),
        typeof(HotkeyRecorderControl),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnHotkeyChanged));

    private static readonly DependencyPropertyKey IsRecordingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsRecording),
        typeof(bool),
        typeof(HotkeyRecorderControl),
        new PropertyMetadata(false, OnIsRecordingChanged));

    /// <summary>
    /// Identifies the <see cref="IsRecording"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsRecordingProperty = IsRecordingPropertyKey.DependencyProperty;

    /// <summary>
    /// Identifies the <see cref="Watermark"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register(
        nameof(Watermark),
        typeof(string),
        typeof(HotkeyRecorderControl),
        new PropertyMetadata("Click to Record", OnWatermarkChanged));

    /// <summary>
    /// Gets or sets the canonical hotkey string.
    /// </summary>
    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the control is actively capturing hotkey keystrokes.
    /// </summary>
    public bool IsRecording
    {
        get => (bool)GetValue(IsRecordingProperty);
        private set => SetValue(IsRecordingPropertyKey, value);
    }

    /// <summary>
    /// Gets or sets the watermark text displayed when no hotkey is set.
    /// </summary>
    public string Watermark
    {
        get => (string)GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    /// <summary>
    /// Gets the current text shown in the display block.
    /// </summary>
    public string CurrentDisplayText => DisplayTextBlock?.Text ?? string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="HotkeyRecorderControl"/> class.
    /// </summary>
    public HotkeyRecorderControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisualState();
        UpdateVisualState();
    }

    /// <summary>
    /// Activates recording mode and directs keyboard focus to this control.
    /// </summary>
    public void StartRecording()
    {
        IsRecording = true;
        Focus();
    }

    /// <summary>
    /// Deactivates recording mode without modifying the hotkey.
    /// </summary>
    public void StopRecording()
    {
        IsRecording = false;
    }

    /// <summary>
    /// Updates the visual display based on recording state, assigned hotkey, and watermark.
    /// </summary>
    public void UpdateVisualState()
    {
        if (DisplayBorder == null || DisplayTextBlock == null)
        {
            return;
        }

        if (IsRecording)
        {
            DisplayBorder.BorderBrush = AccentBrush;
            DisplayTextBlock.Text = "Press keys...";
            DisplayTextBlock.Foreground = AccentBrush;
        }
        else if (!string.IsNullOrWhiteSpace(Hotkey))
        {
            DisplayBorder.BorderBrush = NormalBorderBrush;
            DisplayTextBlock.Text = Hotkey;
            DisplayTextBlock.Foreground = NormalTextBrush;
        }
        else
        {
            DisplayBorder.BorderBrush = NormalBorderBrush;
            DisplayTextBlock.Text = string.IsNullOrEmpty(Watermark) ? "Click to Record" : Watermark;
            DisplayTextBlock.Foreground = MutedTextBrush;
        }
    }

    /// <summary>
    /// Evaluates a key press against the recording state machine.
    /// </summary>
    /// <param name="key">The key pressed.</param>
    /// <param name="modifiers">The active modifier keys.</param>
    /// <param name="winPressed">Optional flag indicating if the Windows key is pressed.</param>
    /// <returns><c>true</c> if the key event was consumed; otherwise, <c>false</c>.</returns>
    public bool HandleRecordedKey(Key key, ModifierKeys modifiers, bool winPressed = false)
    {
        if (!IsRecording)
        {
            return false;
        }

        if (key == Key.Escape)
        {
            IsRecording = false;
            return true;
        }

        if (key is Key.Back or Key.Delete)
        {
            Hotkey = string.Empty;
            IsRecording = false;
            return true;
        }

        if (IsLoneModifierKey(key) || key == Key.None)
        {
            return true;
        }

        bool ctrl = (modifiers & ModifierKeys.Control) != 0;
        bool alt = (modifiers & ModifierKeys.Alt) != 0;
        bool shift = (modifiers & ModifierKeys.Shift) != 0;
        bool win = winPressed || (modifiers & ModifierKeys.Windows) != 0;

        if (ctrl || alt || shift || win)
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            string? formatted = HotkeyGestureParser.FormatHotkey(ctrl, alt, shift, win, vk);
            if (formatted != null)
            {
                Hotkey = formatted;
                IsRecording = false;
                return true;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (!IsRecording)
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                StartRecording();
                e.Handled = true;
            }

            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : (e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key);
        bool win = (Keyboard.Modifiers & ModifierKeys.Windows) != 0
            || Keyboard.IsKeyDown(Key.LWin)
            || Keyboard.IsKeyDown(Key.RWin);

        e.Handled = HandleRecordedKey(key, Keyboard.Modifiers, win);
    }

    /// <inheritdoc/>
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        if (IsRecording)
        {
            IsRecording = false;
        }
    }

    private void OnRecordBorderMouseDown(object sender, MouseButtonEventArgs e)
    {
        StartRecording();
        e.Handled = true;
    }

    private void OnClearButtonClick(object sender, RoutedEventArgs e)
    {
        Hotkey = string.Empty;
        IsRecording = false;
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyRecorderControl control)
        {
            control.UpdateVisualState();
        }
    }

    private static void OnIsRecordingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyRecorderControl control)
        {
            control.UpdateVisualState();
        }
    }

    private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyRecorderControl control)
        {
            control.UpdateVisualState();
        }
    }

    private static bool IsLoneModifierKey(Key key)
    {
        return key is Key.LeftCtrl
            or Key.RightCtrl
            or Key.LeftAlt
            or Key.RightAlt
            or Key.LeftShift
            or Key.RightShift
            or Key.LWin
            or Key.RWin;
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
