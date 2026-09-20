namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.Infrastructure.Windows.Hotkeys;
using Xunit;

public sealed class WindowsGlobalHotkeyServiceTests
{
    private readonly INativeHotkeyProvider _mockProvider = Substitute.For<INativeHotkeyProvider>();

    [Theory]
    [InlineData("Ctrl+Alt+S", (uint)(HotkeyModifiers.Control | HotkeyModifiers.Alt), 0x53u)]
    [InlineData("Ctrl+Alt+D", (uint)(HotkeyModifiers.Control | HotkeyModifiers.Alt), 0x44u)]
    [InlineData("Ctrl+Alt+R", (uint)(HotkeyModifiers.Control | HotkeyModifiers.Alt), 0x52u)]
    [InlineData("Shift+Win+F1", (uint)(HotkeyModifiers.Shift | HotkeyModifiers.Win), 0x70u)]
    [InlineData("Alt+Space", (uint)HotkeyModifiers.Alt, 0x20u)]
    public void RegisterHotkey_ParsesModifiersAndKey_RegistersWithProvider(
        string hotkeyString,
        uint expectedModifiers,
        uint expectedVk)
    {
        // Arrange
        _mockProvider.RegisterHotKey(Arg.Any<int>(), expectedModifiers, expectedVk).Returns(true);
        using var service = new WindowsGlobalHotkeyService(_mockProvider);

        // Act
        bool result = service.RegisterHotkey(hotkeyString, () => { });

        // Assert
        Assert.True(result);
        _mockProvider.Received(1).RegisterHotKey(Arg.Any<int>(), expectedModifiers, expectedVk);
    }

    [Fact]
    public void HotkeyPressed_TriggersRegisteredCallback()
    {
        // Arrange
        int capturedId = -1;
        _mockProvider.RegisterHotKey(Arg.Do<int>(id => capturedId = id), Arg.Any<uint>(), Arg.Any<uint>())
            .Returns(true);

        using var service = new WindowsGlobalHotkeyService(_mockProvider);
        bool callbackInvoked = false;
        bool registered = service.RegisterHotkey("Ctrl+Alt+S", () => callbackInvoked = true);
        Assert.True(registered);

        // Act
        _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedId);

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void UnregisterHotkey_UnregistersFromProviderAndRemovesCallback()
    {
        // Arrange
        int capturedId = -1;
        _mockProvider.RegisterHotKey(Arg.Do<int>(id => capturedId = id), Arg.Any<uint>(), Arg.Any<uint>())
            .Returns(true);
        _mockProvider.UnregisterHotKey(Arg.Any<int>()).Returns(true);

        using var service = new WindowsGlobalHotkeyService(_mockProvider);
        int invokeCount = 0;
        service.RegisterHotkey("Ctrl+Alt+S", () => invokeCount++);

        // Act
        service.UnregisterHotkey("Ctrl+Alt+S");

        // Assert
        _mockProvider.Received(1).UnregisterHotKey(capturedId);

        // Raising the event after unregistering should not invoke the callback
        _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedId);
        Assert.Equal(0, invokeCount);
    }

    [Fact]
    public void UnregisterAll_CleansUpAllRegistrations()
    {
        // Arrange
        var capturedIds = new List<int>();
        _mockProvider.RegisterHotKey(Arg.Do<int>(id => capturedIds.Add(id)), Arg.Any<uint>(), Arg.Any<uint>())
            .Returns(true);
        _mockProvider.UnregisterHotKey(Arg.Any<int>()).Returns(true);

        using var service = new WindowsGlobalHotkeyService(_mockProvider);
        int invokeCount1 = 0;
        int invokeCount2 = 0;
        service.RegisterHotkey("Ctrl+Alt+S", () => invokeCount1++);
        service.RegisterHotkey("Ctrl+Alt+D", () => invokeCount2++);
        Assert.Equal(2, capturedIds.Count);

        // Act
        service.UnregisterAll();

        // Assert
        foreach (int id in capturedIds)
        {
            _mockProvider.Received(1).UnregisterHotKey(id);
        }

        // Raising events for cleared hotkeys must not trigger callbacks
        _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedIds[0]);
        _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedIds[1]);
        Assert.Equal(0, invokeCount1);
        Assert.Equal(0, invokeCount2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Alt+")]
    [InlineData("Ctrl+Alt")]
    [InlineData("InvalidMod+S")]
    [InlineData("Ctrl+Alt+UnknownKey123")]
    [InlineData("Ctrl+A+B")]
    public void RegisterHotkey_InvalidKeyString_ReturnsFalse(string? invalidHotkey)
    {
        // Arrange
        using var service = new WindowsGlobalHotkeyService(_mockProvider);

        // Act
        bool result = service.RegisterHotkey(invalidHotkey!, () => { });

        // Assert
        Assert.False(result);
        _mockProvider.DidNotReceiveWithAnyArgs().RegisterHotKey(default, default, default);
    }

    [Fact]
    public void HotkeyPressed_WhenCallbackThrows_DoesNotCrashService()
    {
        // Arrange
        int capturedId1 = -1;
        int capturedId2 = -1;
        _mockProvider.RegisterHotKey(Arg.Do<int>(id =>
        {
            if (capturedId1 == -1)
            {
                capturedId1 = id;
            }
            else
            {
                capturedId2 = id;
            }
        }), Arg.Any<uint>(), Arg.Any<uint>()).Returns(true);

        using var service = new WindowsGlobalHotkeyService(_mockProvider);
        service.RegisterHotkey("Ctrl+Alt+S", () => throw new InvalidOperationException("Callback fault"));

        bool secondCallbackExecuted = false;
        service.RegisterHotkey("Ctrl+Alt+D", () => secondCallbackExecuted = true);

        // Act & Assert - Should not throw or crash
        var exception = Record.Exception(() =>
        {
            _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedId1);
        });

        Assert.Null(exception);

        // Subsequent hotkeys should still fire correctly
        _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedId2);
        Assert.True(secondCallbackExecuted);
    }

    [Fact]
    public void RegisterHotkey_WhenProviderReturnsFalse_ReturnsFalse()
    {
        // Arrange
        int capturedId = -1;
        _mockProvider.RegisterHotKey(Arg.Do<int>(id => capturedId = id), Arg.Any<uint>(), Arg.Any<uint>())
            .Returns(false);

        using var service = new WindowsGlobalHotkeyService(_mockProvider);
        bool callbackInvoked = false;

        // Act
        bool result = service.RegisterHotkey("Ctrl+Alt+S", () => callbackInvoked = true);

        // Assert
        Assert.False(result);

        // Event for that ID should not invoke callback
        if (capturedId != -1)
        {
            _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedId);
            Assert.False(callbackInvoked);
        }
    }

    [Fact]
    public void RegisterHotkey_ReregisteringSameHotkey_UnregistersPreviousAndUpdatesCallback()
    {
        // Arrange
        var capturedIds = new List<int>();
        _mockProvider.RegisterHotKey(Arg.Do<int>(id => capturedIds.Add(id)), Arg.Any<uint>(), Arg.Any<uint>())
            .Returns(true);
        _mockProvider.UnregisterHotKey(Arg.Any<int>()).Returns(true);

        using var service = new WindowsGlobalHotkeyService(_mockProvider);
        int callback1Count = 0;
        int callback2Count = 0;

        // Act
        service.RegisterHotkey("Ctrl+Alt+S", () => callback1Count++);
        service.RegisterHotkey("ctrl+alt+s", () => callback2Count++);

        // Assert
        Assert.Equal(2, capturedIds.Count);
        _mockProvider.Received(1).UnregisterHotKey(capturedIds[0]);

        // Triggering old ID should do nothing
        _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedIds[0]);
        Assert.Equal(0, callback1Count);

        // Triggering new ID should invoke second callback
        _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedIds[1]);
        Assert.Equal(1, callback2Count);
    }

    [Fact]
    public void Dispose_UnregistersAllHotkeysAndUnsubscribesFromProvider()
    {
        // Arrange
        int capturedId = -1;
        _mockProvider.RegisterHotKey(Arg.Do<int>(id => capturedId = id), Arg.Any<uint>(), Arg.Any<uint>())
            .Returns(true);
        _mockProvider.UnregisterHotKey(Arg.Any<int>()).Returns(true);

        var service = new WindowsGlobalHotkeyService(_mockProvider);
        int invokeCount = 0;
        service.RegisterHotkey("Ctrl+Alt+S", () => invokeCount++);

        // Act
        service.Dispose();

        // Assert
        _mockProvider.Received(1).UnregisterHotKey(capturedId);

        // Raising event after dispose should not invoke callback
        _mockProvider.HotkeyPressed += Raise.Event<Action<int>>(capturedId);
        Assert.Equal(0, invokeCount);

        // Further operations throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => service.RegisterHotkey("Ctrl+Alt+D", () => { }));
    }

    [Fact]
    public void RegisterHotkey_WhenCallbackIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        using var service = new WindowsGlobalHotkeyService(_mockProvider);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.RegisterHotkey("Ctrl+Alt+S", null!));
    }

    [Fact]
    public void WindowsNativeHotkeyProvider_CanInitializeAndDisposeCleanly()
    {
        // Arrange & Act
        var provider = new WindowsNativeHotkeyProvider();

        // Assert - Dispose cleanly without hanging
        provider.Dispose();
    }

    [Fact]
    public void WindowsNativeHotkeyProvider_DoubleDispose_DoesNotThrow()
    {
        // Arrange
        var provider = new WindowsNativeHotkeyProvider();
        provider.Dispose();

        // Act & Assert
        var exception = Record.Exception(() => provider.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void WindowsNativeHotkeyProvider_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new WindowsNativeHotkeyProvider();
        provider.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => provider.RegisterHotKey(1, (uint)HotkeyModifiers.Control, 0x41));
        Assert.Throws<ObjectDisposedException>(() => provider.UnregisterHotKey(1));
    }

    [Fact]
    public void WindowsGlobalHotkeyService_DefaultConstructor_InstantiatesAndDisposesCleanly()
    {
        // Arrange & Act
        var service = new WindowsGlobalHotkeyService();

        // Assert
        service.Dispose();
    }
}
