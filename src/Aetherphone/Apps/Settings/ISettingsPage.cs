using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Settings;

// One screen of the Settings app. A page is fully self-describing: Title names the screen and
// the device header, while Glyph/Tint/Summary render the row that links to it from its parent
// list. Adding a category is a new page plus one entry in SettingsApp's group table.
internal interface ISettingsPage
{
    string Title { get; }

    // Right-aligned hint shown in the parent list row (e.g. the current accent name).
    string Summary { get; }

    // Leading rounded-tile glyph and colour shown in the parent list row.
    string Glyph { get; }

    Vector4 Tint { get; }

    void Draw(in PhoneContext context, Rect body);
}
