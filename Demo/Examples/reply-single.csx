var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Enter a note", Message = "Submit an empty reply first, then enter a note.",
    Input = new() { Placeholder = "Your note", Required = true, Multiline = false },
    Actions = [new ReplyAction { Id = "submit", Label = "Submit" }], Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
