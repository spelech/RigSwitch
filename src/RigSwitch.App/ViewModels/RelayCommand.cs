namespace RigSwitch.App.ViewModels;

using System.Windows.Input;

/// <summary>
/// A reusable implementation of <see cref="ICommand"/> for delegating actions.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand"/> class.
    /// </summary>
    /// <param name="execute">The action to execute.</param>
    /// <param name="canExecute">The optional predicate determining whether the command can execute.</param>
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// Initializes a new parameterless instance of the <see cref="RelayCommand"/> class.
    /// </summary>
    /// <param name="execute">The parameterless action to execute.</param>
    /// <param name="canExecute">The optional parameterless predicate determining whether the command can execute.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
        ArgumentNullException.ThrowIfNull(execute);
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event manually.
    /// </summary>
    public static void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>
/// An asynchronous implementation of <see cref="ICommand"/> preventing re-entrant execution.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncRelayCommand"/> class.
    /// </summary>
    /// <param name="execute">The asynchronous function to execute.</param>
    /// <param name="canExecute">The optional predicate determining whether the command can execute.</param>
    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// Initializes a new parameterless instance of the <see cref="AsyncRelayCommand"/> class.
    /// </summary>
    /// <param name="execute">The parameterless asynchronous function to execute.</param>
    /// <param name="canExecute">The optional parameterless predicate determining whether the command can execute.</param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
        ArgumentNullException.ThrowIfNull(execute);
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    /// <inheritdoc/>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event manually.
    /// </summary>
    public static void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
