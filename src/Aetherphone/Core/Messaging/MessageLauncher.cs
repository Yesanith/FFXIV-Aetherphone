namespace Aetherphone.Core.Messaging;

internal sealed class MessageLauncher
{
    private string? pendingDisplay;
    private string? pendingTarget;

    public void Request(string display, string sendTarget)
    {
        pendingDisplay = display;
        pendingTarget = sendTarget;
    }

    public bool TryConsume(out string display, out string sendTarget)
    {
        if (pendingTarget is null)
        {
            display = string.Empty;
            sendTarget = string.Empty;
            return false;
        }

        display = pendingDisplay!;
        sendTarget = pendingTarget;
        pendingDisplay = null;
        pendingTarget = null;
        return true;
    }
}
