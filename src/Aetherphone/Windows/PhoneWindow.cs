using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Shell;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class PhoneWindow : Window
{
    private readonly PhoneShell shell;

    public PhoneWindow(PhoneShell shell)
        : base(AepConstants.Name, BuildFlags())
    {
        this.shell = shell;
        Size = new Vector2(360f, 740f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300f, 620f),
            MaximumSize = new Vector2(460f, 940f),
        };
        RespectCloseHotkey = false;
    }

    private static ImGuiWindowFlags BuildFlags()
        => ImGuiWindowFlags.NoTitleBar
         | ImGuiWindowFlags.NoScrollbar
         | ImGuiWindowFlags.NoScrollWithMouse
         | ImGuiWindowFlags.NoCollapse
         | ImGuiWindowFlags.NoBackground;

    public override void PreDraw() => ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void Draw()
    {
        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail();
        ImGui.Dummy(available);
        shell.Draw(new Rect(origin, origin + available));
    }
}
