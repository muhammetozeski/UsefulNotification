var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Per-send inherited overrides",
    Message = "Only the accent, text sizes and spacing differ from Success.",
    Theme = NotificationThemes.Success,
    Overrides = new()
    {
        Palette = new() { Accent = new(Color.DarkSlateBlue) },
        Typography = new() { CardTitle = new(20), Body = new(14) },
        Layout = new() { Padding = new(16) },
        Lifetime = new(NotificationLifetimes.UntilAcknowledged),
        Sound = new(null)
    }
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));