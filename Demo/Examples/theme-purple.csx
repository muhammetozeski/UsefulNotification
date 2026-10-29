var theme = NotificationThemes.Urgent;
Print(ThemeResolver.ResolveTheme(theme));
var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Urgent theme",
    Message = "The selected theme supplies colors, sound, lifetime and queue priority together.",
    Theme = theme
}, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));