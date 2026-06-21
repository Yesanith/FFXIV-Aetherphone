namespace Aetherphone.Core.Notifications;

internal interface IToast
{
    void Show(string title, string message);
}
