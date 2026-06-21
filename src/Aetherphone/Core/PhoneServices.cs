using Aetherphone.Core.Game;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core;

internal sealed class PhoneServices : IDisposable
{
    public Configuration Configuration { get; }

    public ThemeProvider Themes { get; }

    public GameData GameData { get; }

    public ITextureProvider Textures { get; }

    public WeatherService Weather { get; }

    public NotificationService Notifications { get; }

    public IRingtone Ringtone { get; }

    public MessageStore Messages { get; }

    public ChatBridge ChatBridge { get; }

    public MessageLauncher MessageLauncher { get; }

    private PhoneServices(Configuration configuration, ThemeProvider themes, GameData gameData, ITextureProvider textures, WeatherService weather, NotificationService notifications, IRingtone ringtone, MessageStore messages, ChatBridge chatBridge, MessageLauncher messageLauncher)
    {
        Configuration = configuration;
        Themes = themes;
        GameData = gameData;
        Textures = textures;
        Weather = weather;
        Notifications = notifications;
        Ringtone = ringtone;
        Messages = messages;
        ChatBridge = chatBridge;
        MessageLauncher = messageLauncher;
    }

    public static PhoneServices Build(Configuration configuration, INotificationManager notificationManager, IChatGui chatGui, IDataManager dataManager, IObjectTable objectTable, IClientState clientState, ITextureProvider textures)
    {
        var themes = new ThemeProvider(configuration);
        var gameData = new GameData(dataManager, objectTable);
        var weather = new WeatherService(dataManager, clientState);
        var toast = new DalamudToast(notificationManager);
        var ringtone = new GameSoundRingtone(configuration);
        var notifications = new NotificationService(toast, ringtone, configuration);
        var messages = new MessageStore();
        var chatBridge = new ChatBridge(messages, notifications, chatGui, gameData);
        var messageLauncher = new MessageLauncher();
        return new PhoneServices(configuration, themes, gameData, textures, weather, notifications, ringtone, messages, chatBridge, messageLauncher);
    }

    public void Dispose() => ChatBridge.Dispose();
}
