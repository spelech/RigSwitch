namespace RigSwitch.Tests.Harness.Taps;

/// <summary>
/// Represents a captured diagnostic event in the test harness ring buffer.
/// </summary>
public sealed record DiagnosticEvent
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;
    public string Action { get; init; } = string.Empty;
    public object? Input { get; init; }
    public object? Result { get; init; }
    public bool? Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();
}

/// <summary>
/// Fixed-size circular buffer capturing state transitions, timestamps, thread IDs, inputs, and results.
/// </summary>
public sealed class DiagnosticRingBuffer
{
    private readonly DiagnosticEvent[] _buffer;
    private readonly int _capacity;
    private readonly object _syncLock = new();
    private int _head;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticRingBuffer"/> class.
    /// </summary>
    /// <param name="capacity">Maximum capacity of the ring buffer (default: 100).</param>
    public DiagnosticRingBuffer(int capacity = 100)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _buffer = new DiagnosticEvent[capacity];
    }

    /// <summary>
    /// Gets the maximum capacity of the circular buffer.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the current number of events stored in the buffer.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_syncLock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Records a diagnostic event into the circular buffer, overwriting the oldest event if full.
    /// </summary>
    /// <param name="event">The diagnostic event to record.</param>
    public void Record(DiagnosticEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        lock (_syncLock)
        {
            _buffer[_head] = @event;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity)
            {
                _count++;
            }
        }
    }

    /// <summary>
    /// Records a diagnostic event by properties into the circular buffer.
    /// </summary>
    public void Record(
        string action,
        object? input = null,
        object? result = null,
        bool? success = null,
        string? errorMessage = null)
    {
        Record(new DiagnosticEvent
        {
            Action = action,
            Input = input,
            Result = result,
            Success = success,
            ErrorMessage = errorMessage
        });
    }

    /// <summary>
    /// Returns a chronological snapshot (oldest to newest) of the captured events.
    /// </summary>
    public IReadOnlyList<DiagnosticEvent> GetSnapshot()
    {
        lock (_syncLock)
        {
            if (_count == 0)
            {
                return Array.Empty<DiagnosticEvent>();
            }

            var snapshot = new DiagnosticEvent[_count];
            var start = _count < _capacity ? 0 : _head;

            for (var i = 0; i < _count; i++)
            {
                var index = (start + i) % _capacity;
                snapshot[i] = _buffer[index];
            }

            return snapshot;
        }
    }

    /// <summary>
    /// Clears all events currently in the buffer.
    /// </summary>
    public void Clear()
    {
        lock (_syncLock)
        {
            Array.Clear(_buffer, 0, _capacity);
            _head = 0;
            _count = 0;
        }
    }
}
