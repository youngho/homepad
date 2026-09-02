using System;
using System.Collections.Generic;

namespace Homepad.Home
{
    public enum HomeItemKind
    {
        Light,
        Heating,
        Gas,
        Vent,
        Elevator,
        ElectricCurtain,
        AirConditioner
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
        Entrance,
        Study,
        Kids,
        DressRoom,
        Alpha,
        Balcony,
        Bathroom,
        Custom
    }

    public enum WallDir
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public sealed class RoomPreset
    {
        public readonly RoomHint Hint;
        public readonly string DefaultName;
        public readonly string Emoji;

        public RoomPreset(RoomHint hint, string defaultName, string emoji)
        {
            Hint = hint;
            DefaultName = defaultName;
            Emoji = emoji;
        }

        public static readonly RoomPreset[] RecommendedPresets =
        {
            new RoomPreset(RoomHint.Living, "거실", "🛋️"),
            new RoomPreset(RoomHint.Master, "안방", "🛏️"),
            new RoomPreset(RoomHint.Bedroom, "침실 1", "🛌"),
            new RoomPreset(RoomHint.Bedroom2, "침실 2", "🧸"),
            new RoomPreset(RoomHint.Study, "서재", "📚"),
            new RoomPreset(RoomHint.Kids, "아이방", "🎨"),
            new RoomPreset(RoomHint.DressRoom, "드레스룸", "👗"),
            new RoomPreset(RoomHint.Kitchen, "주방", "🍳"),
            new RoomPreset(RoomHint.Entrance, "현관", "🚪"),
            new RoomPreset(RoomHint.Alpha, "알파룸", "☕"),
            new RoomPreset(RoomHint.Bathroom, "욕실", "🚿"),
            new RoomPreset(RoomHint.Balcony, "발코니", "🌿")
        };
    }

    /// <summary>
    /// Rule-based Device Category Definition defining attachment surface, singleton rules, and allowed spaces.
    /// </summary>
    public sealed class DeviceCategoryRule
    {
        public readonly HomeItemKind Kind;
        public readonly string CategoryName;
        public readonly string Emoji;
        public readonly Surface DefaultSurface;
        public readonly bool SingletonPerRoom;
        public readonly string Description;

        public DeviceCategoryRule(
            HomeItemKind kind,
            string categoryName,
            string emoji,
            Surface defaultSurface,
            bool singletonPerRoom,
            string description)
        {
            Kind = kind;
            CategoryName = categoryName;
            Emoji = emoji;
            DefaultSurface = defaultSurface;
            SingletonPerRoom = singletonPerRoom;
            Description = description;
        }
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

        // 1. Device Category Rules Registry
        public static readonly Dictionary<HomeItemKind, DeviceCategoryRule> CategoryRules = new Dictionary<HomeItemKind, DeviceCategoryRule>
        {
            [HomeItemKind.Light] = new DeviceCategoryRule(HomeItemKind.Light, "조명", "💡", Surface.Ceiling, false, "천장 무드 조명"),
            [HomeItemKind.Heating] = new DeviceCategoryRule(HomeItemKind.Heating, "난방", "🔥", Surface.Wall, true, "바닥 온돌 난방 조절기"),
            [HomeItemKind.ElectricCurtain] = new DeviceCategoryRule(HomeItemKind.ElectricCurtain, "전동커튼", "🪟", Surface.Window, false, "3D 스마트 창문 커튼"),
            [HomeItemKind.AirConditioner] = new DeviceCategoryRule(HomeItemKind.AirConditioner, "에어컨", "❄️", Surface.Ceiling, true, "천장형 시스템 에어컨"),
            [HomeItemKind.Vent] = new DeviceCategoryRule(HomeItemKind.Vent, "환기", "🌀", Surface.Ceiling, true, "천장 공기 청정 환기"),
            [HomeItemKind.Gas] = new DeviceCategoryRule(HomeItemKind.Gas, "가스 밸브", "🛡️", Surface.Wall, true, "주방 안전 자동 차단 밸브"),
            [HomeItemKind.Elevator] = new DeviceCategoryRule(HomeItemKind.Elevator, "엘리베이터", "🛗", Surface.Floor, true, "현관 엘리베이터 호출기")
        };

        // 2. Predefined Quick Catalog
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

        /// <summary>
        /// Dynamically creates a HomeItemDef based on device kind and room hint according to system rules.
        /// </summary>
        public static HomeItemDef Create(HomeItemKind kind, RoomHint room, string customRoomName = null)
        {
            if (!CategoryRules.TryGetValue(kind, out var rule))
            {
                rule = new DeviceCategoryRule(kind, kind.ToString(), "📦", Surface.Floor, false, "");
            }

            string rName = !string.IsNullOrEmpty(customRoomName) ? customRoomName : RoomName(room);
            string catId = $"{kind.ToString().ToLower()}_{room.ToString().ToLower()}";
            string name = $"{rName} {rule.CategoryName}";
            return new HomeItemDef(catId, kind, rule.DefaultSurface, room, name, rule.SingletonPerRoom);
        }

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
                RoomHint.Study => "서재",
                RoomHint.Kids => "아이방",
                RoomHint.DressRoom => "드레스룸",
                RoomHint.Alpha => "알파룸",
                RoomHint.Bathroom => "욕실",
                RoomHint.Balcony => "발코니",
                _ => "공간"
            };
        }

        public static string RoomEmoji(RoomHint hint)
        {
            return hint switch
            {
                RoomHint.Living => "🛋️",
                RoomHint.Master => "🛏️",
                RoomHint.Bedroom => "🛌",
                RoomHint.Bedroom2 => "🧸",
                RoomHint.Kitchen => "🍳",
                RoomHint.Entrance => "🚪",
                RoomHint.Study => "📚",
                RoomHint.Kids => "🎨",
                RoomHint.DressRoom => "👗",
                RoomHint.Alpha => "☕",
                RoomHint.Bathroom => "🚿",
                RoomHint.Balcony => "🌿",
                _ => "🏠"
            };
        }

        public static HomeItemDef Find(string catalogId)
        {
            if (string.IsNullOrEmpty(catalogId)) return null;

            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].CatalogId.Equals(catalogId, StringComparison.OrdinalIgnoreCase))
                    return Catalog[i];
            }

            int under = catalogId.IndexOf('_');
            if (under > 0)
            {
                string kindStr = catalogId.Substring(0, under);
                string roomStr = catalogId.Substring(under + 1);

                if (Enum.TryParse<HomeItemKind>(kindStr, true, out var kind) &&
                    Enum.TryParse<RoomHint>(roomStr, true, out var room))
                {
                    return Create(kind, room);
                }
            }

            return null;
        }
    }
}
