namespace RigSwitch.Infrastructure.Storage;

using System.Diagnostics;
using System.Text.Json;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Provides JSON-based, atomic, thread-safe persistence for RigSwitch user configuration.
/// </summary>
public sealed class JsonSettingsStorageService : ISettingsStorageService, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private bool _disposed;

    /// <summary>
    /// Gets the absolute file path to the persisted settings JSON file.
    /// </summary>
    public string SettingsFilePath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSettingsStorageService"/> class.
    /// </summary>
    /// <param name="settingsFilePath">
    /// Optional path to the settings file. If null, empty, or whitespace, defaults to
    /// <c>%APPDATA%\RigSwitch\settings.json</c>.
    /// </param>
    public JsonSettingsStorageService(string? settingsFilePath = null)
    {
        SettingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RigSwitch", "settings.json")
            : settingsFilePath;
    }

    /// <inheritdoc/>
    public async Task<UserSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(SettingsFilePath))
            {
                return new UserSettings();
            }

            try
            {
                await using var stream = new FileStream(
                    SettingsFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);

                var loadedSettings = await JsonSerializer.DeserializeAsync<UserSettings>(
                    stream,
                    _serializerOptions,
                    cancellationToken).ConfigureAwait(false);

                return loadedSettings ?? new UserSettings();
            }
            catch (JsonException ex)
            {
                Trace.TraceWarning(
                    "Settings file '{0}' contains corrupt or invalid JSON. Falling back to default settings. Details: {1}",
                    SettingsFilePath,
                    ex.Message);

                return new UserSettings();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempFilePath = $"{SettingsFilePath}.tmp";

            try
            {
                await using (var stream = new FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        settings,
                        _serializerOptions,
                        cancellationToken).ConfigureAwait(false);

                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(tempFilePath, SettingsFilePath, overwrite: true);
            }
            catch
            {
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Best-effort cleanup of temporary file
                    }
                }

                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Releases unmanaged and managed resources used by the <see cref="JsonSettingsStorageService"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _semaphore.Dispose();
    }
}
