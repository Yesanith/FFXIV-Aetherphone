namespace Aetherphone.Apps.Market;

internal sealed class MarketView
{
    public readonly uint ItemId;
    public readonly string Name;
    public readonly uint IconId;

    public MarketView(uint itemId, string name, uint iconId)
    {
        ItemId = itemId;
        Name = name;
        IconId = iconId;
    }
}
