using System.IO;
using Homepad.Home;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Homepad.Editor
{
    public static class ApartmentSetup
    {
        private const string MatDir = "Assets/Models/Apartment/Materials";
        private const string PrefabPath = "Assets/Home/Kit/Prefabs/ModernApartment.prefab";

        [MenuItem("Homepad/Setup Modern Apartment")]
        public static GameObject BuildApartmentPrefab()
        {
            if (!Directory.Exists(MatDir)) Directory.CreateDirectory(MatDir);
            if (!Directory.Exists("Assets/Home/Kit/Prefabs")) Directory.CreateDirectory("Assets/Home/Kit/Prefabs");

            // Load textures
            var texRender = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Apartment/ModernApartment/textures/render_baseColor.jpeg");
            var texMoveis = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Apartment/ModernApartment/textures/moveis_baseColor.jpeg");
            var texPlanta = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Apartment/ModernApartment/textures/planta_baseColor.png");

            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Create Materials
            var matRender = CreateOrUpdateMat("M_Apartment_Render", litShader, m =>
            {
                m.SetTexture("_BaseMap", texRender);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.32f);
            });

            var matMoveis = CreateOrUpdateMat("M_Apartment_Furniture", litShader, m =>
            {
                m.SetTexture("_BaseMap", texMoveis);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.42f);
            });

            var matPlanta = CreateOrUpdateMat("M_Apartment_Plants", litShader, m =>
            {
                m.SetTexture("_BaseMap", texPlanta);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.25f);
            });

            var matCurtain = CreateOrUpdateMat("M_Apartment_Curtain", litShader, m =>
            {
                m.SetColor("_BaseColor", new Color(0.93f, 0.90f, 0.84f, 1f));
                m.SetFloat("_Smoothness", 0.12f);
            });

            var matGlass = CreateOrUpdateMat("M_Apartment_Glass", litShader, m =>
            {
                m.SetColor("_BaseColor", new Color(0.70f, 0.85f, 0.95f, 0.35f));
                m.SetFloat("_Smoothness", 0.92f);
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            });

            var matMirror = CreateOrUpdateMat("M_Apartment_Mirror", litShader, m =>
            {
                m.SetColor("_BaseColor", new Color(0.88f, 0.90f, 0.94f, 1f));
                m.SetFloat("_Metallic", 0.92f);
                m.SetFloat("_Smoothness", 0.95f);
            });

            var matMetals = CreateOrUpdateMat("M_Apartment_Metals", litShader, m =>
            {
                m.SetTexture("_BaseMap", texRender);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Metallic", 0.85f);
                m.SetFloat("_Smoothness", 0.82f);
            });

            var matLightStrip = CreateOrUpdateMat("M_Apartment_LightStrip", litShader, m =>
            {
                var emission = new Color(1.0f, 0.92f, 0.78f) * 3.2f;
                m.SetColor("_BaseColor", new Color(1.0f, 0.95f, 0.85f));
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            });

            // Load base GLTF
            var gltfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Apartment/ModernApartment/scene.gltf");
            if (gltfPrefab == null)
            {
                Debug.LogError("scene.gltf not found!");
                return null;
            }

            var root = new GameObject("ModernApartment");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(gltfPrefab, root.transform);
            model.name = "Model";
            model.transform.localPosition = new Vector3(19.667f, 0.01f, 16.86f);

            // Apply Materials & Shadows
            MeshRenderer rendLuzRipado = null;
            MeshRenderer rendLuzParede = null;
            Transform curtainTrans = null;

            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;

                string n = r.gameObject.name;
                var sharedMat = r.sharedMaterial;
                string matName = sharedMat != null ? sharedMat.name : "";

                if (matName.Contains("render") || n.Contains("Object_4") || n.Contains("Object_5"))
                    r.sharedMaterial = matRender;
                else if (matName.Contains("moveis") || n.Contains("Object_9"))
                    r.sharedMaterial = matMoveis;
                else if (matName.Contains("planta") || n.Contains("Object_21"))
                    r.sharedMaterial = matPlanta;
                else if (matName.Contains("cortina") || n.Contains("Object_7"))
                {
                    r.sharedMaterial = matCurtain;
                    curtainTrans = r.transform;
                    if (r.GetComponent<CurtainCloth>() == null)
                    {
                        r.gameObject.AddComponent<CurtainCloth>();
                    }
                }
                else if (matName.Contains("vidro") || n.Contains("Object_17") || n.Contains("Object_19"))
                    r.sharedMaterial = matGlass;
                else if (matName.Contains("espelho") || n.Contains("Object_15"))
                    r.sharedMaterial = matMirror;
                else if (matName.Contains("luz") || n.Contains("Object_11") || n.Contains("Object_13"))
                {
                    r.sharedMaterial = matLightStrip;
                    if (n.Contains("Object_11") || n.Contains("ripado")) rendLuzRipado = r;
                    if (n.Contains("Object_13") || n.Contains("parede")) rendLuzParede = r;
                }
                else if (matName.Contains("metais") || n.Contains("Object_23"))
                    r.sharedMaterial = matMetals;
                else
                    r.sharedMaterial = matRender;
            }

            // Create Room Lights & Rig
            var lightsRoot = new GameObject("Lights");
            lightsRoot.transform.SetParent(root.transform, false);

            var rig = root.AddComponent<ApartmentLightRig>();

            // 1. Living Room Light (거실 & 다이닝)
            rig.livingLight = AddRoomLight(lightsRoot.transform, "Light_Living", new Vector3(-1.1f, 2.15f, 3.2f), new Color(1f, 0.93f, 0.82f), 3.4f, 8f);
            // 2. Master Bedroom Light (안방)
            rig.masterLight = AddRoomLight(lightsRoot.transform, "Light_Master", new Vector3(2.2f, 2.0f, 2.2f), new Color(1f, 0.90f, 0.78f), 1.5f, 6.5f);
            // 3. Bedroom 1 Light (침실)
            rig.bedroomLight = AddRoomLight(lightsRoot.transform, "Light_Bedroom1", new Vector3(2.4f, 2.0f, -0.6f), new Color(1f, 0.92f, 0.80f), 1.4f, 6.5f);
            // 4. Bedroom 2 Light (침실 2)
            rig.bedroom2Light = AddRoomLight(lightsRoot.transform, "Light_Bedroom2", new Vector3(0.2f, 2.0f, -2.4f), new Color(1f, 0.91f, 0.78f), 1.4f, 6.5f);
            // 5. Kitchen Light (주방)
            rig.kitchenLight = AddRoomLight(lightsRoot.transform, "Light_Kitchen", new Vector3(-0.5f, 2.0f, 0.1f), new Color(1f, 0.96f, 0.88f), 1.5f, 6.5f);

            // Ambient / Fill light for interior
            rig.hallwayLight = AddRoomLight(lightsRoot.transform, "Light_Hallway", new Vector3(0.2f, 2.4f, 0.2f), new Color(0.88f, 0.92f, 1.0f), 0.45f, 14f);
            rig.hallwayLight.shadows = LightShadows.None;

            // Heating Floor Warm Glow Lights
            var heatsRoot = new GameObject("Heaters");
            heatsRoot.transform.SetParent(root.transform, false);
            rig.livingHeaterLight = AddRoomLight(heatsRoot.transform, "HeatGlow_Living", new Vector3(-1.1f, 0.42f, 3.2f), new Color(1.0f, 0.38f, 0.12f), 2.0f, 5.2f);
            rig.livingHeaterLight.shadows = LightShadows.None;
            rig.livingHeaterLight.enabled = false;
            rig.masterHeaterLight = AddRoomLight(heatsRoot.transform, "HeatGlow_Master", new Vector3(2.2f, 0.42f, 2.2f), new Color(1.0f, 0.38f, 0.12f), 2.2f, 4.4f);
            rig.masterHeaterLight.shadows = LightShadows.None;
            rig.masterHeaterLight.enabled = false;
            rig.bedroomHeaterLight = AddRoomLight(heatsRoot.transform, "HeatGlow_Bedroom", new Vector3(2.4f, 0.42f, -0.6f), new Color(1.0f, 0.38f, 0.12f), 2.2f, 4.4f);
            rig.bedroomHeaterLight.shadows = LightShadows.None;
            rig.bedroomHeaterLight.enabled = false;

            // Light strip emission renderers
            rig.luzRipadoRenderer = rendLuzRipado;
            rig.luzParedeRenderer = rendLuzParede;

            // Curtain transform & cloth
            if (curtainTrans == null)
            {
                var allTrans = model.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTrans)
                {
                    if (t.name.Contains("cortina") || t.name.Contains("Object_7"))
                    {
                        curtainTrans = t;
                        break;
                    }
                }
            }
            rig.curtainTransform = curtainTrans;
            if (curtainTrans != null)
            {
                rig.curtainCloth = curtainTrans.GetComponent<CurtainCloth>() ?? curtainTrans.gameObject.AddComponent<CurtainCloth>();
            }

            // Create Anchors for Interaction
            var anchorsRoot = new GameObject("Anchors");
            anchorsRoot.transform.SetParent(root.transform, false);
            rig.livingAnchor = CreateAnchor(anchorsRoot.transform, "Anchor_Living", new Vector3(-1.1f, 1.2f, 3.2f));
            rig.masterAnchor = CreateAnchor(anchorsRoot.transform, "Anchor_Master", new Vector3(2.2f, 1.2f, 2.2f));
            rig.bedroomAnchor = CreateAnchor(anchorsRoot.transform, "Anchor_Bedroom", new Vector3(2.4f, 1.2f, -0.6f));
            rig.bedroom2Anchor = CreateAnchor(anchorsRoot.transform, "Anchor_Bedroom2", new Vector3(0.2f, 1.2f, -2.4f));
            rig.kitchenAnchor = CreateAnchor(anchorsRoot.transform, "Anchor_Kitchen", new Vector3(-0.5f, 1.2f, 0.1f));
            rig.entranceAnchor = CreateAnchor(anchorsRoot.transform, "Anchor_Entrance", new Vector3(-3.8f, 1.0f, -3.2f));
            rig.curtainAnchor = CreateAnchor(anchorsRoot.transform, "Anchor_Curtain", new Vector3(-2.9f, 1.2f, 4.8f));

            // Save Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"ModernApartment prefab created successfully at {PrefabPath}");
            return prefab;
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        private static Light AddRoomLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.Soft;
            return l;
        }

        private static Material CreateOrUpdateMat(string name, Shader shader, System.Action<Material> configure)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            configure(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
