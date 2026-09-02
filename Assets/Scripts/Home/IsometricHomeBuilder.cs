using System.Collections.Generic;
using Homepad.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Homepad.Home
{
    public class IsometricHomeBuilder : MonoBehaviour
    {
        private HomeLayout layout;
        private Transform geometryRoot;
        private Transform itemRoot;
        private Transform ghostRoot;
        private readonly Dictionary<string, HomeItemView> views = new Dictionary<string, HomeItemView>();
        private readonly List<Material> materials = new List<Material>();

        private Material floorMat;
        private Material translucentWallMat;
        private Material edgeLineMat;
        private Material doorFrameMat;
        private Material windowFrameMat;
        private Material glassMat;
        private Material plinthMat;
        private Material groundMat;
        private Material ghostMat;
        private Material lampMat;
        private Material curtainMat;

        private readonly Dictionary<RoomHint, Material> roomFloors = new Dictionary<RoomHint, Material>();
        private DioramaRoomRig dioramaRig;

        public IReadOnlyDictionary<string, HomeItemView> Views => views;
        public DioramaRoomRig DioramaRig => dioramaRig;

        public void SetGroundColor(Color color)
        {
            if (groundMat == null) return;
            SetMatColor(groundMat, color);
        }

        public void Initialize(HomeLayout homeLayout)
        {
            layout = homeLayout;
            EnsureMaterials();

            if (geometryRoot == null)
            {
                geometryRoot = new GameObject("Geometry").transform;
                geometryRoot.SetParent(transform, false);
            }

            if (itemRoot == null)
            {
                itemRoot = new GameObject("Items").transform;
                itemRoot.SetParent(transform, false);
            }

            if (ghostRoot == null)
            {
                ghostRoot = new GameObject("Ghost").transform;
                ghostRoot.SetParent(transform, false);
            }
        }

        public void Rebuild()
        {
            if (layout == null) return;
            ClearChildren(geometryRoot);
            BuildGround();
            BuildRooms();
            RebuildItems();
        }

        public void RebuildItems()
        {
            if (layout == null) return;
            ClearChildren(itemRoot);
            views.Clear();
            for (int i = 0; i < layout.Items.Count; i++)
            {
                CreateItemVisual(layout.Items[i]);
            }

            RefreshItemStates();
        }

        public void RefreshItemStates()
        {
            var manager = WallpadManager.Instance;
            if (dioramaRig != null && manager != null)
            {
                for (int i = 0; i < layout.Items.Count; i++)
                {
                    var item = layout.Items[i];
                    if (item.Kind == HomeItemKind.Light)
                    {
                        var light = FindLight(manager, item.DeviceId);
                        bool on = light != null && light.isOn;
                        dioramaRig.SetLight(item.RoomHint, on);
                    }
                    else if (item.Kind == HomeItemKind.Heating)
                    {
                        var heat = FindHeat(manager, item.DeviceId);
                        bool on = heat != null && heat.isPowered && !heat.isAwayMode;
                        dioramaRig.SetHeating(item.RoomHint, on);
                    }
                }
            }

            foreach (var pair in views)
            {
                ApplyItemState(pair.Value, manager);
            }
        }

        public void ShowGhost(HomeItemDef def, Vector2Int cell, int wallDir, bool valid)
        {
            if (ghostRoot.childCount == 0)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "GhostMesh";
                cube.transform.SetParent(ghostRoot, false);
                Object.Destroy(cube.GetComponent<Collider>());
                var renderer = cube.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ghostMat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            ghostRoot.gameObject.SetActive(true);
            var t = ghostRoot.GetChild(0);
            Color c = valid ? new Color(0.35f, 0.75f, 1f, 0.45f) : new Color(1f, 0.3f, 0.3f, 0.35f);
            SetMatColor(ghostMat, c);

            if (dioramaRig != null)
            {
                var anchor = dioramaRig.GetAnchor(def.Kind, def.RoomHint);
                t.position = anchor != null ? anchor.position : layout.CellCenter(cell, 1.0f);
            }
            else
            {
                t.position = layout != null ? layout.CellCenter(cell, 1.0f) : Vector3.zero;
            }

            t.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            t.rotation = Quaternion.identity;
        }

        public void HideGhost()
        {
            if (ghostRoot != null) ghostRoot.gameObject.SetActive(false);
        }

        private void BuildGround()
        {
            float size = 32f;
            var mesh = CreateQuadMesh(
                new Vector3(-size, -0.05f, -size),
                new Vector3(size, -0.05f, -size),
                new Vector3(size, -0.05f, size),
                new Vector3(-size, -0.05f, size),
                Vector3.up);
            CreateMeshObject("Ground", geometryRoot, mesh, groundMat, false);
        }

        private void BuildRooms()
        {
            EnsureMaterials();

            // 1. Procedural 3D Mesh Generation via HomeDioramaBuilder
            var output = HomeDioramaBuilder.Generate(layout);

            // Create Room Floor meshes
            foreach (var pair in output.RoomFloors)
            {
                var mesh = pair.Value.ToMesh($"Floor_{pair.Key}");
                if (mesh != null)
                {
                    var mat = RoomFloor(pair.Key);
                    CreateMeshObject($"Floor_{pair.Key}", geometryRoot, mesh, mat, true);
                }
            }

            // Create Translucent Glass Walls (No collider so mouse raycast passes through to devices effortlessly)
            CreateMeshObject("TranslucentWalls", geometryRoot, output.TranslucentWalls.ToMesh("TranslucentWalls"), translucentWallMat, false);

            // Create Glowing Architectural Edge Lines (Outlines)
            CreateMeshObject("EdgeLines", geometryRoot, output.EdgeLines.ToMesh("EdgeLines"), edgeLineMat, false);

            // Create Door & Window Frames & Glass
            CreateMeshObject("DoorFrames", geometryRoot, output.DoorFrames.ToMesh("DoorFrames"), doorFrameMat, false);
            CreateMeshObject("WindowFrames", geometryRoot, output.WindowFrames.ToMesh("WindowFrames"), windowFrameMat, false);
            CreateMeshObject("WindowGlass", geometryRoot, output.WindowGlass.ToMesh("WindowGlass"), glassMat, false);
            CreateMeshObject("Plinth", geometryRoot, output.PlinthData.ToMesh("Plinth"), plinthMat, false);

            // 2. Setup Procedural Diorama Room Rig (Lights, Ceiling Lamps, Anchors)
            var rigGo = new GameObject("DioramaRoomRig");
            rigGo.transform.SetParent(geometryRoot, false);
            dioramaRig = rigGo.AddComponent<DioramaRoomRig>();

            var lampsRoot = new GameObject("CeilingLamps");
            lampsRoot.transform.SetParent(rigGo.transform, false);

            var lightsRoot = new GameObject("RoomLights");
            lightsRoot.transform.SetParent(rigGo.transform, false);

            var heatersRoot = new GameObject("HeaterLights");
            heatersRoot.transform.SetParent(rigGo.transform, false);

            var anchorsRoot = new GameObject("Anchors");
            anchorsRoot.transform.SetParent(rigGo.transform, false);

            foreach (var room in layout.Rooms)
            {
                Vector3 roomCenter = new Vector3(
                    (room.Origin.x + room.Size.x * 0.5f) * HomeLayout.CellSize,
                    0f,
                    (room.Origin.y + room.Size.y * 0.5f) * HomeLayout.CellSize
                );
                float roomCeilingY = 2.05f;

                // Create Minimalist Luminous Pendant Lamp
                var lampGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lampGo.name = $"Lamp_{room.Hint}";
                lampGo.transform.SetParent(lampsRoot.transform, false);
                lampGo.transform.position = new Vector3(roomCenter.x, roomCeilingY, roomCenter.z);
                lampGo.transform.localScale = new Vector3(0.35f, 0.035f, 0.35f);
                Object.Destroy(lampGo.GetComponent<Collider>());
                var lampRend = lampGo.GetComponent<MeshRenderer>();
                lampRend.sharedMaterial = new Material(lampMat);

                // Create Room Point Light
                var lightGo = new GameObject($"Light_{room.Hint}");
                lightGo.transform.SetParent(lightsRoot.transform, false);
                lightGo.transform.position = new Vector3(roomCenter.x, roomCeilingY - 0.25f, roomCenter.z);
                var pLight = lightGo.AddComponent<Light>();
                pLight.type = LightType.Point;
                pLight.color = new Color(1.0f, 0.95f, 0.88f);
                pLight.intensity = 1.35f;
                pLight.range = 6.5f;
                pLight.shadows = LightShadows.Soft;

                // Create Floor Heater Warm Glow Light
                var heatGo = new GameObject($"HeatGlow_{room.Hint}");
                heatGo.transform.SetParent(heatersRoot.transform, false);
                heatGo.transform.position = new Vector3(roomCenter.x, 0.25f, roomCenter.z);
                var hLight = heatGo.AddComponent<Light>();
                hLight.type = LightType.Point;
                hLight.color = new Color(1.0f, 0.45f, 0.15f);
                hLight.intensity = 1.0f;
                hLight.range = 4.5f;
                hLight.shadows = LightShadows.None;
                hLight.enabled = false;

                // Anchors
                var ceilAnchor = CreateAnchor(anchorsRoot.transform, $"Anchor_Ceil_{room.Hint}", new Vector3(roomCenter.x, roomCeilingY - 0.1f, roomCenter.z));
                var wallAnchor = CreateAnchor(anchorsRoot.transform, $"Anchor_Wall_{room.Hint}", new Vector3(roomCenter.x, 1.2f, roomCenter.z + 1.2f));
                var floorAnchor = CreateAnchor(anchorsRoot.transform, $"Anchor_Floor_{room.Hint}", new Vector3(roomCenter.x, 0.4f, roomCenter.z));

                var fixture = new DioramaRoomRig.RoomFixture
                {
                    hint = room.Hint,
                    roomLight = pLight,
                    heaterLight = hLight,
                    lampRenderer = lampRend,
                    ceilingAnchor = ceilAnchor,
                    wallAnchor = wallAnchor,
                    floorAnchor = floorAnchor
                };

                dioramaRig.RegisterFixture(fixture);
            }
        }

        private Transform CreateAnchor(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go.transform;
        }

        private void CreateItemVisual(PlacedItem item)
        {
            var go = new GameObject($"Item_{item.DisplayName}_{item.InstanceId}");
            go.transform.SetParent(itemRoot, false);
            var view = go.AddComponent<HomeItemView>();

            Transform curtainLeaf = null;

            if (item.Kind == HomeItemKind.ElectricCurtain)
            {
                // Position curtain on the window wall
                Vector3 wallPos = layout != null ? layout.WallCenter(item.Cell, item.WallDir, 1.35f) : Vector3.zero;
                go.transform.position = wallPos;

                var curtainMeshGo = new GameObject("CurtainMesh");
                curtainMeshGo.transform.SetParent(go.transform, false);

                float w = HomeLayout.CellSize * 0.9f;
                float h = 1.0f;
                var mesh = CreateQuadMesh(
                    new Vector3(-w * 0.5f, -h * 0.5f, 0f),
                    new Vector3(w * 0.5f, -h * 0.5f, 0f),
                    new Vector3(w * 0.5f, h * 0.5f, 0f),
                    new Vector3(-w * 0.5f, h * 0.5f, 0f),
                    Vector3.forward);

                var filter = curtainMeshGo.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var rend = curtainMeshGo.AddComponent<MeshRenderer>();
                rend.sharedMaterial = curtainMat;
                rend.shadowCastingMode = ShadowCastingMode.On;

                float angle = item.WallDir switch
                {
                    0 => 0f,
                    1 => 90f,
                    2 => 180f,
                    _ => 270f
                };
                curtainMeshGo.transform.localRotation = Quaternion.Euler(0f, angle, 0f);

                var cloth = curtainMeshGo.AddComponent<CurtainCloth>();
                cloth.Initialize();
                cloth.SetOpen(item.CurtainOpen);
                curtainLeaf = curtainMeshGo.transform;
            }
            else if (dioramaRig != null)
            {
                var anchor = dioramaRig.GetAnchor(item.Kind, item.RoomHint);
                go.transform.position = anchor != null ? anchor.position : layout.CellCenter(item.Cell, 1.0f);
            }
            else
            {
                go.transform.position = layout != null ? layout.CellCenter(item.Cell, 1.0f) : Vector3.zero;
            }

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.85f, 0.85f, 0.85f);

            view.Bind(item, curtainLeaf, curtainLeaf);
            views[item.InstanceId] = view;
        }

        private void ApplyItemState(HomeItemView view, WallpadManager manager)
        {
            if (view == null || view.Item == null) return;
        }

        private static LightState FindLight(WallpadManager manager, int id)
        {
            if (manager == null) return null;
            for (int i = 0; i < manager.Lights.Count; i++)
            {
                if (manager.Lights[i].id == id) return manager.Lights[i];
            }

            return null;
        }

        private static HeatingState FindHeat(WallpadManager manager, int roomId)
        {
            if (manager == null) return null;
            for (int i = 0; i < manager.HeatingRooms.Count; i++)
            {
                if (manager.HeatingRooms[i].roomId == roomId) return manager.HeatingRooms[i];
            }

            return null;
        }

        private void EnsureMaterials()
        {
            if (floorMat != null) return;
            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // 1. Futuristic Translucent Frosted Glass Wall (Alpha ~0.20, Glossy, Non-occluding)
            translucentWallMat = CreateMat("Mat_Futuristic_TranslucentWall", litShader, new Color(0.86f, 0.92f, 0.98f, 0.20f), 0.92f);
            translucentWallMat.SetFloat("_Surface", 1);
            translucentWallMat.SetFloat("_Blend", 0);
            translucentWallMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            translucentWallMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            translucentWallMat.SetInt("_ZWrite", 0);
            translucentWallMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            translucentWallMat.renderQueue = (int)RenderQueue.Transparent + 10;

            // 2. Glowing Architectural Edge Line Outline (Bright crisp Ice White/Cyan accent with subtle emission)
            edgeLineMat = CreateMat("Mat_Futuristic_EdgeLine", litShader, new Color(0.92f, 0.96f, 1.0f, 1.0f), 0.60f);
            edgeLineMat.EnableKeyword("_EMISSION");
            edgeLineMat.SetColor("_EmissionColor", new Color(0.70f, 0.85f, 1.0f) * 1.5f);

            // 3. Sleek Door & Window Frames (Matte Titanium Slate)
            doorFrameMat = CreateMat("Mat_Futuristic_DoorFrame", litShader, new Color(0.55f, 0.62f, 0.72f), 0.50f);
            windowFrameMat = CreateMat("Mat_Futuristic_WindowFrame", litShader, new Color(0.60f, 0.68f, 0.78f), 0.50f);

            // 4. Plinth Base (Dark Modern Floating Slab)
            plinthMat = CreateMat("Mat_Futuristic_Plinth", litShader, new Color(0.16f, 0.18f, 0.22f), 0.35f);

            // 5. Ground Plane
            groundMat = CreateMat("Mat_Futuristic_Ground", litShader, new Color(0.08f, 0.09f, 0.12f), 0.15f);

            // 6. Ghost Placement
            ghostMat = CreateMat("Mat_Futuristic_Ghost", litShader, new Color(0.35f, 0.75f, 1f, 0.45f), 0.5f);

            // 7. Fabric Curtain
            curtainMat = CreateMat("Mat_Futuristic_Curtain", litShader, new Color(0.94f, 0.92f, 0.88f), 0.20f);

            // 8. Ceiling Lamp Fixture
            lampMat = CreateMat("Mat_Futuristic_Lamp", litShader, new Color(1.0f, 0.96f, 0.88f), 0.6f);
            lampMat.EnableKeyword("_EMISSION");
            lampMat.SetColor("_EmissionColor", new Color(1f, 0.94f, 0.82f) * 2.8f);

            // 9. Transparent Window Glass
            glassMat = CreateMat("Mat_Futuristic_Glass", litShader, new Color(0.65f, 0.82f, 0.95f, 0.35f), 0.92f);
            glassMat.SetFloat("_Surface", 1);
            glassMat.SetFloat("_Blend", 0);
            glassMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            glassMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            glassMat.SetInt("_ZWrite", 0);
            glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glassMat.renderQueue = (int)RenderQueue.Transparent + 20;

            floorMat = CreateMat("Mat_Floor_Default", litShader, new Color(0.92f, 0.82f, 0.70f), 0.35f);

            // Curated Room Floor Palettes
            roomFloors[RoomHint.Living] = CreateMat("Floor_Living_Oak", litShader, new Color(0.94f, 0.82f, 0.68f), 0.38f);
            roomFloors[RoomHint.Master] = CreateMat("Floor_Master_Walnut", litShader, new Color(0.88f, 0.76f, 0.62f), 0.35f);
            roomFloors[RoomHint.Bedroom] = CreateMat("Floor_Bedroom_Birch", litShader, new Color(0.96f, 0.86f, 0.74f), 0.35f);
            roomFloors[RoomHint.Bedroom2] = CreateMat("Floor_Bedroom2_Honey", litShader, new Color(0.92f, 0.80f, 0.66f), 0.35f);
            roomFloors[RoomHint.Kitchen] = CreateMat("Floor_Kitchen_Tile", litShader, new Color(0.94f, 0.94f, 0.92f), 0.55f);
            roomFloors[RoomHint.Entrance] = CreateMat("Floor_Entrance_Stone", litShader, new Color(0.42f, 0.45f, 0.50f), 0.35f);
        }

        private Material CreateMat(string name, Shader shader, Color color, float smoothness)
        {
            var mat = new Material(shader) { name = name };
            SetMatColor(mat, color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            materials.Add(mat);
            return mat;
        }

        private Material RoomFloor(RoomHint hint)
        {
            if (roomFloors.TryGetValue(hint, out var m)) return m;
            return floorMat;
        }

        private static void SetMatColor(Material mat, Color c)
        {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }

        private static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                if (Application.isPlaying) Object.Destroy(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Mesh CreateQuadMesh(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
        {
            var mesh = new Mesh { name = "Quad" };
            mesh.vertices = new[] { p0, p1, p2, p3 };
            mesh.normals = new[] { normal, normal, normal, normal };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material mat, bool collider)
        {
            if (mesh == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.On;
            rend.receiveShadows = true;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }
    }
}
