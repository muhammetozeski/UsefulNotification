using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using Polly;

namespace UsefulNotification;

/// <summary>Sends optional notification requests and management operations to the local application.</summary>
public static class NotificationClient
{
    const bool IsLogEnabled = true;

    /// <summary>Sends a notification, or another operation, and waits for persisted acceptance.</summary>
    /// <param name="notification">Optional content; null creates an ordinary information notification.</param>
    /// <param name="operation">The requested operation; Send is the default.</param>
    /// <param name="id">Existing notification identity for management operations.</param>
    /// <param name="delay">Snooze duration for a management operation.</param>
    /// <param name="order">Target position for a manual-order operation.</param>
    /// <param name="startApplication">Whether to launch the adjacent application if the pipe is unavailable.</param>
    /// <param name="cancellationToken">Cancels connection and communication.</param>
    /// <returns>The server acknowledgement or a rejected response.</returns>
    public static async Task<NotificationResponse> SendAsync(NotificationRequest? notification = null,
        NotificationOperation operation = NotificationOperation.Send, Guid? id = null, TimeSpan? delay = null, int? order = null,
        bool startApplication = true, CancellationToken cancellationToken = default)
    {
        var command = new NotificationCommand { Operation = operation, Id = id, Delay = delay, Order = order, Notification = notification };
        if (operation == NotificationOperation.Send)
            command.Notification = (notification ?? new()) with { Id = notification?.Id ?? Guid.NewGuid(), Source = notification?.Source ?? Assembly.GetEntryAssembly()?.GetName().Name ?? AppConstants.Product.Name };
        using var pipe = new NamedPipeClientStream(".", AppConstants.Transport.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        Log($"Connecting; pipe={AppConstants.Transport.PipeName}", PrintToConsole: false, isRun: IsLogEnabled);
        try { await pipe.ConnectAsync(AppConstants.Transport.ConnectTimeout, cancellationToken); }
        catch (TimeoutException) when (startApplication)
        {
            string executable = Path.Combine(AppPaths.PayloadDirectory, AppConstants.Product.Executable);
            if (!File.Exists(executable)) throw new FileNotFoundException("Start UsefulNotification before sending from an external SDK application.", executable);
            using Process? host = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--background" } });
            await pipe.ConnectAsync(AppConstants.Transport.StartupTimeout, cancellationToken);
        }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(AppConstants.Timeout.Timeout);
        using var writer = new StreamWriter(pipe, AppConstants.Transport.Encoding, leaveOpen: true);
        using var reader = new StreamReader(pipe, AppConstants.Transport.Encoding, leaveOpen: true);
        string payload = JsonSerializer.Serialize(command, AppConstants.WireJson);
        if (payload.Length > AppConstants.Transport.MaximumMessageCharacters) throw new ArgumentException("The request exceeds the configured channel limit.", nameof(notification));
        Log($"Request; operation={operation}; id={command.Notification?.Id ?? id}; characters={payload.Length}", PrintToConsole: false, isRun: IsLogEnabled);
        await writer.WriteLineAsync(payload.AsMemory(), budget.Token);
        await writer.FlushAsync(budget.Token);
        string response = await reader.ReadLineAsync(budget.Token).AsTask().WaitAsync(budget.Token) ?? throw new IOException("The application closed the response channel.");
        return JsonSerializer.Deserialize<NotificationResponse>(response, AppConstants.Json) ?? throw new InvalidDataException("The application returned an empty response.");
    }

    /// <summary>Waits for user acknowledgement while keeping the original request id stable.</summary>
    /// <param name="id">The identity returned by SendAsync.</param>
    /// <param name="cancellationToken">Stops waiting without dismissing the notification.</param>
    /// <returns>The user result or final automatic expiry.</returns>
    public static async Task<NotificationResult> WaitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            NotificationResponse response = await SendAsync(operation: NotificationOperation.Get, id: id, startApplication: false, cancellationToken: cancellationToken);
            NotificationEntry entry = response.Entries?.SingleOrDefault() ?? throw new KeyNotFoundException(id.ToString());
            if (entry.Result is not null) return entry.Result;
            await Task.Delay(AppConstants.Transport.PollInterval, cancellationToken);
        }
    }
}
