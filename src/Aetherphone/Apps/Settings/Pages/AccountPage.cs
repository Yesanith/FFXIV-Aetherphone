using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AccountPage : ISettingsPage, IDisposable
{
    private const string LodestoneProfileUrl = "https://na.finalfantasyxiv.com/lodestone/my/setting/profile/";

    public string Title => Loc.T(L.Account.Title);

    public string Summary => session.IsSignedIn ? session.CurrentUser?.DisplayName ?? Loc.T(L.Account.SignedIn) : Loc.T(L.Account.NotSignedIn);

    public string Glyph => "@";

    public Vector4 Tint => new(0.36f, 0.72f, 0.62f, 1f);

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly GameData gameData;
    private readonly CancellationTokenSource cancellation = new();

    private volatile string status = string.Empty;
    private volatile string code = string.Empty;
    private volatile string? challengeId;
    private volatile bool busy;
    private bool meRequested;

    public AccountPage(AethernetSession session, AethernetClient client, GameData gameData)
    {
        this.session = session;
        this.client = client;
        this.gameData = gameData;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            if (session.IsSignedIn)
            {
                DrawSignedIn(theme);
            }
            else
            {
                DrawSignedOut(theme);
            }
        }
    }

    private void DrawSignedIn(PhoneTheme theme)
    {
        if (session.CurrentUser is null && !meRequested && !busy)
        {
            meRequested = true;
            StartMe();
        }

        var user = session.CurrentUser;
        ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
        using (Plugin.Fonts.Push(1.4f))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextUnformatted(user?.DisplayName ?? Loc.T(L.Account.SignedIn));
            }
        }

        if (user is not null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextUnformatted($"{user.Name}@{user.World}");
                ImGui.TextUnformatted($"{Loc.Plural(L.Account.Followers, user.Followers)} · {Loc.Plural(L.Account.Following, user.Following)}");
            }
        }

        ImGui.Dummy(new Vector2(0f, 12f * ImGuiHelpers.GlobalScale));
        if (Button(Loc.T(L.Account.SignOut), theme))
        {
            session.SignOut();
            ResetFlow();
        }
    }

    private void DrawSignedOut(PhoneTheme theme)
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            Typography.DrawCentered(new Vector2(ImGui.GetContentRegionAvail().X * 0.5f + ImGui.GetCursorScreenPos().X, ImGui.GetCursorScreenPos().Y + 80f * ImGuiHelpers.GlobalScale), Loc.T(L.Account.LogInFirst), theme.TextMuted);
            return;
        }

        var name = player.Name.TextValue;
        var world = gameData.WorldName(gameData.LocalHomeWorldId);

        ImGui.Dummy(new Vector2(0f, 6f * ImGuiHelpers.GlobalScale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Account.SignInIntro));
        }

        ImGui.Dummy(new Vector2(0f, 4f * ImGuiHelpers.GlobalScale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextUnformatted($"{name}@{world}");
        }

        ImGui.Dummy(new Vector2(0f, 10f * ImGuiHelpers.GlobalScale));

        if (challengeId is null)
        {
            if (Button(Loc.T(L.Account.SignIn), theme) && !busy && name.Length > 0 && world.Length > 0)
            {
                StartChallenge(name, world);
            }
        }
        else
        {
            DrawVerifyStep(theme);
        }

        var message = status;
        if (message.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextWrapped(message);
            }
        }
    }

    private void DrawVerifyStep(PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextUnformatted(Loc.T(L.Account.AddCode));
        }

        using (Plugin.Fonts.Push(1.6f))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.Accent))
            {
                ImGui.TextUnformatted(code);
            }
        }

        ImGui.Dummy(new Vector2(0f, 6f * ImGuiHelpers.GlobalScale));
        if (Button(Loc.T(L.Account.CopyCode), theme))
        {
            ImGui.SetClipboardText(code);
        }

        if (Button(Loc.T(L.Account.OpenProfile), theme))
        {
            UrlActions.OpenInBrowser(LodestoneProfileUrl);
        }

        ImGui.Dummy(new Vector2(0f, 6f * ImGuiHelpers.GlobalScale));
        if (Button(Loc.T(L.Account.VerifyAdded), theme) && !busy)
        {
            StartVerify();
        }

        if (Button(Loc.T(L.Common.Cancel), theme))
        {
            ResetFlow();
        }
    }

    private void StartChallenge(string name, string world)
    {
        busy = true;
        status = Loc.T(L.Account.RequestingCode);
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var response = await client.ChallengeAsync(name, world, token).ConfigureAwait(false);
            if (response is null)
            {
                status = Loc.T(L.Account.CannotReach);
                busy = false;
                return;
            }

            code = response.Code;
            challengeId = response.ChallengeId;
            status = response.Instructions;
            busy = false;
        });
    }

    private void StartVerify()
    {
        var id = challengeId;
        if (id is null)
        {
            return;
        }

        busy = true;
        status = Loc.T(L.Account.Verifying);
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var auth = await client.VerifyAsync(id, token).ConfigureAwait(false);
            if (auth is null)
            {
                status = Loc.T(L.Account.CodeNotFound);
                busy = false;
                return;
            }

            session.SignIn(auth.Token, auth.User);
            ResetFlow();
        });
    }

    private void StartMe()
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var me = await client.MeAsync(token).ConfigureAwait(false);
            if (me is not null)
            {
                session.SetUser(me);
            }
        });
    }

    private void ResetFlow()
    {
        challengeId = null;
        code = string.Empty;
        status = string.Empty;
        busy = false;
        meRequested = false;
    }

    private static bool Button(string label, PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, theme.GroupedCard)
            .Push(ImGuiCol.ButtonHovered, Palette.Mix(theme.GroupedCard, theme.Accent, 0.35f))
            .Push(ImGuiCol.ButtonActive, theme.Accent)
            .Push(ImGuiCol.Text, theme.TextStrong))
        {
            return ImGui.Button(label, new Vector2(-1f, 32f * ImGuiHelpers.GlobalScale));
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
