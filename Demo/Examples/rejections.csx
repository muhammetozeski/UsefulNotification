Print(await NotificationClient.SendAsync(new()
{
    Title = "Unsupported image scheme",
    Image = new(new Uri("ftp://example.com/picture.png")), Silent = true
}, cancellationToken: token));
Print(await NotificationClient.SendAsync(new()
{
    Title = "Duplicate checklist identities",
    Checklist = [new() { Id = "same", Text = "First" }, new() { Id = "same", Text = "Second" }], Silent = true
}, cancellationToken: token));
Print(await NotificationClient.UpdateAsync(Guid.NewGuid(), new() { Message = new("Unknown series") }, cancellationToken: token));