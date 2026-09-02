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

        [SerializeField] private GameObject ceilingLampPrefab;
        [SerializeField] private GameObject wallHeaterPrefab;
        [SerializeField] private GameObject curtainPrefab;

        private Material floorMat;
        private Material translucentWallMat;
        private Material edgeLineMat;
        private Material doorFrameMat;
        private Material windowFrameMat;
        private Material glassMat;
        private Material plinthMat;
        private Material groundMat;
        private Material ghostMat;

        private readonly Dictionary<RoomHint, Material> roomFloors = new Dictionary<RoomHint, Material>();
        private DioramaRoomRig dioramaRig;
        private HomeLayout.CutawayView cutawayView;

        public IReadOnlyDictionary<string, HomeItemView> Views => views;
        public DioramaRoomRig DioramaRig => dioramaRig;

        public void SetGroundColor(Color color)
        {
            if (groundMat == null) return;
            SetMatColor(groundMat, color);
        }

        public void SetShellLook(Color wall, Color edge, Color edgeEmission, Color frame)
        {
            EnsureMaterials();
            SetMatColor(translucentWallMat, wall);
            SetMatColor(edgeLineMat, edge);
            SetEmission(edgeLineMat, edgeEmission);
            SetMatColor(doorFrameMat, frame);
            SetMatColor(windowFrameMat, frame);
        }

        public void Initialize(HomeLayout homeLayout)
        {
            layout = homeLayout;
            EnsureKitPrefabs();
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
            if (dioramaRig == null) return;

            dioramaRig.SetAllLights(false);
            if (layout == null) return;

            for (int i = 0; i < layout.Items.Count; i++)
            {
                var item = layout.Items[i];
                if (item.Kind == HomeItemKind.Light)
                {
                    var light = FindLight(manager, item.DeviceId);
                    dioramaRig.SetLight(item.RoomHint, light != null && light.isOn);
                }
                else if (item.Kind == HomeItemKind.Heating)
                {
                    var heat = FindHeat(manager, item.DeviceId);
                    dioramaRig.SetHeating(item.RoomHint, heat != null && heat.isPowered && !heat.isAwayMode);
                }
                else if (item.Kind == HomeItemKind.ElectricCurtain)
                {
                    dioramaRig.SetCurtain(item.RoomHint, item.CurtainOpen);
                }
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
            t.position = GhostPosition(def, cell, wallDir);
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
            CreateMeshObject("Ground", geometryRoot, mesh, groundMat, false, true);
        }

        private void BuildRooms()
        {
            EnsureKitPrefabs();
            EnsureMaterials();
            cutawayView = HomeLayout.CutawayView.FromCamera(CameraForward());
            var output = HomeDioramaBuilder.Generate(layout, cutawayView.Forward);

            foreach (var pair in output.RoomFloors)
            {
                var mesh = pair.Value.ToMesh($"Floor_{pair.Key}");
                if (mesh != null)
                {
                    CreateMeshObject($"Floor_{pair.Key}", geometryRoot, mesh, RoomFloor(pair.Key), true, true);
                }
            }

            CreateMeshObject("TranslucentWalls", geometryRoot, output.TranslucentWalls.ToMesh("TranslucentWalls"), translucentWallMat, false, false);
            CreateMeshObject("EdgeLines", geometryRoot, output.EdgeLines.ToMesh("EdgeLines"), edgeLineMat, false, false);
            CreateMeshObject("Plinth", geometryRoot, output.PlinthData.ToMesh("Plinth"), plinthMat, false, true);

            var rigGo = new GameObject("DioramaRoomRig");
            rigGo.transform.SetParent(geometryRoot, false);
            dioramaRig = rigGo.AddComponent<DioramaRoomRig>();

            var anchorsRoot = new GameObject("Anchors");
            anchorsRoot.transform.SetParent(rigGo.transform, false);

            foreach (var room in layout.Rooms)
            {
                Vector3 roomCenter = layout.RoomCenter(room);
                layout.TryFindPerimeter(room, cutawayView.PrimaryBack, out var wallCell, out var wallDir);
                var ceilAnchor = CreateAnchor(anchorsRoot.transform, $"Anchor_Ceil_{room.Hint}", layout.RoomCenter(room, HomeDioramaBuilder.HighWallHeight - 0.18f));
                var wallAnchor = CreateAnchor(anchorsRoot.transform, $"Anchor_Wall_{room.Hint}", layout.WallCenter(wallCell, wallDir, 1.05f));
                var floorAnchor = CreateAnchor(anchorsRoot.transform, $"Anchor_Floor_{room.Hint}", new Vector3(roomCenter.x, 0.4f, roomCenter.z));

                dioramaRig.RegisterFixture(new DioramaRoomRig.RoomFixture
                {
                    hint = room.Hint,
                    ceilingAnchor = ceilAnchor,
                    wallAnchor = wallAnchor,
                    floorAnchor = floorAnchor
                });
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
            Transform visual = null;
            Transform curtainLeaf = null;

            switch (item.Kind)
            {
                case HomeItemKind.Light:
                    visual = SpawnLight(go.transform, item);
                    break;
                case HomeItemKind.Heating:
                    visual = SpawnHeater(go.transform, item);
                    break;
                case HomeItemKind.ElectricCurtain:
                    curtainLeaf = SpawnCurtain(go.transform, item);
                    visual = curtainLeaf;
                    break;
                default:
                    var anchor = dioramaRig != null ? dioramaRig.GetAnchor(item.Kind, item.RoomHint) : null;
                    go.transform.position = anchor != null ? anchor.position : layout.CellCenter(item.Cell, 1.0f);
                    break;
            }

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.8f, 0.8f, 0.8f);
            view.Bind(item, visual, curtainLeaf);
            views[item.InstanceId] = view;
        }

        private Transform SpawnLight(Transform parent, PlacedItem item)
        {
            var room = layout.FindRoom(item.RoomHint);
            parent.position = room != null
                ? layout.RoomCenter(room, HomeDioramaBuilder.HighWallHeight - 0.18f)
                : layout.CellCenter(item.Cell, HomeDioramaBuilder.HighWallHeight - 0.18f);
            parent.rotation = Quaternion.identity;

            var inst = SpawnKit(ceilingLampPrefab, parent, "CeilingLamp");
            var lightRig = inst.GetComponentInChildren<RoomLightRig>(true);
            var light = inst.GetComponentInChildren<Light>(true);
            if (light == null) light = inst.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.94f, 0.84f);
            light.range = 6.5f;
            light.shadows = LightShadows.Soft;
            light.enabled = false;
            light.intensity = 0.12f;

            MeshRenderer shade = null;
            var renderers = inst.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].name.IndexOf("shade", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    shade = renderers[i];
                    break;
                }
            }

            if (shade == null && renderers.Length > 0) shade = renderers[0];

            dioramaRig?.RegisterFixture(new DioramaRoomRig.RoomFixture
            {
                hint = item.RoomHint,
                roomLight = light,
                lampRenderer = shade,
                lightRig = lightRig,
                ceilingAnchor = parent
            });
            return inst.transform;
        }

        private Transform SpawnHeater(Transform parent, PlacedItem item)
        {
            parent.position = layout.WallCenter(item.Cell, item.WallDir, 0.52f);
            parent.rotation = Quaternion.LookRotation(-HomeLayout.DirNormal(item.WallDir));

            var inst = SpawnKit(wallHeaterPrefab, parent, "WallHeater");
            var glow = inst.GetComponentInChildren<HeaterGlow>(true);
            var heatLight = inst.GetComponentInChildren<Light>(true);
            if (heatLight == null)
            {
                heatLight = inst.AddComponent<Light>();
                heatLight.type = LightType.Point;
                heatLight.color = new Color(1f, 0.42f, 0.14f);
                heatLight.range = 4.5f;
                heatLight.intensity = 1.6f;
                heatLight.shadows = LightShadows.None;
            }

            heatLight.enabled = false;
            if (glow != null) glow.SetOn(false);

            dioramaRig?.RegisterFixture(new DioramaRoomRig.RoomFixture
            {
                hint = item.RoomHint,
                heaterLight = heatLight,
                heaterGlow = glow,
                wallAnchor = parent
            });
            return inst.transform;
        }

        private Transform SpawnCurtain(Transform parent, PlacedItem item)
        {
            parent.position = layout.WallCenter(item.Cell, item.WallDir, 1.2f);
            parent.rotation = Quaternion.LookRotation(-HomeLayout.DirNormal(item.WallDir));

            var inst = SpawnKit(curtainPrefab, parent, "Curtain");
            var cloth = inst.GetComponentInChildren<CurtainCloth>(true);
            if (cloth != null)
            {
                cloth.Initialize();
                cloth.SetOpen(item.CurtainOpen);
            }

            dioramaRig?.RegisterFixture(new DioramaRoomRig.RoomFixture
            {
                hint = item.RoomHint,
                curtainCloth = cloth,
                wallAnchor = parent
            });
            return inst.transform;
        }

        private static GameObject SpawnKit(GameObject prefab, Transform parent, string fallbackName)
        {
            if (prefab != null)
            {
                var inst = Object.Instantiate(prefab, parent, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                var colliders = inst.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++) Object.Destroy(colliders[i]);
                return inst;
            }

            var go = new GameObject(fallbackName);
            go.transform.SetParent(parent, false);
            return go;
        }

        private void EnsureKitPrefabs()
        {
#if UNITY_EDITOR
            if (ceilingLampPrefab == null)
                ceilingLampPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Home/Kit/Prefabs/CeilingLamp.prefab");
            if (wallHeaterPrefab == null)
                wallHeaterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Home/Kit/Prefabs/WallHeater.prefab");
            if (curtainPrefab == null)
                curtainPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Home/Kit/Prefabs/Curtain.prefab");
#endif
        }

        private static Vector3 CameraForward()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.forward : new Vector3(1f, -1f, 1f);
        }

        private Vector3 GhostPosition(HomeItemDef def, Vector2Int cell, int wallDir)
        {
            if (layout == null) return Vector3.zero;
            if (def != null && (def.Kind == HomeItemKind.ElectricCurtain || def.Surface == Surface.Wall))
                return layout.WallCenter(cell, wallDir, 1.05f);
            if (def != null && def.Surface == Surface.Ceiling)
                return layout.CellCenter(cell, HomeDioramaBuilder.HighWallHeight - 0.18f);
            return layout.CellCenter(cell, 0.45f);
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

            translucentWallMat = CreateMat("Mat_Wall_Translucent", litShader, new Color(0.96f, 0.93f, 0.88f, 0.32f), 0.55f);
            MakeTransparent(translucentWallMat, 10);

            edgeLineMat = CreateMat("Mat_Wall_Edge", litShader, new Color(0.82f, 0.76f, 0.68f), 0.35f);
            SetEmission(edgeLineMat, new Color(0.08f, 0.07f, 0.05f));

            doorFrameMat = CreateMat("Mat_Door_Frame", litShader, new Color(0.72f, 0.54f, 0.38f), 0.45f);
            windowFrameMat = CreateMat("Mat_Window_Frame", litShader, new Color(0.72f, 0.54f, 0.38f), 0.45f);
            plinthMat = CreateMat("Mat_Plinth", litShader, new Color(0.24f, 0.26f, 0.30f), 0.30f);
            groundMat = CreateMat("Mat_Ground", litShader, new Color(0.08f, 0.09f, 0.12f), 0.15f);
            ghostMat = CreateMat("Mat_Ghost", litShader, new Color(0.35f, 0.75f, 1f, 0.45f), 0.5f);

            glassMat = CreateMat("Mat_Window_Glass", litShader, new Color(0.65f, 0.82f, 0.95f, 0.28f), 0.92f);
            MakeTransparent(glassMat, 20);

            floorMat = CreateMat("Mat_Floor_Default", litShader, new Color(0.92f, 0.82f, 0.70f), 0.35f);
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

        private static void MakeTransparent(Material mat, int queueBias)
        {
            if (mat == null) return;
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent + queueBias;
        }

        private static void SetEmission(Material mat, Color emission)
        {
            if (mat == null || !mat.HasProperty("_EmissionColor")) return;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
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

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material mat, bool collider, bool shadows)
        {
            if (mesh == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            rend.receiveShadows = shadows;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }
    }
}
