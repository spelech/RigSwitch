namespace RigSwitch.Tests.Harness;

using System.Diagnostics;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Tests.Harness.Disturbances;
using RigSwitch.Tests.Harness.Envelope;

/// <summary>
/// Metric results from high-throughput simulation stress runs.
/// </summary>
public sealed record StressRunResult
{
    public int TotalIterations { get; init; }
    public int SuccessfulSwitches { get; init; }
    public int FailedSwitches { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public TimeSpan MaxTransitionDuration { get; init; }
    public TimeSpan AverageTransitionDuration { get; init; }
    public ProfileMode FinalProfile { get; init; }
    public bool Converged { get; init; }
}

/// <summary>
/// Executes high-throughput profile switches and disturbance scenarios, capturing diagnostics into agent envelopes on failure.
/// </summary>
public sealed class SimulationHarnessRunner
{
    /// <summary>
    /// Gets or sets the timeout applied per individual profile switch.
    /// </summary>
    public TimeSpan PerSwitchTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Executes a sequential profile-switching stress loop for a specified number of iterations.
    /// </summary>
    public async Task<StressRunResult> RunSequentialSwitchLoopAsync(
        IProfileSwitchCoordinator coordinator,
        SimulationDisturbanceContext context,
        int iterations = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        var totalSw = Stopwatch.StartNew();
        var successCount = 0;
        var failCount = 0;
        var maxDuration = TimeSpan.Zero;
        var totalTransitionTime = TimeSpan.Zero;

        for (var i = 1; i <= iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = coordinator.CurrentProfile == ProfileMode.Desk
                ? ProfileMode.SimRig
                : ProfileMode.Desk;

            using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stepCts.CancelAfter(PerSwitchTimeout);

            var sw = Stopwatch.StartNew();
            bool success;
            try
            {
                success = await coordinator.SwitchProfileAsync(target, stepCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                context.Log($"[Runner] Iteration {i} threw unexpected exception: {ex.Message}");
                var envelope = AgentFeedbackEnvelope.Create(
                    inputs: new Dictionary<string, object?> { ["iteration"] = i, ["target"] = target.ToString() },
                    activeSettings: context.Settings,
                    actionHistory: context.RingBuffer.GetSnapshot(),
                    expected: "Successful transition",
                    actual: $"Exception: {ex.GetType().Name} - {ex.Message}",
                    capturedLogs: context.Logs,
                    reproductionCommand: "dotnet test tests/RigSwitch.Tests.Harness --filter FullyQualifiedName~RapidSwitchLoop_50Iterations_ConvergesCleanlyWithoutDeadlock");

                throw new SimulationHarnessException($"Profile switch threw an unhandled exception on iteration {i}.", envelope);
            }

            sw.Stop();
            var duration = sw.Elapsed;
            totalTransitionTime += duration;
            if (duration > maxDuration)
            {
                maxDuration = duration;
            }

            if (success)
            {
                successCount++;
            }
            else
            {
                failCount++;
                var envelope = AgentFeedbackEnvelope.Create(
                    inputs: new Dictionary<string, object?> { ["iteration"] = i, ["target"] = target.ToString() },
                    activeSettings: context.Settings,
                    actionHistory: context.RingBuffer.GetSnapshot(),
                    expected: true,
                    actual: false,
                    capturedLogs: context.Logs,
                    reproductionCommand: "dotnet test tests/RigSwitch.Tests.Harness --filter FullyQualifiedName~RapidSwitchLoop_50Iterations_ConvergesCleanlyWithoutDeadlock");

                throw new SimulationHarnessException($"Sequential switch iteration {i} failed to converge.", envelope);
            }
        }

        totalSw.Stop();

        var converged = successCount == iterations && failCount == 0;
        var avgDuration = iterations > 0
            ? TimeSpan.FromTicks(totalTransitionTime.Ticks / iterations)
            : TimeSpan.Zero;

        return new StressRunResult
        {
            TotalIterations = iterations,
            SuccessfulSwitches = successCount,
            FailedSwitches = failCount,
            TotalDuration = totalSw.Elapsed,
            MaxTransitionDuration = maxDuration,
            AverageTransitionDuration = avgDuration,
            FinalProfile = coordinator.CurrentProfile,
            Converged = converged
        };
    }

    /// <summary>
    /// Executes rapid concurrent profile switches with mid-transition cancellations to assert clean unwinding and lock recovery.
    /// </summary>
    public async Task RunConcurrentSwitchesWithCancellationAsync(
        IProfileSwitchCoordinator coordinator,
        SimulationDisturbanceContext context,
        int taskCount = 8,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(context);

        var tasks = new Task[taskCount];

        for (var i = 0; i < taskCount; i++)
        {
            var taskIndex = i;
            var target = (taskIndex % 2 == 0) ? ProfileMode.SimRig : ProfileMode.Desk;

            tasks[taskIndex] = Task.Run(async () =>
            {
                // Alternate cancellation behavior: odd tasks get cancelled quickly mid-flight
                using var perTaskCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (taskIndex % 2 == 1)
                {
                    perTaskCts.CancelAfter(TimeSpan.FromMilliseconds(5));
                }
                else
                {
                    perTaskCts.CancelAfter(PerSwitchTimeout);
                }

                try
                {
                    await coordinator.SwitchProfileAsync(target, perTaskCts.Token).ConfigureAwait(false);
                    context.Log($"[Runner] Task {taskIndex} completed switch to {target}");
                }
                catch (OperationCanceledException)
                {
                    context.Log($"[Runner] Task {taskIndex} cancelled as expected.");
                }
                catch (Exception ex)
                {
                    context.Log($"[Runner] Task {taskIndex} failed with unexpected error: {ex.Message}");
                    throw;
                }
            }, cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when tasks get cancelled
        }
    }

    /// <summary>
    /// Validates an assertion and throws a <see cref="SimulationHarnessException"/> containing the agent feedback envelope if false.
    /// </summary>
    public static void AssertWithEnvelope(
        bool condition,
        string message,
        SimulationDisturbanceContext context,
        object? expected,
        object? actual,
        string reproductionCommand,
        Dictionary<string, object?>? inputs = null)
    {
        if (!condition)
        {
            var envelope = AgentFeedbackEnvelope.Create(
                inputs: inputs ?? new Dictionary<string, object?>(),
                activeSettings: context.Settings,
                actionHistory: context.RingBuffer.GetSnapshot(),
                expected: expected,
                actual: actual,
                capturedLogs: context.Logs,
                reproductionCommand: reproductionCommand);

            throw new SimulationHarnessException(message, envelope);
        }
    }
}
