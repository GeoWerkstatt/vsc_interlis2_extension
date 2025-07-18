using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Cache;

/// <summary>
/// Stores the content of the currently opened INTERLIS files in memory.
/// </summary>
public sealed class FileContentCache : ICache<string>
{
    /// <inheritdoc />
    public event Action<DocumentUri>? DocumentInvalidated;

    private readonly ConcurrentDictionary<string, string> buffers = new();

    /// <summary>
    /// Update the buffer for the given document.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the file.</param>
    /// <param name="buffer">The file content.</param>
    public void UpdateBuffer(DocumentUri uri, string buffer)
    {
        buffers.AddOrUpdate(uri.ToString(), buffer, (k, v) => buffer);
        DocumentInvalidated?.Invoke(uri);
    }

    /// <summary>
    /// Get the buffer for the given document.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the file.</param>
    /// <returns>The file content if the file exists, or an empty string otherwise.</returns>
    public ValueTask<string> GetAsync(DocumentUri uri)
    {
        var content = buffers.TryGetValue(uri.ToString(), out var buffer) ? buffer : string.Empty;
        return new ValueTask<string>(content);
    }

    /// <summary>
    /// Remove the buffer for the given document.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the file.</param>
    public void ClearBuffer(DocumentUri uri)
    {
        buffers.TryRemove(uri.ToString(), out _);
        DocumentInvalidated?.Invoke(uri);
    }
}
