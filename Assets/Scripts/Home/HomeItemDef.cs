namespace Homepad.Home
{
    public enum HomeItemKind
    {
        Light,
        Heating,
        Gas,
        Vent,
        Elevator,
        ElectricCurtain
    }

    public enum Surface
    {
        Floor,
        Wall,
        Ceiling,
        Window
    }

    public enum RoomHint
    {
        Living,
        Master,
        Bedroom,
        Bedroom2,
        Kitchen,
        Entrance
    }

    public enum WallDir
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public sealed class HomeItemDef
    {
        public readonly string CatalogId;
        public readonly HomeItemKind Kind;
        public readonly Surface Surface;
        public readonly RoomHint RoomHint;
        public readonly string DisplayName;
        public readonly bool Singleton;

        public HomeItemDef(string catalogId, HomeItemKind kind, Surface surface, RoomHint roomHint, string displayName, bool singleton)
        {
            CatalogId = catalogId;
            Kind = kind;
            Surface = surface;
            RoomHint = roomHint;
            DisplayName = displayName;
            Singleton = singleton;
        }

        public static readonly HomeItemDef[] Catalog =
        {
            new HomeItemDef("light_living", HomeItemKind.Light, Surface.Ceiling, RoomHint.Living, "거실 조명", false),
            new HomeItemDef("light_master", HomeItemKind.Light, Surface.Ceiling, RoomHint.Master, "안방 조명", false),
            new HomeItemDef("light_bed1", HomeItemKind.Light, Surface.Ceiling, RoomHint.Bedroom, "침실 조명", false),
            new HomeItemDef("light_bed2", HomeItemKind.Light, Surface.Ceiling, RoomHint.Bedroom2, "침실 2 조명", false),
            new HomeItemDef("light_kitchen", HomeItemKind.Light, Surface.Ceiling, RoomHint.Kitchen, "주방 조명", false),
            new HomeItemDef("heat_living", HomeItemKind.Heating, Surface.Wall, RoomHint.Living, "거실 난방", true),
            new HomeItemDef("heat_master", HomeItemKind.Heating, Surface.Wall, RoomHint.Master, "안방 난방", true),
            new HomeItemDef("heat_bed1", HomeItemKind.Heating, Surface.Wall, RoomHint.Bedroom, "침실 난방", true),
            new HomeItemDef("heat_bed2", HomeItemKind.Heating, Surface.Wall, RoomHint.Bedroom2, "침실 2 난방", true),
            new HomeItemDef("gas", HomeItemKind.Gas, Surface.Wall, RoomHint.Kitchen, "가스 밸브", true),
            new HomeItemDef("vent", HomeItemKind.Vent, Surface.Ceiling, RoomHint.Living, "환기", true),
            new HomeItemDef("elevator", HomeItemKind.Elevator, Surface.Floor, RoomHint.Entrance, "엘리베이터", true),
            new HomeItemDef("curtain", HomeItemKind.ElectricCurtain, Surface.Window, RoomHint.Living, "전동커튼", false)
        };

        public static ushort RoomCode(RoomHint hint)
        {
            return hint switch
            {
                RoomHint.Living => 0x0001,
                RoomHint.Master => 0x0101,
                RoomHint.Bedroom => 0x0201,
                RoomHint.Bedroom2 => 0x0301,
                RoomHint.Kitchen => 0x0401,
                RoomHint.Entrance => 0x0001,
                _ => 0x0001
            };
        }

        public static string RoomName(RoomHint hint)
        {
            return hint switch
            {
                RoomHint.Living => "거실",
                RoomHint.Master => "안방",
                RoomHint.Bedroom => "침실 1",
                RoomHint.Bedroom2 => "침실 2",
                RoomHint.Kitchen => "주방",
                RoomHint.Entrance => "현관",
                _ => "방"
            };
        }

        public static HomeItemDef Find(string catalogId)
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].CatalogId == catalogId) return Catalog[i];
            }

            return null;
        }
    }
}
