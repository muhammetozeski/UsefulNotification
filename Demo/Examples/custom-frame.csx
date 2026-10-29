var theme = new NotificationTheme
{
    Id = "demo-build", Name = "Build review", Parent = NotificationThemes.Attention,
    Overrides = new()
    {
        Frame = new()
        {
            Pattern = new(BorderPattern.Dashed),
            AccentEdges = new(BorderEdges.All),
            AccentColor = new(Color.SteelBlue),
            CornerRadius = new(8)
        },
        Sound = new(null)
    }
};
Print(ThemeResolver.ResolveTheme(theme));
var receipt = await NotificationClient.SendAsync(new() { Title = "Inherited custom theme", Message = "Attention supplies delivery and typography; this theme changes only its frame and sound.", Theme = theme }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));