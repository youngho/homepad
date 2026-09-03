using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Homepad.Core
{
    public static class KocomMarkdownParser
    {
        public static string UserMarkdownPath =>
            Path.Combine(Application.persistentDataPath, "kocom-hex.md");

        public static string[] GetSearchPaths()
        {
#if UNITY_EDITOR
            return new string[]
            {
                Path.Combine(Application.dataPath, "../Docs/kocom-hex.md"),
                Path.Combine(Application.dataPath, "Docs/kocom-hex.md"),
                Path.Combine(Application.streamingAssetsPath, "kocom-hex.md"),
                UserMarkdownPath
            };
#else
            EnsurePersistentCopy();
            return new string[]
            {
                UserMarkdownPath,
                Path.Combine(Application.streamingAssetsPath, "kocom-hex.md")
            };
#endif
        }

        public static string FindMarkdownPath()
        {
            foreach (var path in GetSearchPaths())
            {
                try
                {
                    if (File.Exists(path))
                    {
                        return Path.GetFullPath(path);
                    }
                }
                catch
                {
                    // ignore invalid path on platform
                }
            }
            return null;
        }

        public static List<HexPreset> LoadFromDisk()
        {
            string content = LoadMarkdownText(out string source);
            if (string.IsNullOrEmpty(content))
            {
                Debug.LogError("[KocomMarkdownParser] kocom-hex.md 를 찾지 못했습니다.");
                return null;
            }

            var list = ParseMarkdown(content);
            Debug.Log($"[KocomMarkdownParser] '{source}' 에서 {list.Count}개의 패킷 프리셋을 성공적으로 로드했습니다.");
            return list;
        }

        public static string LoadMarkdownText(out string source)
        {
            string path = FindMarkdownPath();
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    source = path;
                    return File.ReadAllText(path, System.Text.Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[KocomMarkdownParser] 마크다운 로드 실패: {ex.Message}");
                }
            }

            var baked = LoadBakedMarkdown();
            if (!string.IsNullOrEmpty(baked))
            {
                source = "Resources/kocom-hex";
                return baked;
            }

            source = null;
            return null;
        }

        static string LoadBakedMarkdown()
        {
            var embedded = Resources.Load<TextAsset>("kocom-hex");
            if (embedded != null && !string.IsNullOrWhiteSpace(embedded.text))
            {
                return embedded.text;
            }

            return null;
        }

        static void EnsurePersistentCopy()
        {
            try
            {
                if (File.Exists(UserMarkdownPath))
                {
                    return;
                }

                string baked = null;
                var streaming = Path.Combine(Application.streamingAssetsPath, "kocom-hex.md");
                if (File.Exists(streaming))
                {
                    baked = File.ReadAllText(streaming, System.Text.Encoding.UTF8);
                }

                if (string.IsNullOrEmpty(baked))
                {
                    baked = LoadBakedMarkdown();
                }

                if (string.IsNullOrEmpty(baked))
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(UserMarkdownPath));
                File.WriteAllText(UserMarkdownPath, baked, System.Text.Encoding.UTF8);
                Debug.Log($"[KocomMarkdownParser] 실행 중 수정용 복사본을 만들었습니다: {UserMarkdownPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KocomMarkdownParser] persistent 복사 실패: {ex.Message}");
            }
        }

        public static List<HexPreset> ParseMarkdown(string markdown)
        {
            var presets = new List<HexPreset>();
            if (string.IsNullOrWhiteSpace(markdown)) return presets;

            var lines = markdown.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            HexCategory currentCategory = HexCategory.Lighting;
            int counter = 1;

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Detect Category Headers (e.g. "## 1. 조명", "## 난방", "## 환기", "## 현관문")
                if (line.StartsWith("##"))
                {
                    if (line.Contains("조명") || line.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0)
                        currentCategory = HexCategory.Lighting;
                    else if (line.Contains("난방") || line.IndexOf("Heat", StringComparison.OrdinalIgnoreCase) >= 0)
                        currentCategory = HexCategory.Heating;
                    else if (line.Contains("환기") || line.IndexOf("Vent", StringComparison.OrdinalIgnoreCase) >= 0)
                        currentCategory = HexCategory.Ventilation;
                    else if (line.Contains("현관") || line.Contains("도어") || line.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
                        currentCategory = HexCategory.DoorLock;
                    continue;
                }

                // Skip markdown table headers (e.g. "|---|---|---|")
                if (line.Contains("---") || line.Contains("| 이름 |") || line.Contains("| 구분 |") || line.Contains("| 방 |"))
                {
                    continue;
                }

                // Table Row Parsing: | 이름 | 설명 | HEX |
                if (line.StartsWith("|") && line.EndsWith("|"))
                {
                    var cols = line.Split(new[] { '|' }, StringSplitOptions.None);
                    if (cols.Length < 3) continue;

                    string col1 = cols[1].Trim().Trim('`', '*');
                    string col2 = (cols.Length >= 4) ? cols[2].Trim().Trim('`', '*') : "";
                    string col3 = (cols.Length >= 4) ? cols[3].Trim().Trim('`', '*') : cols[2].Trim().Trim('`', '*');

                    // Find which column contains the 21-byte HEX pattern
                    string hexCandidate = "";
                    string title = col1;
                    string desc = col2;

                    if (IsHexPattern(col3))
                    {
                        hexCandidate = col3;
                    }
                    else if (IsHexPattern(col2))
                    {
                        hexCandidate = col2;
                        desc = col1;
                    }
                    else
                    {
                        // Check all columns
                        for (int c = 1; c < cols.Length - 1; c++)
                        {
                            string col = cols[c].Trim().Trim('`', '*');
                            if (IsHexPattern(col))
                            {
                                hexCandidate = col;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(hexCandidate))
                    {
                        // Auto-calculate checksum if requested or check validity
                        string cleanHex = NormalizeHexString(hexCandidate);
                        string id = $"{currentCategory}_{counter++}";
                        presets.Add(new HexPreset(id, currentCategory, title, desc, cleanHex));
                    }
                }
            }

            return presets;
        }

        private static bool IsHexPattern(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string clean = text.Replace(" ", "").Replace("-", "").ToUpperInvariant();
            return clean.StartsWith("AA55") && clean.Length >= 42;
        }

        private static string NormalizeHexString(string hex)
        {
            string clean = hex.Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();
            if (clean.Length > 42) clean = clean.Substring(0, 42);

            // Format nicely: "AA 55 30 BC ..."
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < clean.Length; i += 2)
            {
                if (i > 0) sb.Append(' ');
                if (i + 2 <= clean.Length)
                    sb.Append(clean.Substring(i, 2));
                else
                    sb.Append(clean.Substring(i));
            }
            return sb.ToString();
        }
    }
}
