using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Runtime.InteropServices;

namespace Whirlwind
{
    [ComVisible(true)]
    [Guid("D1A1A1A1-1111-2222-3333-444444444444")]
    public class MyNotificationActivator : NotificationActivator
    {
        public override void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId)
        {
            // обработка клика по уведомлению
        }
    }
}
