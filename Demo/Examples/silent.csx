var receipt = await NotificationClient.SendAsync(new() { Title = "Silent notification", Sound = NotificationSounds.HighUrgency, Silent = true, Lifetime = new ExpiringLifetime(TimeSpan.FromSeconds(3)) }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
