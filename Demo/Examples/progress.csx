var receipt = await NotificationClient.SendAsync(new() { Title = "Copying files", Progress = new() { Value = 0, Maximum = 1000, Label = "Files copied" }, Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted)
{
    foreach (double value in new double[] { 100, 300, 600, 1000 })
    {
        await Task.Delay(TimeSpan.FromMilliseconds(650), token);
        Print(await NotificationClient.UpdateAsync(receipt.NotificationId, new() { Progress = new() { Value = new(value) } }, cancellationToken: token));
    }
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Get, Id = receipt.Id }, cancellationToken: token));
    Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
}
