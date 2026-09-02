using System.IO;
using Homepad.Home;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Homepad.Editor
{
    public static class WallpadSceneSetup
    {
        private const string ProfilePath = "Assets/Settings/Wallpad_VolumeProfile.asset";

        [MenuItem("Homepad/Setup Wallpad Scene Visuals")]
        public static void SetupSceneVisuals()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "WallpadMain")
            {
                EditorSceneManager.OpenScene("Assets/Scenes/WallpadMain.unity");
            }

            // 1. Camera Setup
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 7.5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f); // Luxury dark slate
                cam.transform.rotation = Quaternion.Euler(35.264f, 45f, 0f);
                cam.transform.position = new Vector3(-13.5f, 14.5f, -13.5f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                // Add URP Additional Camera Data
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camData.antialiasingQuality = AntialiasingQuality.High;
            }

            // 2. Main Key Light (Directional)
            var keyLightGo = GameObject.Find("Directional Light");
            if (keyLightGo == null) keyLightGo = new GameObject("Directional Light");
            keyLightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var keyLight = keyLightGo.GetComponent<Light>() ?? keyLightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1.0f, 0.97f, 0.92f);
            keyLight.intensity = 0.55f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 0.65f;

            // 3. Ambient Fill Light
            var fillLightGo = GameObject.Find("Fill Light");
            if (fillLightGo == null) fillLightGo = new GameObject("Fill Light");
            fillLightGo.transform.rotation = Quaternion.Euler(30f, 150f, 0f);
            var fillLight = fillLightGo.GetComponent<Light>() ?? fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.70f, 0.78f, 0.92f);
            fillLight.intensity = 0.14f;
            fillLight.shadows = LightShadows.None;

            // 4. Global Post-Processing Volume
            EnsureVolumeProfile();
            var volumeGo = GameObject.Find("Global PostProcess Volume");
            if (volumeGo == null) volumeGo = new GameObject("Global PostProcess Volume");
            var volume = volumeGo.GetComponent<Volume>() ?? volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

            // 5. Wire IsometricHomeBuilder
            var builder = Object.FindFirstObjectByType<IsometricHomeBuilder>();
            if (builder != null)
            {
                var so = new SerializedObject(builder);
                var modernApartmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Home/Kit/Prefabs/ModernApartment.prefab");
                var prop = so.FindProperty("modernApartmentPrefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = modernApartmentPrefab;
                    so.ApplyModifiedProperties();
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("WallpadMain scene visuals configured and saved successfully!");
        }

        private static void EnsureVolumeProfile()
        {
            if (!Directory.Exists("Assets/Settings")) Directory.CreateDirectory("Assets/Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // Bloom
            if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.85f));

            // Tonemapping
            if (!profile.TryGet<Tonemapping>(out var tone)) tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            // Color Adjustments
            if (!profile.TryGet<ColorAdjustments>(out var colorAdj)) colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.15f);
            colorAdj.contrast.Override(12f);
            colorAdj.saturation.Override(8f);

            // Vignette
            if (!profile.TryGet<Vignette>(out var vig)) vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.03f, 0.04f, 0.08f));

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
    }
}
