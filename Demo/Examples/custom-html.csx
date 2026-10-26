var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Report review", Message = "Enter the review note in the custom card.",
    Template = new(new FileInfo(Path.Combine(assets.FullName, "reply-card.html"))),
    Data = new() { ["report"] = "Quarterly report" }, Group = "Reports", Tag = Guid.NewGuid().ToString(),
    Input = new() { Required = true }, Actions = [new ReplyAction { Id = "submit", Label = "Submit" }], Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
