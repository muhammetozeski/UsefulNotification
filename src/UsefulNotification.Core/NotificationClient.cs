using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Polly;
using Polly.Retry;
using Polly.Timeout;
namespace UsefulNotification;

/// <summary>Owns actual client endpoint, startup, polling and Polly request policies.</summary>
public sealed record NotificationClientOptions
{
    /// <summary>Maximum response-line length in characters, initially 16 Mi characters so retained history can exceed one request. Increase for deliberately larger histories; inbound requests keep the host's separate limit.</summary>
    public int MaximumResponseCharacters { get; set; } = 16 * 1024 * 1024;
    /// <summary>Optional host executable location. Null resolves the payload beside this library; set it when integrating from another application folder.</summary>
    public FileInfo? Application { get; set; }
    /// <summary>True, initially, permits a background host launch after the initial connection times out.</summary>
    public bool StartApplication { get; set; } = true;
    /// <summary>Local named-pipe endpoint, initially the built-in host endpoint. A custom endpoint must match a listening host.</summary>
    public string PipeName { get; set; } = AppConstants.Transport.PipeName;
    /// <summary>Initial connection and retry-connection time limit as a duration; initially two seconds.</summary>
    public TimeSpan ConnectTimeout { get; set; } = AppConstants.Transport.ConnectTimeout;
    /// <summary>Connection time allowed after launching the host; initially twenty seconds.</summary>
    public TimeSpan StartupTimeout { get; set; } = AppConstants.Transport.StartupTimeout;
    /// <summary>Delay between result queries while waiting; initially 200 milliseconds.</summary>
    public TimeSpan PollInterval { get; set; } = AppConstants.Transport.PollInterval;
    /// <summary>Actual Polly request timeout options, initially four seconds across the request pipeline after the initial connection.</summary>
    public TimeoutStrategyOptions RequestTimeout { get; set; } = new() { Timeout = TimeSpan.FromSeconds(4) };
    /// <summary>Actual Polly retry options used only for Send, Get and List; initially two IO-failure retries with exponential delay starting at 120 milliseconds.</summary>
    public RetryStrategyOptions Retry { get; set; } = new()
    {
        MaxRetryAttempts = 2, Delay = TimeSpan.FromMilliseconds(120), BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = new PredicateBuilder().Handle<IOException>()
    };
}
/// <summary>Sends typed notifications and updates to the resident host. Defaults and presets are actual owned objects.</summary>
public static class NotificationClient
{
    const bool IsLogEnabled = true;
    /// <summary>Process-wide default client options. Individual calls may supply an independent options object; payload serialization occurs before connection awaits.</summary>
    public static NotificationClientOptions Options = new();

    /// <summary>Sends an optional notification; captures its full payload before the first await.</summary>
    /// <param name="notification">Complete request, or null for a plain notification with host defaults. Later edits do not alter the captured payload.</param>
    /// <param name="options">Per-call endpoint and policy options; null uses Options.</param>
    /// <param name="cancellationToken">Cancels startup, connection and request waits. A host may already have committed a request when its caller cancels.</param>
    /// <returns>Durable acceptance with series and occurrence identities, or a receipt containing a typed rejection.</returns>
    public static Task<NotificationReceipt> SendAsync(NotificationRequest? notification = null, NotificationClientOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = (notification ?? new()) with { Id = notification?.Id ?? Guid.NewGuid(), Source = notification?.Source ?? Assembly.GetEntryAssembly()?.GetName().Name ?? AppConstants.Product.Name };
        return ExecuteAsync(new() { Notification = request }, options, cancellationToken);
    }
    /// <summary>Updates supplied fields and preserves absent fields. A supplied Change with null Value clears a nullable field.</summary>
    /// <param name="id">Retained series identity to update.</param>
    /// <param name="update">Partial field changes, captured before the first connection await.</param>
    /// <param name="options">Per-call endpoint and policy options; null uses Options.</param>
    /// <param name="cancellationToken">Cancels the request wait without undoing a change already committed by the host.</param>
    /// <returns>The accepted occurrence identity or a typed rejection receipt.</returns>
    public static Task<NotificationReceipt> UpdateAsync(Guid id, NotificationUpdate update, NotificationClientOptions? options = null, CancellationToken cancellationToken = default) =>
        ExecuteAsync(new() { Operation = NotificationOperation.Update, Id = id, Update = update }, options, cancellationToken);

    /// <summary>Executes a typed management command and returns durable acceptance or a typed failure.</summary>
    /// <param name="command">Management operation and its required data; serialization captures the command before any connection await.</param>
    /// <param name="options">Per-call endpoint and policy options; null uses Options.</param>
    /// <param name="cancellationToken">Cancels connection and request waits; committed host changes retain their effects.</param>
    /// <returns>The operation's acknowledgement, optional query entries, or typed rejection.</returns>
    public static Task<NotificationReceipt> ExecuteAsync(NotificationCommand command, NotificationClientOptions? options = null, CancellationToken cancellationToken = default)
    {
        string payload = JsonSerializer.Serialize(command, AppConstants.WireJson);
        return ExchangeAsync(payload, command.Operation, options ?? Options, cancellationToken);
    }
    internal static Task<NotificationReceipt> SendWireAsync(string payload, NotificationOperation operation, CancellationToken cancellationToken = default) => ExchangeAsync(payload, operation, Options, cancellationToken);

    static async Task<NotificationReceipt> ExchangeAsync(string payload, NotificationOperation operation, NotificationClientOptions source, CancellationToken cancellationToken)
    {
        if (payload.Length > AppConstants.Transport.MaximumMessageCharacters) throw new ArgumentException("The request exceeds the channel limit.");
        var settings = source with { };
        var pipeline = new ResiliencePipelineBuilder().AddTimeout(source.RequestTimeout);
        if (operation is NotificationOperation.Send or NotificationOperation.Get or NotificationOperation.List) pipeline.AddRetry(source.Retry);
        var requests = pipeline.Build();
        NamedPipeClientStream? initial = new(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            try { await initial.ConnectAsync(settings.ConnectTimeout, cancellationToken); }
            catch (TimeoutException) when (settings.StartApplication)
            {
                string executable = settings.Application?.FullName ?? Path.Combine(AppPaths.PayloadDirectory, AppConstants.Product.Executable);
                if (!File.Exists(executable)) throw new FileNotFoundException("Start UsefulNotification or configure NotificationClient.Options.Application.", executable);
                using var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { AppConstants.Product.BackgroundArgument } });
                await initial.ConnectAsync(settings.StartupTimeout, cancellationToken);
            }
            return await requests.ExecuteAsync(async token =>
            {
                using var pipe = Interlocked.Exchange(ref initial, null) ?? new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                if (!pipe.IsConnected) await pipe.ConnectAsync(settings.ConnectTimeout, token);
                Log($"Request; operation={operation}; pipe={settings.PipeName}; characters={payload.Length}", PrintToConsole: false, isRun: IsLogEnabled);
                await NotificationProtocol.WriteLineAsync(pipe, payload, token);
                string text = await NotificationProtocol.ReadLineAsync(pipe, token, settings.MaximumResponseCharacters);
                var root = JsonNode.Parse(text) ?? throw new JsonException("An acknowledgement is required.");
                if (root["protocolVersion"]?.GetValue<int>() != 2)
                    return new NotificationReceipt { Failure = new(NotificationError.IncompatibleHost, "This client requires UsefulNotification 2 or later.") };
                return root.Deserialize<NotificationReceipt>(AppConstants.Json) ?? throw new JsonException("An acknowledgement is required.");
            }, cancellationToken);
        }
        finally { initial?.Dispose(); }
    }
    /// <summary>Waits for the exact display identified by a send receipt. Canceling the wait does not dismiss it.</summary>
    /// <param name="receipt">Accepted receipt containing both notification and occurrence identities.</param>
    /// <param name="options">Polling endpoint and interval; null uses Options. Waiting never starts a missing host.</param>
    /// <param name="cancellationToken">Stops polling without changing the notification.</param>
    /// <returns>The exact occurrence's persisted completion, including its reply, selected items and external action outcome.</returns>
    /// <exception cref="ArgumentException">The receipt has no accepted notification and occurrence identities.</exception>
    /// <exception cref="KeyNotFoundException">The targeted series or occurrence has been removed from retained history.</exception>
    public static Task<NotificationResult> WaitAsync(NotificationReceipt receipt, NotificationClientOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!receipt.Accepted || receipt.Id is null || receipt.OccurrenceId is null) throw new ArgumentException("An accepted display receipt is required.");
        return WaitAsync(receipt.NotificationId, receipt.OccurrenceId, options, cancellationToken);
    }
    /// <summary>Waits for the latest completed result of a series; prefer a receipt to target a particular display.</summary>
    /// <param name="id">Series identity whose latest retained completion is requested.</param>
    /// <param name="cancellationToken">Stops polling without changing the notification.</param>
    /// <returns>The latest retained completion, which can belong to an earlier display of a recurring series.</returns>
    public static Task<NotificationResult> WaitAsync(Guid id, CancellationToken cancellationToken = default) => WaitAsync(id, null, null, cancellationToken);

    static async Task<NotificationResult> WaitAsync(Guid id, Guid? occurrence, NotificationClientOptions? options, CancellationToken cancellationToken)
    {
        var settings = (options ?? Options) with { StartApplication = false };
        while (true)
        {
            var response = await ExecuteAsync(new() { Operation = NotificationOperation.Get, Id = id }, settings, cancellationToken);
            if (!response.Accepted) throw new IOException(response.Failure?.Message);
            var entry = response.Entries?.SingleOrDefault() ?? throw new KeyNotFoundException(id.ToString());
            var display = occurrence is { } key ? entry.Occurrences.SingleOrDefault(item => item.Id == key) ?? throw new KeyNotFoundException("The display result was removed from history.") : null;
            if ((display?.Result ?? (occurrence is null ? entry.Result : null)) is { } result) return result;
            await Task.Delay(settings.PollInterval, cancellationToken);
        }
    }
}
