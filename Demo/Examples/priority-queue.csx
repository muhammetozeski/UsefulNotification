foreach (NotificationPriority priority in Enum.GetValues<NotificationPriority>())
{
    for (int copy = 1; copy <= 2; copy++)
        Print(await NotificationClient.SendAsync(new()
        {
            Title = $"{priority} / {copy}", Message = "Dismiss cards to promote queued notifications.",
            Priority = priority, Lifetime = NotificationLifetimes.UntilAcknowledged, Silent = true
        }, cancellationToken: token));
}
Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.List }, cancellationToken: token));
