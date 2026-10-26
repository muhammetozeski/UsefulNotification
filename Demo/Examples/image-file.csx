var receipt = await NotificationClient.SendAsync(new() { Title = "Local image", Message = "This image is shipped with the demo.", Image = new NotificationImage(new FileInfo(Path.Combine(assets.FullName, "sample.svg"))), Lifetime = NotificationLifetimes.UntilAcknowledged, Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
