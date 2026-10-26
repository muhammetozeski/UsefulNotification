var receipt = await NotificationClient.SendAsync(new() { Title = "Cancel only the wait", Lifetime = NotificationLifetimes.UntilAcknowledged, Silent = true }, cancellationToken: token); Print(receipt);
if (receipt.Accepted)
{
    using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token);
    waiting.CancelAfter(TimeSpan.FromSeconds(3));
    try { Print(await NotificationClient.WaitAsync(receipt, cancellationToken: waiting.Token)); }
    catch (OperationCanceledException) when (!token.IsCancellationRequested) { Print("Waiting canceled; the notification is still active."); }
    Print(await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Get, Id = receipt.Id }, cancellationToken: token));
}
