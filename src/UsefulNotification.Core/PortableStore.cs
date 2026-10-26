using System.Text.Json;
using Polly;

namespace UsefulNotification;

/// <summary>Reads portable JSON with backup recovery and commits replacements without exposing partial documents.</summary>
internal static class PortableStore
{
    const bool IsLogEnabled = true;

    /// <summary>Reads a document or its last valid backup; an absent document uses the supplied default.</summary>
    /// <typeparam name="T">Stored document type.</typeparam>
    /// <param name="name">File name below UserData.</param>
    /// <param name="fallback">Value for a document that has never been created.</param>
    /// <returns>The loaded value; corruption without a valid backup remains an error.</returns>
    public static T Read<T>(string name, T fallback)
    {
        string path = Path.Combine(AppPaths.UserData, name);
        if (!File.Exists(path)) return fallback;
        try { return NotificationProtocol.ReadStored<T>(File.ReadAllText(path)) ?? throw new InvalidDataException(path); }
        catch (Exception first) when (first is JsonException or IOException or InvalidDataException)
        {
            Log($"Recovery requested; path={path}; error={first}", PrintToConsole: false, isRun: IsLogEnabled);
            try
            {
                T restored = NotificationProtocol.ReadStored<T>(File.ReadAllText(path + AppConstants.Storage.BackupExtension)) ?? throw new InvalidDataException(path);
                File.Copy(path, path + DateTimeOffset.Now.ToUnixTimeMilliseconds() + AppConstants.Storage.UnreadableExtension, true);
                Write(name, restored);
                return restored;
            }
            catch (Exception backup) { throw new AggregateException("The document and its backup could not be recovered.", first, backup); }
        }
    }

    /// <summary>Writes a uniquely staged document, retains the preceding version, then atomically replaces the destination.</summary>
    /// <typeparam name="T">Stored document type.</typeparam>
    /// <param name="name">File name below UserData.</param>
    /// <param name="document">The complete snapshot to commit.</param>
    public static void Write<T>(string name, T document)
    {
        Directory.CreateDirectory(AppPaths.UserData);
        string path = Path.Combine(AppPaths.UserData, name);
        string staging = path + Guid.NewGuid().ToString("N") + AppConstants.Storage.TemporaryExtension;
        try
        {
            File.WriteAllText(staging, JsonSerializer.Serialize(document, AppConstants.StorageJson));
            new ResiliencePipelineBuilder().AddRetry(AppConstants.Retry).Build().Execute(() =>
            {
                if (File.Exists(path)) File.Replace(staging, path, path + AppConstants.Storage.BackupExtension);
                else File.Move(staging, path);
            });
            Log($"Document committed; path={path}", PrintToConsole: false, isRun: IsLogEnabled);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }
}
