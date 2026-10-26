var sound = new SoundPattern { Name = "Two short tones", Steps = [new(600, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(100)), new(900, TimeSpan.FromMilliseconds(180), TimeSpan.Zero)] };
var receipt = await NotificationClient.SendAsync(new() { Title = "Custom tones", Sound = sound, Silent = false, Lifetime = new ExpiringLifetime(TimeSpan.FromSeconds(3)) }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
