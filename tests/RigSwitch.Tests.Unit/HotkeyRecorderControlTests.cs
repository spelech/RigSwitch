namespace RigSwitch.Tests.Unit;

using System.Windows.Input;
using RigSwitch.App.Controls;
using Xunit;

public sealed class HotkeyRecorderControlTests
{
    [Fact]
    public void Constructor_DefaultProperties_InitializedCorrectly()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl();

            Assert.Equal(string.Empty, control.Hotkey);
            Assert.False(control.IsRecording);
            Assert.Equal("Click to Record", control.Watermark);
            Assert.Equal("Click to Record", control.CurrentDisplayText);
        });
    }

    [Fact]
    public void HotkeyProperty_WhenSet_UpdatesDisplayText()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl { Hotkey = "Ctrl+Alt+S" };

            Assert.Equal("Ctrl+Alt+S", control.Hotkey);
            Assert.Equal("Ctrl+Alt+S", control.CurrentDisplayText);
        });
    }

    [Fact]
    public void WatermarkProperty_WhenSetAndHotkeyEmpty_UpdatesDisplayText()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl { Watermark = "None (Optional)" };

            Assert.Equal("None (Optional)", control.CurrentDisplayText);
        });
    }

    [Fact]
    public void StartRecording_SetsIsRecordingTrue_AndShowsRecordingText()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl { Hotkey = "Ctrl+Alt+S" };

            control.StartRecording();

            Assert.True(control.IsRecording);
            Assert.Equal("Press keys...", control.CurrentDisplayText);
        });
    }

    [Fact]
    public void StopRecording_SetsIsRecordingFalse_AndRestoresHotkeyText()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl { Hotkey = "Ctrl+Alt+S" };
            control.StartRecording();

            control.StopRecording();

            Assert.False(control.IsRecording);
            Assert.Equal("Ctrl+Alt+S", control.CurrentDisplayText);
        });
    }

    [Fact]
    public void HandleRecordedKey_Escape_CancelsRecordingWithoutChangingHotkey()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl { Hotkey = "Ctrl+Alt+S" };
            control.StartRecording();

            bool handled = control.HandleRecordedKey(Key.Escape, ModifierKeys.None);

            Assert.True(handled);
            Assert.False(control.IsRecording);
            Assert.Equal("Ctrl+Alt+S", control.Hotkey);
            Assert.Equal("Ctrl+Alt+S", control.CurrentDisplayText);
        });
    }

    [Theory]
    [InlineData(Key.Back)]
    [InlineData(Key.Delete)]
    public void HandleRecordedKey_BackOrDelete_ClearsHotkeyAndExitsRecording(Key key)
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl { Hotkey = "Ctrl+Alt+S" };
            control.StartRecording();

            bool handled = control.HandleRecordedKey(key, ModifierKeys.None);

            Assert.True(handled);
            Assert.False(control.IsRecording);
            Assert.Equal(string.Empty, control.Hotkey);
            Assert.Equal("Click to Record", control.CurrentDisplayText);
        });
    }

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LWin)]
    [InlineData(Key.RWin)]
    public void HandleRecordedKey_LoneModifierKey_IgnoredAndRemainsRecording(Key key)
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl();
            control.StartRecording();

            bool handled = control.HandleRecordedKey(key, ModifierKeys.Control);

            Assert.True(handled);
            Assert.True(control.IsRecording);
            Assert.Equal(string.Empty, control.Hotkey);
            Assert.Equal("Press keys...", control.CurrentDisplayText);
        });
    }

    [Fact]
    public void HandleRecordedKey_NonModifierKeyWithoutModifiers_IgnoredAndRemainsRecording()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl();
            control.StartRecording();

            bool handled = control.HandleRecordedKey(Key.S, ModifierKeys.None);

            Assert.True(handled);
            Assert.True(control.IsRecording);
            Assert.Equal(string.Empty, control.Hotkey);
            Assert.Equal("Press keys...", control.CurrentDisplayText);
        });
    }

    [Fact]
    public void HandleRecordedKey_ValidModifierAndKey_SetsHotkeyAndExitsRecording()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl();
            control.StartRecording();

            bool handled = control.HandleRecordedKey(Key.S, ModifierKeys.Control | ModifierKeys.Alt);

            Assert.True(handled);
            Assert.False(control.IsRecording);
            Assert.Equal("Ctrl+Alt+S", control.Hotkey);
            Assert.Equal("Ctrl+Alt+S", control.CurrentDisplayText);
        });
    }

    [Fact]
    public void HandleRecordedKey_FunctionKeyWithAlt_SetsHotkey()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl();
            control.StartRecording();

            bool handled = control.HandleRecordedKey(Key.F12, ModifierKeys.Alt);

            Assert.True(handled);
            Assert.False(control.IsRecording);
            Assert.Equal("Alt+F12", control.Hotkey);
            Assert.Equal("Alt+F12", control.CurrentDisplayText);
        });
    }

    [Fact]
    public void HandleRecordedKey_WinKeyCombination_SetsHotkey()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl();
            control.StartRecording();

            bool handled = control.HandleRecordedKey(Key.D, ModifierKeys.None, winPressed: true);

            Assert.True(handled);
            Assert.False(control.IsRecording);
            Assert.Equal("Win+D", control.Hotkey);
        });
    }

    [Fact]
    public void HandleRecordedKey_WhenNotRecording_ReturnsFalse()
    {
        RunInSta(() =>
        {
            var control = new HotkeyRecorderControl { Hotkey = "Ctrl+Alt+S" };

            bool handled = control.HandleRecordedKey(Key.A, ModifierKeys.Control);

            Assert.False(handled);
            Assert.Equal("Ctrl+Alt+S", control.Hotkey);
        });
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
