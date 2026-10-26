var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Review required",
    Message = "Dismiss this card when you have read it.",
    Lifetime = NotificationLifetimes.UntilAcknowledged,
    Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
