using System.Numerics;

namespace Aetherphone.Core.Notifications;

internal sealed record PhoneNotification(string AppId, string Title, string Body, DateTime ReceivedAt, Vector4 Accent);
