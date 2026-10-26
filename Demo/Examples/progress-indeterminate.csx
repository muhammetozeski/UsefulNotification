var receipt = await NotificationClient.SendAsync(new() { Title = "Preparing download", Progress = new() { Value = null, Label = "Connecting" }, Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted)
{
    await Task.Delay(TimeSpan.FromSeconds(2), token);
    Print(await NotificationClient.UpdateAsync(receipt.NotificationId, new() { Progress = new() { Value = new(50), Label = new("Receiving data") } }, cancellationToken: token));
    Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
}
