using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Homepad.Home;

namespace Homepad.Editor
{
    public static class HomeKitBaker
    {
        const string Root = "Assets/Home/Kit";
        const float W = 6.4f;
        const float H = 2.4f;
        const float D = 6.4f;

        public static void Bake()
        {
            EnsureFolder("Assets/Home");
            EnsureFolder(Root);
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Meshes");
            EnsureFolder(Root + "/Prefabs");

            var oak = Lit(Root + "/Materials/FloorOak.mat", new Color(0.55f, 0.38f, 0.22f), 0.28f);
            var plaster = Lit(Root + "/Materials/WallPlaster.mat", new Color(0.93f, 0.91f, 0.86f), 0.18f);
            var trim = Lit(Root + "/Materials/Trim.mat", new Color(0.36f, 0.24f, 0.16f), 0.32f);
            var fabric = Lit(Root + "/Materials/SofaFabric.mat", new Color(0.42f, 0.38f, 0.34f), 0.22f);
            var cushion = Lit(Root + "/Materials/Cushion.mat", new Color(0.62f, 0.28f, 0.24f), 0.2f);
            var rug = Lit(Root + "/Materials/Rug.mat", new Color(0.22f, 0.28f, 0.32f), 0.12f);
            var glass = Lit(Root + "/Materials/WindowGlass.mat", new Color(0.55f, 0.72f, 0.88f, 0.28f), 0.85f, true);
            var sky = Emissive(Root + "/Materials/SkyFill.mat", new Color(0.55f, 0.7f, 0.95f), new Color(1.1f, 1.3f, 1.8f));
            var metal = Lit(Root + "/Materials/LampMetal.mat", new Color(0.72f, 0.7f, 0.66f), 0.7f);
            var shade = Emissive(Root + "/Materials/LampShade.mat", new Color(0.96f, 0.9f, 0.75f), new Color(0.15f, 0.12f, 0.08f));
            var heat = Lit(Root + "/Materials/HeaterBody.mat", new Color(0.4f, 0.38f, 0.36f), 0.45f);
            var plate = Emissive(Root + "/Materials/HeaterPlate.mat", new Color(0.45f, 0.22f, 0.14f), Color.black);
            var cloth = Lit(Root + "/Materials/CurtainFabric.mat", new Color(0.86f, 0.82f, 0.76f), 0.08f);
            var table = Lit(Root + "/Materials/TableWood.mat", new Color(0.32f, 0.2f, 0.12f), 0.4f);

            var room = BuildLivingRoom(oak, plaster, trim, fabric, cushion, rug, glass, sky, table);
            var lamp = BuildLamp(metal, shade);
            var heater = BuildHeater(heat, plate);
            var curtain = BuildCurtain(cloth);

            AssignToScene(room, lamp, heater, curtain);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static GameObject BuildLivingRoom(Material oak, Material plaster, Material trim, Material fabric,
            Material cushion, Material rug, Material glass, Material sky, Material table)
        {
            var root = new GameObject("LivingRoom");
            Box("Floor", root.transform, new Vector3(W * 0.5f, -0.02f, D * 0.5f), new Vector3(W, 0.04f, D), oak);
            // North + East walls kept for isometric cutaway
            Box("WallNorth", root.transform, new Vector3(W * 0.5f, H * 0.5f, D - 0.04f), new Vector3(W, H, 0.08f), plaster);
            Box("WallEast", root.transform, new Vector3(W - 0.04f, H * 0.5f, D * 0.5f), new Vector3(0.08f, H, D), plaster);
            Box("BaseNorth", root.transform, new Vector3(W * 0.5f, 0.05f, D - 0.07f), new Vector3(W, 0.1f, 0.04f), trim);
            Box("BaseEast", root.transform, new Vector3(W - 0.07f, 0.05f, D * 0.5f), new Vector3(0.04f, 0.1f, D), trim);

            var win = Box("WindowFrame", root.transform, new Vector3(W * 0.5f, 1.35f, D - 0.02f), new Vector3(3.2f, 1.5f, 0.06f), trim);
            Box("Glass", win.transform, Vector3.zero, new Vector3(0.92f, 0.88f, 0.4f), glass);
            Box("Sky", root.transform, new Vector3(W * 0.5f, 1.35f, D + 0.2f), new Vector3(3.0f, 1.4f, 0.02f), sky);

            Box("Rug", root.transform, new Vector3(2.9f, 0.015f, 2.7f), new Vector3(2.8f, 0.03f, 2.2f), rug);
            Box("SofaBase", root.transform, new Vector3(2.4f, 0.28f, 4.7f), new Vector3(2.4f, 0.42f, 0.85f), fabric);
            Box("SofaBack", root.transform, new Vector3(2.4f, 0.7f, 5.05f), new Vector3(2.4f, 0.55f, 0.22f), fabric);
            Box("CushionL", root.transform, new Vector3(1.7f, 0.55f, 4.65f), new Vector3(0.7f, 0.18f, 0.55f), cushion);
            Box("CushionR", root.transform, new Vector3(3.1f, 0.55f, 4.65f), new Vector3(0.7f, 0.18f, 0.55f), cushion);
            Box("Table", root.transform, new Vector3(2.5f, 0.22f, 3.55f), new Vector3(1.2f, 0.08f, 0.7f), table);
            Box("Leg1", root.transform, new Vector3(2.05f, 0.1f, 3.3f), new Vector3(0.06f, 0.2f, 0.06f), table);
            Box("Leg2", root.transform, new Vector3(2.95f, 0.1f, 3.3f), new Vector3(0.06f, 0.2f, 0.06f), table);
            Box("Leg3", root.transform, new Vector3(2.05f, 0.1f, 3.8f), new Vector3(0.06f, 0.2f, 0.06f), table);
            Box("Leg4", root.transform, new Vector3(2.95f, 0.1f, 3.8f), new Vector3(0.06f, 0.2f, 0.06f), table);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.position = new Vector3(W * 0.35f, 3.2f, D * 0.2f);
            fillGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1f, 0.96f, 0.88f);
            fill.intensity = 0.12f;
            fill.shadows = LightShadows.Soft;

            var rig = root.AddComponent<RoomLightRig>();
            var so = new SerializedObject(rig);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, Root + "/Prefabs/LivingRoom.prefab");
        }

        static GameObject BuildLamp(Material metal, Material shade)
        {
            var root = new GameObject("CeilingLamp");
            Box("Stem", root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.04f, 0.24f, 0.04f), metal);
            var shadeGo = Box("Shade", root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.55f, 0.12f, 0.55f), shade);
            var lightGo = new GameObject("Bulb");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            var lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 9f;
            lamp.intensity = 0.15f;
            lamp.color = new Color(1f, 0.93f, 0.78f);
            lamp.shadows = LightShadows.Soft;
            return SavePrefab(root, Root + "/Prefabs/CeilingLamp.prefab");
        }

        static GameObject BuildHeater(Material body, Material plateMat)
        {
            var root = new GameObject("WallHeater");
            Box("Body", root.transform, Vector3.zero, new Vector3(0.7f, 0.55f, 0.08f), body);
            var plate = Box("Plate", root.transform, new Vector3(0f, 0f, -0.03f), new Vector3(0.58f, 0.38f, 0.02f), plateMat);
            var glow = root.AddComponent<HeaterGlow>();
            var so = new SerializedObject(glow);
            var plates = so.FindProperty("plates");
            plates.arraySize = 1;
            plates.GetArrayElementAtIndex(0).objectReferenceValue = plate.GetComponent<MeshRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, Root + "/Prefabs/WallHeater.prefab");
        }

        static GameObject BuildCurtain(Material cloth)
        {
            var root = new GameObject("Curtain");
            var panel = new GameObject("Cloth");
            panel.transform.SetParent(root.transform, false);
            var filter = panel.AddComponent<MeshFilter>();
            filter.sharedMesh = CurtainMesh();
            var renderer = panel.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = cloth;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            panel.AddComponent<CurtainCloth>();
            panel.transform.localScale = new Vector3(1.55f, 1.85f, 1f);
            return SavePrefab(root, Root + "/Prefabs/Curtain.prefab");
        }

        static Mesh CurtainMesh()
        {
            const int nx = 14;
            const int ny = 18;
            var verts = new Vector3[(nx + 1) * (ny + 1)];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[nx * ny * 6];
            int vi = 0;
            for (int y = 0; y <= ny; y++)
            {
                float v = y / (float)ny;
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx;
                    verts[vi] = new Vector3(u - 0.5f, v, 0f);
                    norms[vi] = Vector3.back;
                    uvs[vi] = new Vector2(u, v);
                    vi++;
                }
            }

            int ti = 0;
            for (int y = 0; y < ny; y++)
            {
                for (int x = 0; x < nx; x++)
                {
                    int i = y * (nx + 1) + x;
                    tris[ti++] = i;
                    tris[ti++] = i + nx + 1;
                    tris[ti++] = i + 1;
                    tris[ti++] = i + 1;
                    tris[ti++] = i + nx + 1;
                    tris[ti++] = i + nx + 2;
                }
            }

            var mesh = new Mesh { name = "CurtainCloth" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.normals = norms;
            mesh.uv = uvs;
            AssetDatabase.CreateAsset(mesh, Root + "/Meshes/CurtainCloth.asset");
            return mesh;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        static Material Lit(string path, Color color, float smoothness, bool transparent = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material Emissive(string path, Color color, Color emission)
        {
            var mat = Lit(path, color, 0.4f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject SavePrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void AssignToScene(GameObject room, GameObject lamp, GameObject heater, GameObject curtain)
        {
            var builder = Object.FindFirstObjectByType<IsometricHomeBuilder>();
            if (builder == null) return;
            var so = new SerializedObject(builder);
            so.FindProperty("livingRoomPrefab").objectReferenceValue = room;
            so.FindProperty("ceilingLampPrefab").objectReferenceValue = lamp;
            so.FindProperty("wallHeaterPrefab").objectReferenceValue = heater;
            so.FindProperty("curtainPrefab").objectReferenceValue = curtain;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(builder);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
