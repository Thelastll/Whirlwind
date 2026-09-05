using Microsoft.Toolkit.Uwp.Notifications;
using NAudio.CoreAudioApi;
using Windows.UI.Notifications;

namespace Whirlwind.Classes
{
    internal static class ShowNotification
    {
        internal static void ShowToast(string message, Views.DeviceItem device, sbyte muted = -1)
        {
            if (muted == 3 || device.Ip == App.MainWindowInstance.CurrentInterlocutor) return;

            if (muted != 1)
            {
                PlaySounds.PlayNotificationSound();
            }
            if (muted != 2) {
                var content = new ToastContentBuilder()
                    .AddText(device.Name)
                    .AddText(message)
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
