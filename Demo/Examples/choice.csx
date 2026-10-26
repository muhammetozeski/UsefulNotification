var receipt = await NotificationClient.SendAsync(new() { Title = "Choose a destination", Message = "The selected action is returned to the caller.", Actions = [new CallbackAction { Id = "local", Label = "Local" }, new CallbackAction { Id = "remote", Label = "Remote" }], Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
