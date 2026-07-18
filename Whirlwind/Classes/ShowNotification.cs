using Microsoft.Toolkit.Uwp.Notifications;
using NAudio.CoreAudioApi;
using Windows.UI.Notifications;

namespace Whirlwind.Classes
{
    internal static class ShowNotification
    {
        public static void ShowToast(string name, string message, Views.DeviceItem device, sbyte muted = -1)
        {
            if (muted == 3) return;

            if (muted != 1)
            {
                PlaySounds.PlayNotificationSound();
            }
            if (muted != 2) {
                var content = new ToastContentBuilder()
                    .AddText(name)
                    .AddText(message)
                    .AddArgument("action", "open")
                    .AddArgument("deviceId", device.Id)
                    .GetToastContent();

                var toast = new ToastNotification(content.GetXml())
                {
                    Tag = "whirlwind",
                    Group = "main"
                };

                var notifier = ToastNotificationManager.CreateToastNotifier("Whirlwind.App");

                notifier.Show(toast);
            }
        }
    }
}
