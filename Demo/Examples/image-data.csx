string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='280' height='90'><rect width='280' height='90' fill='#244566'/><text x='18' y='53' fill='white' font-family='Segoe UI' font-size='20'>Embedded image</text></svg>";
var uri = new Uri("data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)));
var receipt = await NotificationClient.SendAsync(new() { Title = "Embedded image", Image = new(uri), Lifetime = NotificationLifetimes.UntilAcknowledged, Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
