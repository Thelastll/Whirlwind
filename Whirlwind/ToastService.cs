using Microsoft.Toolkit.Uwp.Notifications;

public static class ToastService
{
    public static void Show(string title, string message)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .Show();
    }
}
