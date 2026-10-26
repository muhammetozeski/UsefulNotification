var request = new NotificationRequest { Id = Guid.NewGuid(), Title = "Original content", Tag = Guid.NewGuid().ToString(), Lifetime = NotificationLifetimes.UntilAcknowledged, Silent = true };
var first = await NotificationClient.SendAsync(request, cancellationToken: token); Print(first);
var repeated = await NotificationClient.SendAsync(request, cancellationToken: token); Print(repeated);
Print(new { SameSeries = first.Id == repeated.Id, SameOccurrence = first.OccurrenceId == repeated.OccurrenceId });
await Task.Delay(TimeSpan.FromSeconds(2), token);
var replacement = await NotificationClient.SendAsync(request with { Id = Guid.NewGuid(), Title = "Updated through the same tag" }, cancellationToken: token); Print(replacement);
if (replacement.Accepted) Print(await NotificationClient.WaitAsync(replacement, cancellationToken: token));
