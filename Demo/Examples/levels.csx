foreach (NotificationLevel level in Enum.GetValues<NotificationLevel>())
{
    Print(await NotificationClient.SendAsync(new()
    {
        Title = level.ToString(), Message = "Inspect the semantic level accent.",
        Level = level, Lifetime = new ExpiringLifetime(TimeSpan.FromSeconds(3)), Silent = true
    }, cancellationToken: token));
}
