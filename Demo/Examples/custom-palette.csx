var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Per-request appearance",
    Message = "A custom palette, typography and spacing for this request.",
    Appearance = new()
    {
        Palette = NotificationThemes.Light.Palette with { Accent = Color.DarkSlateBlue },
        Typography = NotificationThemes.Light.Typography with { CardTitle = 20, Body = 14 },
        Layout = NotificationThemes.Light.Layout with { Padding = 16 }
    },
    Lifetime = NotificationLifetimes.UntilAcknowledged,
    Silent = true
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
