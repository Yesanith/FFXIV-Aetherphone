namespace Aetherphone.Core.Notifications;

internal sealed class NotificationService
{
    private const int MaxRetained = 50;

    private readonly IToast toast;
    private readonly IRingtone ringtone;
    private readonly Configuration configuration;
    private readonly List<PhoneNotification> recent = new();

    public int UnreadCount { get; private set; }

    public IReadOnlyList<PhoneNotification> Recent => recent;

    public event Action? Changed;

    public NotificationService(IToast toast, IRingtone ringtone, Configuration configuration)
    {
        this.toast = toast;
        this.ringtone = ringtone;
        this.configuration = configuration;
    }

    public void Notify(PhoneNotification notification)
    {
        recent.Add(notification);
        if (recent.Count > MaxRetained)
        {
            recent.RemoveAt(0);
        }

        UnreadCount++;

        if (!configuration.DoNotDisturb)
        {
            toast.Show(notification.Title, notification.Body);
            ringtone.Play();
        }

        Changed?.Invoke();
    }

    public void MarkAllRead()
    {
        if (UnreadCount == 0)
        {
            return;
        }

        UnreadCount = 0;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (recent.Count == 0 && UnreadCount == 0)
        {
            return;
        }

        recent.Clear();
        UnreadCount = 0;
        Changed?.Invoke();
    }
}
