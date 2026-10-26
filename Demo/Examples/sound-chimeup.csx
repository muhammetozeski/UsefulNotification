var receipt = await NotificationClient.SendAsync(new() { Title = "ChimeUp sound", Message = "Listen to the selected preset.", Sound = NotificationSounds.ChimeUp, Silent = false, Lifetime = new ExpiringLifetime(TimeSpan.FromSeconds(3)) }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
