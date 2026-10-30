using System.ComponentModel;
using System.Text.Json;

namespace UsefulNotification;

/// <summary>Names the existing Codex conversation that receives a completed notification's JSON result.</summary>
/// <param name="ThreadId">Nonempty conversation identity, independent of notification and occurrence identities.</param>
public sealed record CodexThreadTarget(Guid ThreadId);

/// <summary>Retains a result-message attempt in the notification journal. An uncertain started attempt is never automatically sent twice.</summary>
/// <param name="Target">Destination captured when this occurrence completed.</param>
public sealed record LlmResultDelivery(CodexThreadTarget Target)
{
    /// <summary>Durably recorded immediately before starting Codex; null means the attempt has not started.</summary>
    public DateTimeOffset? AttemptedAt { get; set; }
    /// <summary>Time Codex confirmed queue acceptance, or null if acceptance was not confirmed.</summary>
    public DateTimeOffset? QueuedAt { get; set; }
    /// <summary>Technical failure retained for inspection; notification completion itself remains unchanged.</summary>
    public string? Error { get; set; }
}

/// <summary>Owns Codex executable discovery and the bounded, hidden native queue process configuration.</summary>
internal sealed class CodexMessagingOptions
{
    public string Provider { get; set; } = "Codex";
    public DirectoryInfo BinDirectory { get; set; } = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin"));
    public ProcessStartInfo Process { get; set; } = new("codex.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
}

/// <summary>Delivers one stored occurrence result through Codex's native queue command without a shell or helper script.</summary>
internal static class CodexResultNotifier
{
    const bool IsLogEnabled = true;

    /// <summary>Finds the installed Codex CLI, queues the complete result in the existing conversation, and checks its process exit code.</summary>
    /// <param name="target">Validated existing Codex conversation.</param>
    /// <param name="result">Already persisted completion, including both identities, action, reply and checklist selections.</param>
    /// <param name="cancellationToken">Host lifetime cancellation; expiry terminates only the owned queue process.</param>
    /// <returns>Completes after confirmed queue acceptance; throws on unavailable Codex, cancellation or a rejected queue command.</returns>
    public static async Task SendAsync(CodexThreadTarget target, NotificationResult result, CancellationToken cancellationToken = default)
    {
        if (target.ThreadId == Guid.Empty) throw new ArgumentException("A nonempty Codex thread identity is required.");
        var policy = AppConstants.CodexMessaging;
        var stopwatch = Stopwatch.StartNew();
        string payload = JsonSerializer.Serialize(result, AppConstants.WireJson);
        string message = $"UsefulNotification result:\n{payload}";
        var candidates = new List<string>();
        if (Path.IsPathRooted(policy.Process.FileName)) candidates.Add(policy.Process.FileName);
        else
        {
            try
            {
                if (policy.BinDirectory.Exists)
                    candidates.AddRange(policy.BinDirectory.EnumerateDirectories().Select(directory => Path.Combine(directory.FullName, policy.Process.FileName))
                        .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Log($"Codex discovery failed; trying PATH: {error}", PrintToConsole: false, isRun: IsLogEnabled); }
            candidates.Add(policy.Process.FileName);
        }
        try
        {
            foreach (string executable in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                using var process = new Process
                {
                    StartInfo = new(executable)
                    {
                        UseShellExecute = policy.Process.UseShellExecute, CreateNoWindow = policy.Process.CreateNoWindow,
                        RedirectStandardOutput = policy.Process.RedirectStandardOutput, RedirectStandardError = policy.Process.RedirectStandardError
                    }
                };
                foreach (string argument in new[] { "queue", "--thread", target.ThreadId.ToString(), "--message", message }) process.StartInfo.ArgumentList.Add(argument);
                Log($"Codex queue start; executable={executable}; thread={target.ThreadId}; result={payload}", PrintToConsole: false, isRun: IsLogEnabled);
                try { if (!process.Start()) throw new IOException("Codex did not start."); }
                catch (Win32Exception error) when (error.NativeErrorCode is 2 or 3 && executable != candidates[^1])
                { Log($"Codex candidate disappeared; trying the next executable: {error}", PrintToConsole: false, isRun: IsLogEnabled); continue; }
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                budget.CancelAfter(policy.Timeout);
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                try
                {
                    await process.WaitForExitAsync(budget.Token);
                    string stdout = await output; string stderr = await errors;
                    Log($"Codex queue completed; thread={target.ThreadId}; exit={process.ExitCode}; stdout={stdout}; stderr={stderr}", PrintToConsole: false, isRun: IsLogEnabled);
                    if (process.ExitCode != 0) throw new IOException($"Codex queue rejected the result; exit={process.ExitCode}; stderr={stderr}; stdout={stdout}");
                    return;
                }
                finally
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                    await Task.WhenAll(output, errors);
                }
            }
            throw new FileNotFoundException("Codex was not found in its installation directory or PATH.");
        }
        finally { Log($"Codex result delivery finished; thread={target.ThreadId}; occurrence={result.OccurrenceId}; elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}", PrintToConsole: false, isRun: IsLogEnabled); }
    }
}
