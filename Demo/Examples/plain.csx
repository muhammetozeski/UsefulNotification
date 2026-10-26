var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Backup completed",
    Message = "12 files were copied to the destination.",
    Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
