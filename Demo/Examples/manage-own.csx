var receipt = await NotificationClient.SendAsync(new() { Title = "Managed demo card", Lifetime = NotificationLifetimes.UntilAcknowledged, Silent = true }, cancellationToken: token); Print(receipt);
if (receipt.Accepted)
{
    var current = await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Get, Id = receipt.Id }, cancellationToken: token); Print(current);
    if (current.Entries?.SingleOrDefault() is { } entry) Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Replace, Id = receipt.Id, Notification = entry.Request with { Message = "Complete request replaced; identity retained." } }, cancellationToken: token));
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Move, Id = receipt.Id, Order = 0 }, cancellationToken: token));
    await Task.Delay(TimeSpan.FromSeconds(1), token);
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Dismiss, Id = receipt.Id, OccurrenceId = receipt.OccurrenceId }, cancellationToken: token));
    var retry = await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Retry, Id = receipt.Id }, cancellationToken: token); Print(retry);
    await Task.Delay(TimeSpan.FromSeconds(1), token);
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Snooze, Id = receipt.Id, OccurrenceId = retry.OccurrenceId, Delay = TimeSpan.FromSeconds(10) }, cancellationToken: token));
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.ClearHistory, Id = receipt.Id }, cancellationToken: token));
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Get, Id = receipt.Id }, cancellationToken: token));
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Delete, Id = receipt.Id }, cancellationToken: token));
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.List }, cancellationToken: token));
}
