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
                if (activePresets == null)
                {
                    Reload();
                }
                return activePresets;
            }
        }

        public static void Reload()
        {
            var loaded = KocomMarkdownParser.LoadFromDisk();
            activePresets = loaded ?? new List<HexPreset>();
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
    }
}
