namespace RigSwitch.Tests.Harness.Envelope;

using System.Text.Json;
using System.Text.Json.Serialization;
using RigSwitch.Core.Models;
using RigSwitch.Tests.Harness.Taps;

/// <summary>
/// Represents expected vs actual delta in the failure feedback envelope.
/// </summary>
public sealed record OutputDelta
{
    [JsonPropertyName("expected")]
    public object? Expected { get; init; }

    [JsonPropertyName("actual")]
    public object? Actual { get; init; }
}

/// <summary>
/// Standardized 6-part agent feedback envelope formatted on harness assertion failures.
/// </summary>
public sealed record AgentFeedbackEnvelope
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    [JsonPropertyName("inputs")]
    public Dictionary<string, object?> Inputs { get; init; } = new();

    [JsonPropertyName("active_settings")]
    public UserSettings? ActiveSettings { get; init; }

    [JsonPropertyName("action_history")]
    public IReadOnlyList<DiagnosticEvent> ActionHistory { get; init; } = Array.Empty<DiagnosticEvent>();

    [JsonPropertyName("output_delta")]
    public OutputDelta OutputDelta { get; init; } = new();

    [JsonPropertyName("captured_logs")]
    public IReadOnlyList<string> CapturedLogs { get; init; } = Array.Empty<string>();

    [JsonPropertyName("reproduction_command")]
    public string ReproductionCommand { get; init; } = string.Empty;

    /// <summary>
    /// Serializes the envelope into a formatted JSON string.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, SerializerOptions);
    }

    /// <summary>
    /// Creates a populated 6-part agent feedback envelope.
    /// </summary>
    public static AgentFeedbackEnvelope Create(
        Dictionary<string, object?> inputs,
        UserSettings? activeSettings,
        IReadOnlyList<DiagnosticEvent> actionHistory,
        object? expected,
        object? actual,
        IReadOnlyList<string> capturedLogs,
        string reproductionCommand)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(actionHistory);
        ArgumentNullException.ThrowIfNull(capturedLogs);
        ArgumentException.ThrowIfNullOrWhiteSpace(reproductionCommand);

        return new AgentFeedbackEnvelope
        {
            Inputs = inputs,
            ActiveSettings = activeSettings,
            ActionHistory = actionHistory,
            OutputDelta = new OutputDelta
            {
                Expected = expected,
                Actual = actual
            },
            CapturedLogs = capturedLogs,
            ReproductionCommand = reproductionCommand
        };
    }
}

/// <summary>
/// Exception thrown when a simulation harness assertion fails, carrying the 6-part agent feedback envelope.
/// </summary>
public sealed class SimulationHarnessException : Exception
{
    public AgentFeedbackEnvelope Envelope { get; }

    public SimulationHarnessException(string message, AgentFeedbackEnvelope envelope)
        : base($"{message}\n--- AGENT FEEDBACK ENVELOPE ---\n{envelope?.ToJson()}")
    {
        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
    }
}
