using FFXIVClientStructs.FFXIV.Client.UI;

namespace Aetherphone.Core.Notifications;

internal sealed class GameSoundRingtone : IRingtone
{
    private readonly Configuration configuration;

    public GameSoundRingtone(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Play()
    {
        var soundId = configuration.RingtoneId;
        if (soundId == 0)
        {
            return;
        }

        UIGlobals.PlayChatSoundEffect(soundId);
    }
}
