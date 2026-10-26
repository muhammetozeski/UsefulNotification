var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Light theme",
    Message = "Inspect the frame, text and action colors, then dismiss.",
    Theme = NotificationThemes.Light,
    Lifetime = NotificationLifetimes.UntilAcknowledged,
    Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
