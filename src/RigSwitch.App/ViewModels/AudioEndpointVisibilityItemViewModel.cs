namespace RigSwitch.App.ViewModels;

/// <summary>
/// View model representing an audio endpoint with a real-time visibility toggle.
/// </summary>
public sealed class AudioEndpointVisibilityItemViewModel : ViewModelBase
{
    private bool _isVisible;
    private readonly Func<AudioEndpointVisibilityItemViewModel, bool, Task>? _onVisibilityChanged;

    /// <summary>
    /// Gets the unique MMDevice GUID identifier of the audio endpoint.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the friendly product name of the audio endpoint.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the driver or adapter name of the audio endpoint.
    /// </summary>
    public string Adapter { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the audio endpoint is visible and enabled in Windows.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                if (_onVisibilityChanged != null)
                {
                    _ = _onVisibilityChanged(this, value);
                }
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioEndpointVisibilityItemViewModel"/> class.
    /// </summary>
    /// <param name="id">The MMDevice endpoint GUID identifier.</param>
    /// <param name="name">The friendly name of the endpoint.</param>
    /// <param name="adapter">The adapter description.</param>
    /// <param name="isVisible">Initial visibility state.</param>
    /// <param name="onVisibilityChanged">Callback invoked when visibility toggles.</param>
    public AudioEndpointVisibilityItemViewModel(
        string id,
        string name,
        string adapter,
        bool isVisible,
        Func<AudioEndpointVisibilityItemViewModel, bool, Task>? onVisibilityChanged = null)
    {
        Id = id;
        Name = name;
        Adapter = adapter;
        _isVisible = isVisible;
        _onVisibilityChanged = onVisibilityChanged;
    }
}
