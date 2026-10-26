var relative = await NotificationClient.SendAsync(new() { Title = "After three seconds", Schedule = new() { After = TimeSpan.FromSeconds(3) }, Silent = true }, cancellationToken: token);
Print(relative);
var absolute = await NotificationClient.SendAsync(new() { Title = "At a specified time", Schedule = new() { At = DateTimeOffset.Now.AddSeconds(5) }, Silent = true }, cancellationToken: token);
Print(absolute);
if (relative.Accepted) Print(await NotificationClient.WaitAsync(relative, cancellationToken: token));
if (absolute.Accepted) Print(await NotificationClient.WaitAsync(absolute, cancellationToken: token));
