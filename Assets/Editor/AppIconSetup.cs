using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Homepad.Editor
{
    public static class AppIconSetup
    {
        private const string Root = "Assets/AppIcons";

        [MenuItem("Homepad/Apply App Icons")]
        public static void Apply()
        {
            var def = Load<Texture2D>(Root + "/AppIcon_1024.png");
            if (def == null)
            {
                Debug.LogError("App icon 1024 is missing at " + Root + "/AppIcon_1024.png");
                return;
            }

            ApplyDefault(def);
            ApplyStandalone();
            ApplyIos();
            ApplyAndroid();
            AssetDatabase.SaveAssets();
            Debug.Log("App icons assigned for Default, Standalone, iOS, and Android.");
        }

        private static void ApplyDefault(Texture2D def)
        {
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { def }, IconKind.Any);
        }

        private static void ApplyStandalone()
        {
            var bySize = LoadSized(Root + "/Standalone", "Icon_{0}.png");
            AssignSizedIcons(NamedBuildTarget.Standalone, IconKind.Application, bySize);
            AssignSizedIcons(NamedBuildTarget.Standalone, IconKind.Any, bySize);
        }

        private static void ApplyIos()
        {
            var ios = NamedBuildTarget.iOS;
            var bySize = LoadSized(Root + "/iOS", "{0}.png");
            if (bySize.Count == 0) return;

            AssignPlatform(ios, "UnityEditor.iOS.iOSPlatformIconKind", "Application", bySize);
            AssignPlatform(ios, "UnityEditor.iOS.iOSPlatformIconKind", "Spotlight", bySize);
            AssignPlatform(ios, "UnityEditor.iOS.iOSPlatformIconKind", "Settings", bySize);
            AssignPlatform(ios, "UnityEditor.iOS.iOSPlatformIconKind", "Notification", bySize);
            AssignPlatform(ios, "UnityEditor.iOS.iOSPlatformIconKind", "Marketing", bySize);
        }

        private static void ApplyAndroid()
        {
            var android = NamedBuildTarget.Android;
            var legacy = LoadSized(Root + "/Android", "ic_{0}.png");
            AssignPlatform(android, "UnityEditor.Android.AndroidPlatformIconKind", "Legacy", legacy);
            AssignPlatform(android, "UnityEditor.Android.AndroidPlatformIconKind", "Round", legacy);

            var fg = Load<Texture2D>(Root + "/Android/adaptive_foreground.png");
            var bg = Load<Texture2D>(Root + "/Android/adaptive_background.png");
            if (fg == null || bg == null) return;

            var kind = FindKind("UnityEditor.Android.AndroidPlatformIconKind", "Adaptive");
            if (kind == null) return;

            var icons = PlayerSettings.GetPlatformIcons(android, kind);
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i].SetTextures(new[] { bg, fg });
            }

            PlayerSettings.SetPlatformIcons(android, kind, icons);
        }

        private static void AssignSizedIcons(NamedBuildTarget target, IconKind kind, Dictionary<int, Texture2D> bySize)
        {
            int[] sizes;
            try
            {
                sizes = PlayerSettings.GetIconSizes(target, kind);
            }
            catch (Exception)
            {
                return;
            }

            if (sizes == null || sizes.Length == 0) return;

            var textures = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                textures[i] = Closest(bySize, sizes[i]);
            }

            PlayerSettings.SetIcons(target, textures, kind);
        }

        private static void AssignPlatform(
            NamedBuildTarget target,
            string kindType,
            string kindName,
            Dictionary<int, Texture2D> bySize)
        {
            var kind = FindKind(kindType, kindName);
            if (kind == null || bySize.Count == 0) return;

            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            for (int i = 0; i < icons.Length; i++)
            {
                var tex = Closest(bySize, icons[i].width);
                if (tex == null) continue;
                icons[i].SetTexture(tex);
            }

            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        private static PlatformIconKind FindKind(string typeName, string property)
        {
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                var type = asms[i].GetType(typeName);
                if (type == null) continue;
                var prop = type.GetProperty(property, BindingFlags.Public | BindingFlags.Static);
                if (prop == null) continue;
                return prop.GetValue(null) as PlatformIconKind;
            }

            return null;
        }

        private static Dictionary<int, Texture2D> LoadSized(string folder, string nameFormat)
        {
            var map = new Dictionary<int, Texture2D>();
            if (!AssetDatabase.IsValidFolder(folder)) return map;

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (int i = 0; i < 2048; i++)
            {
                string path = folder + "/" + string.Format(nameFormat, i);
                var tex = Load<Texture2D>(path);
                if (tex != null) map[i] = tex;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                if (!map.ContainsKey(tex.width)) map[tex.width] = tex;
            }

            return map;
        }

        private static Texture2D Closest(Dictionary<int, Texture2D> bySize, int width)
        {
            if (bySize.TryGetValue(width, out var exact)) return exact;

            Texture2D best = null;
            int bestDelta = int.MaxValue;
            foreach (var pair in bySize)
            {
                int delta = pair.Key >= width ? pair.Key - width : (width - pair.Key) + 10000;
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = pair.Value;
                }
            }

            return best;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
