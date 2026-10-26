var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Defer through a declared action",
    Message = "Press Remind in ten seconds, then inspect the next occurrence.",
    Actions = [new SnoozeAction(TimeSpan.FromSeconds(10)) { Id = "defer", Label = "Remind in ten seconds" }],
    Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted)
{
    Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Get, Id = receipt.Id }, cancellationToken: token));
}
