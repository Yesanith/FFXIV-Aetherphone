namespace Aetherphone.Core.Character;

internal sealed record LocalCharacter(
    string Name,
    string WorldName,
    string DataCenter,
    string Job,
    int Level,
    int AverageItemLevel,
    string Race,
    string Clan,
    string Gender,
    string Nameday,
    string Guardian,
    string CityState,
    string GrandCompany,
    IReadOnlyList<EquippedItem> Gear);
