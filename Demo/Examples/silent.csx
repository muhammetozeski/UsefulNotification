var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Silent success",
    Message = "Success keeps its colors and delivery; this send explicitly overrides its sound with null.",
    Theme = NotificationThemes.Success,
    Overrides = new() { Sound = new(null) }
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));