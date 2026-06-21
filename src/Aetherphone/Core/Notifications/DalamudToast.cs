using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class DalamudToast : IToast
{
    private readonly INotificationManager manager;

    public DalamudToast(INotificationManager manager)
    {
        this.manager = manager;
    }

    public void Show(string title, string message)
    {
        manager.AddNotification(new Notification
        {
            Title = title,
            Content = message,
            Type = NotificationType.Info,
        });
    }
}
