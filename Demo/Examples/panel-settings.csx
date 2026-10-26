Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.ShowPanel }, cancellationToken: token));
Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.ReloadSettings }, cancellationToken: token));
