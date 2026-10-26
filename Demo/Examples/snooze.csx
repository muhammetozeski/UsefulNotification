var receipt = await NotificationClient.SendAsync(new() { Title = "Snooze this reminder", Schedule = new(), Snooze = [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1)], Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted)
{
    Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Get, Id = receipt.Id }, cancellationToken: token));
}
