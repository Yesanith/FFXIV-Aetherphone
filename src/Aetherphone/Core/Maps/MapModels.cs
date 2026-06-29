namespace Aetherphone.Core.Maps;

internal sealed class MapAetheryte
{
    public required uint RowId { get; init; }

    public required string Name { get; init; }

    public required byte Order { get; init; }
}

internal sealed class MapZone
{
    public required uint TerritoryRowId { get; init; }

    public required string Name { get; init; }

    public required string RegionName { get; init; }

    public required byte ExpansionOrder { get; init; }

    public required string MapTexturePath { get; init; }

    public required IReadOnlyList<MapAetheryte> Aetherytes { get; init; }
}

internal sealed class MapRegion
{
    public required string Name { get; init; }

    public required byte Order { get; init; }

    public required IReadOnlyList<MapZone> Zones { get; init; }
}

internal readonly record struct MapLocation(string Zone, string Region);
