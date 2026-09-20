namespace RigSwitch.Tests.Harness;

using System.Text.Json;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using RigSwitch.Tests.Harness.Disturbances;
using RigSwitch.Tests.Harness.Envelope;
using RigSwitch.Tests.Harness.Taps;
using Xunit;

public sealed class SimulationStressTests
{
    [Fact]
    public void DiagnosticRingBuffer_RecordsEventsAndMaintainsCapacity()
    {
        // Arrange
        var buffer = new DiagnosticRingBuffer(capacity: 5);

        // Act
        for (var i = 1; i <= 8; i++)
        {
            buffer.Record($"Action_{i}", input: i, result: i * 2, success: true);
        }

        var snapshot = buffer.GetSnapshot();

        // Assert
        Assert.Equal(5, buffer.Count);
        Assert.Equal(5, snapshot.Count);
        Assert.Equal("Action_4", snapshot[0].Action);
        Assert.Equal("Action_8", snapshot[4].Action);
    }

    [Fact]
    public void AgentFeedbackEnvelope_SerializesAllSixPartsCorrectly()
    {
        // Arrange
        var buffer = new DiagnosticRingBuffer(10);
        buffer.Record("TestEvent", input: "req", result: "res", success: true);

        var settings = new UserSettings { DeskMonitorId = "TEST_MON" };
        var logs = new List<string> { "Log line 1", "Log line 2" };
        var inputs = new Dictionary<string, object?> { ["param1"] = 42 };

        var envelope = AgentFeedbackEnvelope.Create(
            inputs: inputs,
            activeSettings: settings,
            actionHistory: buffer.GetSnapshot(),
            expected: "SimRig",
            actual: "Desk",
            capturedLogs: logs,
            reproductionCommand: "dotnet test --filter Test");

        // Act
        var json = envelope.ToJson();

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("inputs", out _));
        Assert.True(root.TryGetProperty("active_settings", out _));
        Assert.True(root.TryGetProperty("action_history", out _));
        Assert.True(root.TryGetProperty("output_delta", out var delta));
        Assert.Equal("SimRig", delta.GetProperty("expected").GetString());
        Assert.Equal("Desk", delta.GetProperty("actual").GetString());
        Assert.True(root.TryGetProperty("captured_logs", out _));
        Assert.True(root.TryGetProperty("reproduction_command", out _));
    }

    [Fact]
    public async Task RapidSwitchLoop_50Iterations_ConvergesCleanlyWithoutDeadlock()
    {
        // Arrange
        var context = new SimulationDisturbanceContext();
        using var coordinator = context.CreateCoordinator(initialProfile: ProfileMode.Desk);
        var runner = new SimulationHarnessRunner();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await runner.RunSequentialSwitchLoopAsync(coordinator, context, iterations: 50, cts.Token);

        // Assert
        Assert.Equal(50, result.TotalIterations);
        Assert.Equal(50, result.SuccessfulSwitches);
        Assert.Equal(0, result.FailedSwitches);
        Assert.True(result.Converged);
        Assert.Equal(ProfileMode.Desk, result.FinalProfile);
        Assert.True(context.IsMonitorActive(context.Settings.DeskMonitorId));
    }

    [Fact]
    public async Task Disturbance_WhenRigMonitorMissing_SafelyAbortsWithDiagnosticEnvelope()
    {
        // Arrange
        var context = new SimulationDisturbanceContext();
        using var coordinator = context.CreateCoordinator(initialProfile: ProfileMode.Desk);

        // Simulate missing ASUS monitor during SimRig switch
        context.SimulateTargetMonitorDisconnect(context.Settings.RigMonitorId);

        // Act
        var switchSucceeded = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert
        Assert.False(switchSucceeded);
        Assert.Equal(ProfileMode.Desk, coordinator.CurrentProfile);
        Assert.True(context.IsMonitorActive(context.Settings.DeskMonitorId));

        // Verify ring buffer recorded the safety gate trigger
        var events = context.RingBuffer.GetSnapshot();
        Assert.Contains(events, e => e.Success == false && (e.ErrorMessage?.Contains(context.Settings.RigMonitorId) ?? false));
    }

    [Fact]
    public async Task Disturbance_WhenPebbleV3Unplugged_AlwaysRoutesToMsiFallback()
    {
        // Arrange
        var context = new SimulationDisturbanceContext();
        using var coordinator = context.CreateCoordinator(initialProfile: ProfileMode.Desk);
        var random = new Random(12345);

        // Act & Assert across 20 switches
        for (var i = 0; i < 20; i++)
        {
            var isPebbleUnplugged = random.Next(2) == 0;
            var targetState = isPebbleUnplugged ? DevicePresenceState.Unplugged : DevicePresenceState.Active;
            context.SimulateAudioDeviceStateChange(context.Settings.DeskPrimaryAudioId, targetState);

            // Toggle to SimRig and back to Desk to test audio routing
            var toRig = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);
            Assert.True(toRig);

            var toDesk = await coordinator.SwitchProfileAsync(ProfileMode.Desk);
            Assert.True(toDesk);

            if (isPebbleUnplugged)
            {
                Assert.Equal(context.Settings.DeskFallbackAudioId, context.ActiveDefaultAudioId);
            }
            else
            {
                Assert.Equal(context.Settings.DeskPrimaryAudioId, context.ActiveDefaultAudioId);
            }
        }
    }

    [Fact]
    public async Task Disturbance_ConcurrentCancellation_UnwindsCleanlyWithoutCorruptingState()
    {
        // Arrange
        var context = new SimulationDisturbanceContext();
        using var coordinator = context.CreateCoordinator(initialProfile: ProfileMode.Desk);
        var runner = new SimulationHarnessRunner();

        // Introduce simulated delay so tasks overlap and cancellation fires mid-transition
        context.SimulateDisplaySwitchDelay(TimeSpan.FromMilliseconds(20));

        // Act - run concurrent switches with cancellations
        await runner.RunConcurrentSwitchesWithCancellationAsync(coordinator, context, taskCount: 8);

        // Assert - After cancellations unwind, coordinator gate must NOT be orphaned or corrupt
        context.SimulateDisplaySwitchDelay(TimeSpan.Zero);
        using var cleanCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var recoverySwitch = await coordinator.SwitchProfileAsync(ProfileMode.SimRig, cleanCts.Token);
        Assert.True(recoverySwitch);
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
        Assert.True(context.IsMonitorActive(context.Settings.RigMonitorId));

        var backToDesk = await coordinator.SwitchProfileAsync(ProfileMode.Desk, cleanCts.Token);
        Assert.True(backToDesk);
        Assert.Equal(ProfileMode.Desk, coordinator.CurrentProfile);
        Assert.True(context.IsMonitorActive(context.Settings.DeskMonitorId));
    }

    [Fact]
    public async Task Disturbance_WhenCcdFails_CapturesFailureInRingBufferAndAborts()
    {
        // Arrange
        var context = new SimulationDisturbanceContext();
        using var coordinator = context.CreateCoordinator(initialProfile: ProfileMode.Desk);

        context.SimulateCcdFailure(0x1F); // Simulated CCD error code

        // Act
        var success = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert
        Assert.False(success);
        Assert.Equal(ProfileMode.Desk, coordinator.CurrentProfile);
        var events = context.RingBuffer.GetSnapshot();
        Assert.Contains(events, e => e.Success == false && (e.ErrorMessage?.Contains("0x0000001F") ?? false));
    }

    [Fact]
    public async Task SimulationHarnessRunner_WhenSwitchFails_ThrowsSimulationHarnessExceptionWithEnvelope()
    {
        // Arrange
        var context = new SimulationDisturbanceContext();
        using var coordinator = context.CreateCoordinator(initialProfile: ProfileMode.Desk);
        var runner = new SimulationHarnessRunner();

        // Disconnect rig monitor so the first switch to SimRig fails
        context.SimulateTargetMonitorDisconnect(context.Settings.RigMonitorId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<SimulationHarnessException>(async () =>
        {
            await runner.RunSequentialSwitchLoopAsync(coordinator, context, iterations: 1);
        });

        Assert.NotNull(ex.Envelope);
        Assert.Equal(false, ex.Envelope.OutputDelta.Actual);
        Assert.Equal(true, ex.Envelope.OutputDelta.Expected);
        Assert.NotEmpty(ex.Envelope.ReproductionCommand);
        Assert.NotEmpty(ex.Envelope.ActionHistory);
    }

    [Fact]
    public async Task ScopedDisturbances_RestoreStateUponDisposal()
    {
        // Arrange
        var context = new SimulationDisturbanceContext();
        var rigMon = context.Settings.RigMonitorId;
        var pebbleId = context.Settings.DeskPrimaryAudioId;

        // Act & Assert: Monitor unplug disturbance
        using (new MonitorUnpluggedDisturbance(context, rigMon))
        {
            var currentDisplays = await context.DisplayService.EnumerateDisplaysAsync();
            Assert.DoesNotContain(currentDisplays, d => d.MonitorId == rigMon);
        }
        var restoredDisplays = await context.DisplayService.EnumerateDisplaysAsync();
        Assert.Contains(restoredDisplays, d => d.MonitorId == rigMon);

        // Act & Assert: Audio fallback disturbance
        using (new AudioFallbackDisturbance(context, pebbleId, DevicePresenceState.Unplugged))
        {
            Assert.Equal(DevicePresenceState.Unplugged, context.GetAudioDeviceState(pebbleId));
        }
        Assert.Equal(DevicePresenceState.Active, context.GetAudioDeviceState(pebbleId));
    }

    [Fact]
    public async Task StressTest_RapidPresetSwitching_ConvergesDeterministically()
    {
        // Arrange
        var context = new SimulationDisturbanceContext();
        using var coordinator = context.CreateCoordinator(initialProfile: ProfileMode.Desk);

        var switchSequence = new (ProfileMode Profile, int PresetIndex)[]
        {
            (ProfileMode.SimRig, 0),
            (ProfileMode.SimRig, 1),
            (ProfileMode.Desk, 0),
            (ProfileMode.Desk, 2),
            (ProfileMode.SimRig, 2),
            (ProfileMode.Desk, 1),
        };

        // Act & Assert across rapid multi-preset switching cycles
        for (var iteration = 0; iteration < 5; iteration++)
        {
            foreach (var (targetProfile, presetIndex) in switchSequence)
            {
                var success = await coordinator.SwitchToPresetAsync(targetProfile, presetIndex);
                Assert.True(success);

                // Verify coordinator convergence
                Assert.Equal(targetProfile, coordinator.CurrentProfile);
                Assert.Equal(presetIndex, coordinator.CurrentPresetIndex);

                var expectedPreset = targetProfile == ProfileMode.Desk
                    ? context.Settings.DeskPresets[presetIndex]
                    : context.Settings.RigPresets[presetIndex];
                Assert.Equal(expectedPreset.Name, coordinator.CurrentPreset.Name);

                // Verify native display convergence
                Assert.True(context.IsMonitorActive(expectedPreset.TargetMonitorId));

                var inactiveProfile = targetProfile == ProfileMode.Desk ? ProfileMode.SimRig : ProfileMode.Desk;
                var inactivePreset = context.Settings.GetActivePreset(inactiveProfile);
                if (!string.Equals(inactivePreset.TargetMonitorId, expectedPreset.TargetMonitorId, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.False(context.IsMonitorActive(inactivePreset.TargetMonitorId));
                }

                // Verify native audio convergence
                Assert.Equal(expectedPreset.PrimaryAudioId, context.ActiveDefaultAudioId);
                var endpoints = await context.AudioDirector.EnumerateAudioEndpointsAsync();
                var activeEndpoint = endpoints.First(e => string.Equals(e.Id, expectedPreset.PrimaryAudioId, StringComparison.OrdinalIgnoreCase));
                Assert.True(activeEndpoint.IsDefaultPlayback);
            }
        }
    }

    [Fact]
    public async Task Disturbance_MonitorUnpluggedDuringPresetSwitch_HaltsGracefullyViaSafetyGateAndLogsToRingBuffer()
    {
        // Arrange
        var context = new SimulationDisturbanceContext();
        using var coordinator = context.CreateCoordinator(initialProfile: ProfileMode.Desk);
        var targetPreset = context.Settings.RigPresets[1];

        // Act: Inject disturbance by unplugging the monitor configured for SimRig Preset 1
        using (new MonitorUnpluggedDisturbance(context, targetPreset.TargetMonitorId))
        {
            var switchSucceeded = await coordinator.SwitchToPresetAsync(ProfileMode.SimRig, 1);

            // Assert: Safety gate halted transition gracefully
            Assert.False(switchSucceeded);
            Assert.Equal(ProfileMode.Desk, coordinator.CurrentProfile);
            Assert.Equal(0, coordinator.CurrentPresetIndex);
            Assert.True(context.IsMonitorActive(context.Settings.DeskMonitorId));

            // Verify failure and monitor id are recorded in DiagnosticRingBuffer
            var events = context.RingBuffer.GetSnapshot();
            Assert.Contains(events, e => e.Success == false && (e.ErrorMessage?.Contains(targetPreset.TargetMonitorId) ?? false));
        }

        // Post-disturbance recovery: Once monitor is reconnected, preset switch succeeds
        var recoverySwitch = await coordinator.SwitchToPresetAsync(ProfileMode.SimRig, 1);
        Assert.True(recoverySwitch);
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
        Assert.Equal(1, coordinator.CurrentPresetIndex);
        Assert.True(context.IsMonitorActive(targetPreset.TargetMonitorId));
        Assert.Equal(targetPreset.PrimaryAudioId, context.ActiveDefaultAudioId);
    }
}
