namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Windows.Processes;
using Xunit;

public sealed class WindowsApplicationLifecycleHookServiceTests : IDisposable
{
    private readonly INativeProcessProvider _processProvider;
    private readonly WindowsApplicationLifecycleHookService _service;

    public WindowsApplicationLifecycleHookServiceTests()
    {
        _processProvider = Substitute.For<INativeProcessProvider>();
        _service = new WindowsApplicationLifecycleHookService(_processProvider);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public async Task LaunchHooksForPresetAsync_WhenPresetHasHooks_StartsProcessesAndRecordsPids()
    {
        // Arrange
        var hook1 = new PresetApplicationHook
        {
            Id = "hook-1",
            ExecutablePath = @"C:\Apps\PitHouse.exe",
            Arguments = "--minimized"
        };
        var hook2 = new PresetApplicationHook
        {
            Id = "hook-2",
            ExecutablePath = @"C:\Apps\SimHub.exe",
            Arguments = ""
        };
        var preset = new WorkstationPreset
        {
            ApplicationHooks = [hook1, hook2]
        };

        var mockProcess1 = Substitute.For<INativeProcess>();
        mockProcess1.Id.Returns(1001);

        var mockProcess2 = Substitute.For<INativeProcess>();
        mockProcess2.Id.Returns(1002);

        _processProvider.FileExists(@"C:\Apps\PitHouse.exe").Returns(true);
        _processProvider.FileExists(@"C:\Apps\SimHub.exe").Returns(true);
        _processProvider.Start(@"C:\Apps\PitHouse.exe", "--minimized").Returns(mockProcess1);
        _processProvider.Start(@"C:\Apps\SimHub.exe", "").Returns(mockProcess2);

        // Act
        await _service.LaunchHooksForPresetAsync(preset);

        // Assert
        _processProvider.Received(1).Start(@"C:\Apps\PitHouse.exe", "--minimized");
        _processProvider.Received(1).Start(@"C:\Apps\SimHub.exe", "");
        mockProcess1.Received(1).Dispose();
        mockProcess2.Received(1).Dispose();
    }

    [Fact]
    public async Task LaunchHooksForPresetAsync_WhenFileDoesNotExist_LogsWarningSafelyWithoutThrowing()
    {
        // Arrange
        var hook = new PresetApplicationHook
        {
            Id = "hook-missing",
            ExecutablePath = @"C:\NonExistent\Missing.exe"
        };
        var preset = new WorkstationPreset
        {
            ApplicationHooks = [hook]
        };

        _processProvider.FileExists(@"C:\NonExistent\Missing.exe").Returns(false);

        // Act & Assert (should complete safely without throwing)
        await _service.LaunchHooksForPresetAsync(preset);

        _processProvider.DidNotReceive().Start(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LaunchHooksForPresetAsync_WhenStartThrows_ContinuesWithoutThrowing()
    {
        // Arrange
        var hook = new PresetApplicationHook
        {
            Id = "hook-err",
            ExecutablePath = @"C:\Apps\Error.exe"
        };
        var preset = new WorkstationPreset
        {
            ApplicationHooks = [hook]
        };

        _processProvider.FileExists(@"C:\Apps\Error.exe").Returns(true);
        _processProvider.Start(@"C:\Apps\Error.exe", Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("System error"));

        // Act & Assert
        await _service.LaunchHooksForPresetAsync(preset);
    }

    [Fact]
    public async Task CloseHooksForPresetAsync_WhenProcessGracefullyCloses_DoesNotKill()
    {
        // Arrange
        var hook = new PresetApplicationHook
        {
            Id = "hook-graceful",
            ExecutablePath = @"C:\Apps\App.exe",
            CloseOnSwitchAway = true
        };
        var preset = new WorkstationPreset
        {
            ApplicationHooks = [hook]
        };

        var mockProcess = Substitute.For<INativeProcess>();
        mockProcess.Id.Returns(2001);
        mockProcess.CloseMainWindow().Returns(true);
        mockProcess.WaitForExitAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        mockProcess.HasExited.Returns(false);

        _processProvider.FileExists(@"C:\Apps\App.exe").Returns(true);
        _processProvider.Start(@"C:\Apps\App.exe", "").Returns(mockProcess);
        _processProvider.GetProcessById(2001).Returns(mockProcess);

        // Launch first to track PID
        await _service.LaunchHooksForPresetAsync(preset);

        // Act
        await _service.CloseHooksForPresetAsync(preset);

        // Assert
        mockProcess.Received(1).CloseMainWindow();
        await mockProcess.Received(1).WaitForExitAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        mockProcess.DidNotReceive().Kill(Arg.Any<bool>());
    }

    [Fact]
    public async Task CloseHooksForPresetAsync_WhenGracefulCloseTimesOut_InvokesKillWithProcessTree()
    {
        // Arrange
        var hook = new PresetApplicationHook
        {
            Id = "hook-kill",
            ExecutablePath = @"C:\Apps\StubbornApp.exe",
            CloseOnSwitchAway = true
        };
        var preset = new WorkstationPreset
        {
            ApplicationHooks = [hook]
        };

        var mockProcess = Substitute.For<INativeProcess>();
        mockProcess.Id.Returns(3001);
        mockProcess.CloseMainWindow().Returns(true);
        // Timeout
        mockProcess.WaitForExitAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        mockProcess.HasExited.Returns(false);

        _processProvider.FileExists(@"C:\Apps\StubbornApp.exe").Returns(true);
        _processProvider.Start(@"C:\Apps\StubbornApp.exe", "").Returns(mockProcess);
        _processProvider.GetProcessById(3001).Returns(mockProcess);

        await _service.LaunchHooksForPresetAsync(preset);

        // Act
        await _service.CloseHooksForPresetAsync(preset);

        // Assert
        mockProcess.Received(1).CloseMainWindow();
        mockProcess.Received(1).Kill(entireProcessTree: true);
    }

    [Fact]
    public async Task CloseHooksForPresetAsync_WhenCloseOnSwitchAwayIsFalse_DoesNotCloseOrKill()
    {
        // Arrange
        var hook = new PresetApplicationHook
        {
            Id = "hook-persistent",
            ExecutablePath = @"C:\Apps\KeepRunning.exe",
            CloseOnSwitchAway = false
        };
        var preset = new WorkstationPreset
        {
            ApplicationHooks = [hook]
        };

        var mockProcess = Substitute.For<INativeProcess>();
        mockProcess.Id.Returns(4001);

        _processProvider.FileExists(@"C:\Apps\KeepRunning.exe").Returns(true);
        _processProvider.Start(@"C:\Apps\KeepRunning.exe", "").Returns(mockProcess);

        await _service.LaunchHooksForPresetAsync(preset);

        // Act
        await _service.CloseHooksForPresetAsync(preset);

        // Assert
        _processProvider.DidNotReceive().GetProcessById(Arg.Any<int>());
        mockProcess.DidNotReceive().CloseMainWindow();
        mockProcess.DidNotReceive().Kill(Arg.Any<bool>());
    }

    [Fact]
    public async Task CloseHooksForPresetAsync_WhenPidExpiredOrUntracked_FallsBackToGetProcessesByName()
    {
        // Arrange
        var hook = new PresetApplicationHook
        {
            Id = "hook-fallback",
            ExecutablePath = @"C:\Apps\OrphanApp.exe",
            CloseOnSwitchAway = true
        };
        var preset = new WorkstationPreset
        {
            ApplicationHooks = [hook]
        };

        var fallbackProc = Substitute.For<INativeProcess>();
        fallbackProc.Id.Returns(5001);
        fallbackProc.CloseMainWindow().Returns(true);
        fallbackProc.WaitForExitAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        fallbackProc.HasExited.Returns(false);

        _processProvider.GetProcessesByName("OrphanApp").Returns([fallbackProc]);

        // Act - Close without prior Launch so no PID is recorded
        await _service.CloseHooksForPresetAsync(preset);

        // Assert
        _processProvider.Received(1).GetProcessesByName("OrphanApp");
        fallbackProc.Received(1).CloseMainWindow();
    }
}
