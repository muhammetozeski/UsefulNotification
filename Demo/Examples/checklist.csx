var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Release checklist",
    Checklist = [new() { Id = "build", Text = "Build completed", Checked = true }, new() { Id = "review", Text = "Review completed" }, new() { Id = "publish", Text = "Package published" }],
    Actions = [new ReplyAction { Label = "Confirm" }], Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
