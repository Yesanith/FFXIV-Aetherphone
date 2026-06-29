using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Maps;

internal sealed class MapData
{
    private const string UnknownRegionName = "Eorzea";

    private readonly IDataManager data;
    private readonly IClientState clientState;

    private readonly List<MapRegion> regions = new();
    private readonly Dictionary<uint, MapZone> zonesByTerritory = new();

    private bool built;

    public MapData(IDataManager data, IClientState clientState)
    {
        this.data = data;
        this.clientState = clientState;
    }

    public IReadOnlyList<MapRegion> Regions
    {
        get
        {
            EnsureBuilt();
            return regions;
        }
    }

    public MapLocation CurrentLocation()
    {
        var territoryId = clientState.TerritoryType;
        if (territoryId == 0 || !data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            return new MapLocation(string.Empty, string.Empty);
        }

        var zone = PlaceName(territory.PlaceName.RowId);
        var region = PlaceName(territory.PlaceNameRegion.RowId);
        return new MapLocation(zone, region);
    }

    public bool TryGetCurrentZone(out MapZone zone)
    {
        EnsureBuilt();
        var territoryId = clientState.TerritoryType;
        if (territoryId != 0 && zonesByTerritory.TryGetValue(territoryId, out var found))
        {
            zone = found;
            return true;
        }

        zone = null!;
        return false;
    }

    private void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        built = true;
        Build();
    }

    private void Build()
    {
        var aetherytesByTerritory = CollectAetherytes();
        var territories = data.GetExcelSheet<TerritoryType>();
        var zonesByRegion = new Dictionary<string, List<MapZone>>(StringComparer.Ordinal);
        var regionOrder = new Dictionary<string, byte>(StringComparer.Ordinal);
        var seenZoneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var territory in territories)
        {
            if (!aetherytesByTerritory.TryGetValue(territory.RowId, out var aetherytes) || aetherytes.Count == 0)
            {
                continue;
            }

            var zoneName = PlaceName(territory.PlaceName.RowId);
            if (zoneName.Length == 0)
            {
                continue;
            }

            if (!seenZoneNames.Add(zoneName))
            {
                continue;
            }

            var regionName = PlaceName(territory.PlaceNameRegion.RowId);
            if (regionName.Length == 0)
            {
                regionName = UnknownRegionName;
            }

            var expansionOrder = (byte)territory.ExVersion.RowId;

            aetherytes.Sort(CompareAetherytes);

            var zone = new MapZone
            {
                TerritoryRowId = territory.RowId,
                Name = zoneName,
                RegionName = regionName,
                ExpansionOrder = expansionOrder,
                MapTexturePath = MapTexturePath(territory.Map.RowId),
                Aetherytes = aetherytes,
            };

            zonesByTerritory[territory.RowId] = zone;

            if (!zonesByRegion.TryGetValue(regionName, out var bucket))
            {
                bucket = new List<MapZone>();
                zonesByRegion[regionName] = bucket;
                regionOrder[regionName] = expansionOrder;
            }
            else if (expansionOrder < regionOrder[regionName])
            {
                regionOrder[regionName] = expansionOrder;
            }

            bucket.Add(zone);
        }

        regions.Clear();
        foreach (var pair in zonesByRegion)
        {
            pair.Value.Sort(CompareZones);
            regions.Add(new MapRegion
            {
                Name = pair.Key,
                Order = regionOrder[pair.Key],
                Zones = pair.Value,
            });
        }

        regions.Sort(CompareRegions);
    }

    private Dictionary<uint, List<MapAetheryte>> CollectAetherytes()
    {
        var result = new Dictionary<uint, List<MapAetheryte>>();
        var seenNames = new Dictionary<uint, HashSet<string>>();

        foreach (var aetheryte in data.GetExcelSheet<Aetheryte>())
        {
            if (!aetheryte.IsAetheryte || aetheryte.Invisible)
            {
                continue;
            }

            var territoryId = aetheryte.Territory.RowId;
            if (territoryId == 0)
            {
                continue;
            }

            var name = PlaceName(aetheryte.PlaceName.RowId);
            if (name.Length == 0)
            {
                continue;
            }

            if (!seenNames.TryGetValue(territoryId, out var names))
            {
                names = new HashSet<string>(StringComparer.Ordinal);
                seenNames[territoryId] = names;
            }

            if (!names.Add(name))
            {
                continue;
            }

            if (!result.TryGetValue(territoryId, out var bucket))
            {
                bucket = new List<MapAetheryte>();
                result[territoryId] = bucket;
            }

            bucket.Add(new MapAetheryte
            {
                RowId = aetheryte.RowId,
                Name = name,
                Order = aetheryte.Order,
            });
        }

        return result;
    }

    private string PlaceName(uint placeNameRowId)
    {
        if (placeNameRowId != 0 && data.GetExcelSheet<PlaceName>().TryGetRow(placeNameRowId, out var placeName))
        {
            return placeName.Name.ExtractText();
        }

        return string.Empty;
    }

    private string MapTexturePath(uint mapRowId)
    {
        if (mapRowId == 0 || !data.GetExcelSheet<Map>().TryGetRow(mapRowId, out var map))
        {
            return string.Empty;
        }

        var mapId = map.Id.ExtractText();
        if (string.IsNullOrEmpty(mapId) || mapId == "0000/0000")
        {
            return string.Empty;
        }

        var rawKey = mapId.Replace("/", string.Empty);
        return $"ui/map/{mapId}/{rawKey}_m.tex";
    }

    private static int CompareAetherytes(MapAetheryte left, MapAetheryte right)
    {
        var byOrder = left.Order.CompareTo(right.Order);
        if (byOrder != 0)
        {
            return byOrder;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareZones(MapZone left, MapZone right)
    {
        var byExpansion = left.ExpansionOrder.CompareTo(right.ExpansionOrder);
        if (byExpansion != 0)
        {
            return byExpansion;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareRegions(MapRegion left, MapRegion right)
    {
        var byOrder = left.Order.CompareTo(right.Order);
        if (byOrder != 0)
        {
            return byOrder;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}
