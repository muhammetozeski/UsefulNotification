var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Acknowledge through a declared action",
    Message = "Press Acknowledge to complete this occurrence.",
    Actions = [new DismissAction { Id = "acknowledge", Label = "Acknowledge" }],
    Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
