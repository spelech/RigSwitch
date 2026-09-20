namespace RigSwitch.Infrastructure.Windows.Hotkeys;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

/// <summary>
/// Provides a Win32 message-only window hosted on a dedicated STA thread to process global hotkey events.
/// </summary>
public sealed class WindowsNativeHotkeyProvider : INativeHotkeyProvider
{
    private const uint WM_REGISTER_HOTKEY = NativeHotkeyApi.WM_USER + 1;
    private const uint WM_UNREGISTER_HOTKEY = NativeHotkeyApi.WM_USER + 2;

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new(false);
    private readonly ConcurrentDictionary<int, byte> _registeredIds = new();

    private IntPtr _hwnd = IntPtr.Zero;
    private string? _className;
    private NativeHotkeyApi.WndProcDelegate? _wndProc; // Prevent GC from collecting delegate
    private bool _disposed;

    /// <inheritdoc/>
    public event Action<int>? HotkeyPressed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsNativeHotkeyProvider"/> class.
    /// Spawns an STA background thread to process Windows messages.
    /// </summary>
    public WindowsNativeHotkeyProvider()
    {
        _thread = new Thread(ThreadProc)
        {
            IsBackground = true,
            Name = "RigSwitch_HotkeyMessageLoop"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        _started.Wait();
    }

    /// <inheritdoc/>
    public bool RegisterHotKey(int id, uint modifiers, uint vk)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        uint packed = (modifiers & 0xFFFF) | ((vk & 0xFFFF) << 16);
        IntPtr result = NativeHotkeyApi.SendMessageW(_hwnd, WM_REGISTER_HOTKEY, (IntPtr)id, (IntPtr)packed);
        bool success = result == (IntPtr)1;

        if (success)
        {
            _registeredIds.TryAdd(id, 0);
        }

        return success;
    }

    /// <inheritdoc/>
    public bool UnregisterHotKey(int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        IntPtr result = NativeHotkeyApi.SendMessageW(_hwnd, WM_UNREGISTER_HOTKEY, (IntPtr)id, IntPtr.Zero);
        _registeredIds.TryRemove(id, out _);
        return result == (IntPtr)1;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hwnd != IntPtr.Zero)
        {
            foreach (int id in _registeredIds.Keys)
            {
                NativeHotkeyApi.SendMessageW(_hwnd, WM_UNREGISTER_HOTKEY, (IntPtr)id, IntPtr.Zero);
            }

            _registeredIds.Clear();
            NativeHotkeyApi.PostMessageW(_hwnd, NativeHotkeyApi.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        if (_thread != Thread.CurrentThread && _thread.IsAlive)
        {
            _thread.Join(2000);
        }

        _started.Dispose();
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_REGISTER_HOTKEY:
            {
                int id = wParam.ToInt32();
                uint packed = (uint)lParam.ToInt64();
                uint modifiers = packed & 0xFFFF;
                uint vk = (packed >> 16) & 0xFFFF;
                bool success = NativeHotkeyApi.RegisterHotKey(hWnd, id, modifiers, vk);
                return success ? (IntPtr)1 : IntPtr.Zero;
            }

            case WM_UNREGISTER_HOTKEY:
            {
                int id = wParam.ToInt32();
                bool success = NativeHotkeyApi.UnregisterHotKey(hWnd, id);
                return success ? (IntPtr)1 : IntPtr.Zero;
            }

            case NativeHotkeyApi.WM_HOTKEY:
            {
                int id = wParam.ToInt32();
                try
                {
                    HotkeyPressed?.Invoke(id);
                }
                catch
                {
                    // Do not allow subscriber exceptions to crash the message loop
                }

                return IntPtr.Zero;
            }

            case NativeHotkeyApi.WM_CLOSE:
            {
                NativeHotkeyApi.DestroyWindow(hWnd);
                return IntPtr.Zero;
            }

            case NativeHotkeyApi.WM_NCDESTROY:
            {
                NativeHotkeyApi.PostQuitMessage(0);
                return IntPtr.Zero;
            }

            default:
                return NativeHotkeyApi.DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }

    private void ThreadProc()
    {
        IntPtr hInstance = NativeHotkeyApi.GetModuleHandleW(null);
        _className = $"RigSwitch_HotkeyHost_{Guid.NewGuid():N}";
        _wndProc = WndProc;

        var wc = new NativeHotkeyApi.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeHotkeyApi.WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = hInstance,
            lpszClassName = _className
        };

        NativeHotkeyApi.RegisterClassExW(ref wc);

        _hwnd = NativeHotkeyApi.CreateWindowExW(
            0,
            _className,
            "RigSwitchHotkeyWindow",
            0,
            0,
            0,
            0,
            0,
            NativeHotkeyApi.HWND_MESSAGE,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        _started.Set();

        while (NativeHotkeyApi.GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeHotkeyApi.TranslateMessage(ref msg);
            NativeHotkeyApi.DispatchMessageW(ref msg);
        }

        if (_className != null)
        {
            NativeHotkeyApi.UnregisterClassW(_className, hInstance);
        }
    }
}
