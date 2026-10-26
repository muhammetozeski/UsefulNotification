var sound = new SoundPattern { Name = "Local WAV", AudioFile = new FileInfo(Path.Combine(assets.FullName, "sample.wav")) };
var receipt = await NotificationClient.SendAsync(new() { Title = "Local audio file", Sound = sound, Silent = false, Lifetime = new ExpiringLifetime(TimeSpan.FromSeconds(3)) }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
