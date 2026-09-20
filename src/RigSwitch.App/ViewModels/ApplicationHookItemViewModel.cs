namespace RigSwitch.App.ViewModels;

using System.IO;
using System.Windows.Input;
using RigSwitch.Core.Models;

/// <summary>
/// Wraps a <see cref="PresetApplicationHook"/> for UI binding, editing, and removal.
/// </summary>
public sealed class ApplicationHookItemViewModel : ViewModelBase
{
    private string _id;
    private string _executablePath;
    private string _arguments;
    private bool _closeOnSwitchAway;

    /// <summary>
    /// Gets the unique identifier of the hook.
    /// </summary>
    public string Id
    {
        get => _id;
        init => _id = value;
    }

    /// <summary>
    /// Gets or sets the absolute path to the executable.
    /// </summary>
    public string ExecutablePath
    {
        get => _executablePath;
        set
        {
            if (SetProperty(ref _executablePath, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Gets or sets optional command line arguments.
    /// </summary>
    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this process should be terminated when switching away.
    /// </summary>
    public bool CloseOnSwitchAway
    {
        get => _closeOnSwitchAway;
        set => SetProperty(ref _closeOnSwitchAway, value);
    }

    /// <summary>
    /// Gets the user-friendly display name (filename or default fallback).
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                return "Unknown Application";
            }

            var fileName = Path.GetFileName(ExecutablePath);
            return string.IsNullOrWhiteSpace(fileName) ? ExecutablePath : fileName;
        }
    }

    /// <summary>
    /// Gets the command that removes this hook from its parent collection.
    /// </summary>
    public ICommand RemoveCommand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationHookItemViewModel"/> class from a model.
    /// </summary>
    /// <param name="hook">The underlying preset application hook model.</param>
    /// <param name="onRemove">Action to invoke when this item is removed.</param>
    public ApplicationHookItemViewModel(PresetApplicationHook hook, Action<ApplicationHookItemViewModel>? onRemove = null)
    {
        ArgumentNullException.ThrowIfNull(hook);
        _id = hook.Id;
        _executablePath = hook.ExecutablePath;
        _arguments = hook.Arguments;
        _closeOnSwitchAway = hook.CloseOnSwitchAway;

        RemoveCommand = new RelayCommand(() => onRemove?.Invoke(this));
    }

    /// <summary>
    /// Converts this ViewModel back to a <see cref="PresetApplicationHook"/> model.
    /// </summary>
    /// <returns>A new <see cref="PresetApplicationHook"/> instance.</returns>
    public PresetApplicationHook ToModel()
    {
        return new PresetApplicationHook
        {
            Id = Id,
            ExecutablePath = ExecutablePath,
            Arguments = Arguments,
            CloseOnSwitchAway = CloseOnSwitchAway
        };
    }
}
