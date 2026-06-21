namespace Aetherphone.Core.Messaging;

internal enum MessageDirection
{
    Incoming,
    Outgoing,
}

internal sealed record ChatLine(MessageDirection Direction, string Text, DateTime At);
