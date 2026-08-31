using System;
using System.Collections.Generic;
using System.Globalization;

namespace Homepad.Core
{
    public enum HexCategory
    {
        All = 0,
        Lighting = 1,
        Heating = 2,
        Ventilation = 3,
        DoorLock = 4,
        Custom = 5
    }

    [System.Serializable]
    public class HexPreset
    {
        public string id;
        public HexCategory category;
        public string title;
        public string description;
        public string hexString;
        public byte[] rawBytes;

        public HexPreset(string id, HexCategory category, string title, string description, string hexString)
        {
            this.id = id;
            this.category = category;
            this.title = title;
            this.description = description;
            this.hexString = hexString.Trim();
            this.rawBytes = KocomHexPresets.HexStringToBytes(this.hexString);
        }
    }

    public static class KocomHexPresets
    {
        private static List<HexPreset> activePresets;

        public static IReadOnlyList<HexPreset> AllPresets
        {
            get
            {
                if (activePresets == null || activePresets.Count == 0)
                {
                    Reload();
                }
                return activePresets;
            }
        }

        public static void Reload()
        {
            var loaded = KocomMarkdownParser.LoadFromDisk();
            if (loaded != null && loaded.Count > 0)
            {
                activePresets = loaded;
            }
            else if (activePresets == null || activePresets.Count == 0)
            {
                activePresets = GetDefaultHardcodedPresets();
            }
        }

        public static List<HexPreset> GetPresetsByCategory(HexCategory category)
        {
            var all = AllPresets;
            if (category == HexCategory.All) return new List<HexPreset>(all);

            var list = new List<HexPreset>();
            foreach (var p in all)
            {
                if (p.category == category) list.Add(p);
            }
            return list;
        }

        public static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return new byte[0];
            string clean = hex.Replace(" ", "").Replace("-", "").Trim();
            if (clean.Length % 2 != 0) clean = "0" + clean;

            byte[] bytes = new byte[clean.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (byte.TryParse(clean.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    bytes[i] = b;
                }
            }
            return bytes;
        }

        public static string RecalculateChecksum(string hex)
        {
            byte[] bytes = HexStringToBytes(hex);
            if (bytes.Length != KocomProtocol.PacketSize) return hex;

            bytes[18] = KocomProtocol.ComputeChecksum(bytes);
            return KocomProtocol.ToHexString(bytes);
        }

        private static List<HexPreset> GetDefaultHardcodedPresets()
        {
            return new List<HexPreset>
            {
                new HexPreset("LIGHT_LIVING_OFF", HexCategory.Lighting, "거실 조명 전체 OFF", "거실 (00 01) 전체 소등",
                    "AA 55 30 BC 00 0E 00 01 00 00 00 00 00 00 00 00 00 00 FB 0D 0D"),
                new HexPreset("LIGHT_LIVING_SW1_ON", HexCategory.Lighting, "거실 조명 스위치1 ON", "거실 (00 01) 스위치1 켜기",
                    "AA 55 30 BC 00 0E 00 01 00 00 FF 00 00 00 00 00 00 00 FA 0D 0D"),
                new HexPreset("LIGHT_LIVING_SW2_ON", HexCategory.Lighting, "거실 조명 스위치2 ON", "거실 (00 01) 스위치2 켜기",
                    "AA 55 30 BC 00 0E 00 01 00 00 00 FF 00 00 00 00 00 00 FA 0D 0D"),
                new HexPreset("HEAT_LIVING_ON_20", HexCategory.Heating, "거실 난방 ON (20°C)", "거실 (00 01) 난방 가동, 설정 20°C",
                    "AA 55 30 BC 00 36 00 01 00 00 11 00 14 00 00 00 00 00 48 0D 0D"),
                new HexPreset("VENT_SPEED_LOW", HexCategory.Ventilation, "환기 풍량 1단 (약)", "풍량 1단 약풍 설정",
                    "AA 55 30 BC 00 48 00 01 00 00 88 A0 40 00 00 00 00 00 9D 0D 0D"),
                new HexPreset("DOOR_UNLOCK_REQ", HexCategory.DoorLock, "현관문 문열림 요청 (REQ)", "월패드(00 01) -> 도어락(00 33) CMD 00 02",
                    "AA 55 30 BC 00 01 00 33 00 02 00 00 00 00 00 00 00 00 22 0D 0D")
            };
        }
    }
}
