var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Three-second notification",
    Message = "Keep the pointer away to let the lifetime expire.",
    Lifetime = new ExpiringLifetime(TimeSpan.FromSeconds(3)),
    Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
