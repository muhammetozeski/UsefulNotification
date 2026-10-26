var theme = NotificationThemes.Dark with
{
    Frame = NotificationThemes.Dark.Frame with
    {
        Pattern = BorderPattern.Dashed,
        AccentEdges = BorderEdges.Top | BorderEdges.Left,
        AccentColor = Color.SteelBlue,
        CornerRadius = 8,
        Thickness = new() { Top = 3, Right = 1, Bottom = 1, Left = 5 }
    }
};
var receipt = await NotificationClient.SendAsync(new() { Title = "Custom frame", Message = "Dashed top and left accents.", Theme = theme, Lifetime = NotificationLifetimes.UntilAcknowledged, Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
