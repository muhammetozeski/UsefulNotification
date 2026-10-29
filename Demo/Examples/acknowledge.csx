var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Review required",
    Message = "Click the card background to acknowledge, or use Snooze to postpone.",
    Theme = NotificationThemes.Reminder,
    Interaction = NotificationInteractions.ClickToAcknowledge,
    Overrides = new() { Sound = new(null) }
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));