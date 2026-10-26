Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Shutdown }, cancellationToken: token));
