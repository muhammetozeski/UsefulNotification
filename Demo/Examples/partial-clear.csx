var receipt = await NotificationClient.SendAsync(new() { Title = "Partial update", Message = "The action and progress will be removed.", Actions = [new CallbackAction { Label = "Temporary button" }], Progress = new() { Value = 20, Maximum = 1000 }, Lifetime = NotificationLifetimes.UntilAcknowledged, Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted)
{
    await Task.Delay(TimeSpan.FromSeconds(2), token);
    Print(await NotificationClient.UpdateAsync(receipt.NotificationId, new() { Message = new("Only the message changed; optional content was cleared."), Actions = new([]), ProgressContent = new(null), Schedule = new(null) }, cancellationToken: token));
    Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
}
