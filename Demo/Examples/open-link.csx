var receipt = await NotificationClient.SendAsync(new() { Title = "Open a web page", Actions = [new OpenUriAction(new Uri("https://example.com")) { Label = "Open link" }], Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
