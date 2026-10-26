var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Repeated reminder", Message = "Dismiss the first card to inspect the next due time.",
    Schedule = new() { After = TimeSpan.FromSeconds(3), Recurrence = NotificationRecurrences.Yearly }, Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted)
{
    Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Get, Id = receipt.Id }, cancellationToken: token));
}
