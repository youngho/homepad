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

        [Header("Apartment Kit")]
        [SerializeField] private GameObject modernApartmentPrefab;
        [SerializeField] private GameObject ceilingLampPrefab;
        [SerializeField] private GameObject wallHeaterPrefab;
        [SerializeField] private GameObject curtainPrefab;

        private Material floorMat;
        private Material wallMat;
        private Material doorMat;
        private Material glassMat;
        private Material ceilingMat;
        private Material groundMat;
        private Material ghostMat;
        private readonly Dictionary<RoomHint, Material> roomFloors = new Dictionary<RoomHint, Material>();
        private ApartmentLightRig apartmentRig;

        public IReadOnlyDictionary<string, HomeItemView> Views => views;
        public ApartmentLightRig ApartmentRig => apartmentRig;

        public void Initialize(HomeLayout homeLayout)
        {
            layout = homeLayout;
#if UNITY_EDITOR
            if (modernApartmentPrefab == null)
            {
                modernApartmentPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Home/Kit/Prefabs/ModernApartment.prefab");
            }
#endif

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
            if (apartmentRig != null && manager != null)
            {
                // Update Apartment Dynamic Lighting & Heating
                for (int i = 0; i < layout.Items.Count; i++)
                {
                    var item = layout.Items[i];
                    if (item.Kind == HomeItemKind.Light)
                    {
                        var light = FindLight(manager, item.DeviceId);
                        bool on = light != null && light.isOn;
                        apartmentRig.SetLight(item.RoomHint, on);
                    }
                    else if (item.Kind == HomeItemKind.Heating)
                    {
                        var heat = FindHeat(manager, item.DeviceId);
                        bool on = heat != null && heat.isPowered && !heat.isAwayMode;
                        apartmentRig.SetHeating(item.RoomHint, on);
                    }
                    else if (item.Kind == HomeItemKind.ElectricCurtain)
                    {
                        apartmentRig.SetCurtain(item.CurtainOpen);
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

            if (apartmentRig != null)
            {
                var anchor = apartmentRig.GetAnchor(def.Kind, def.RoomHint);
                t.position = anchor != null ? anchor.position : Vector3.zero;
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
            float size = 48f;
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
            apartmentRig = null;

            if (modernApartmentPrefab != null)
            {
                var aptGo = Object.Instantiate(modernApartmentPrefab, geometryRoot);
                aptGo.name = "ModernApartment";
                aptGo.transform.localPosition = Vector3.zero;
                aptGo.transform.localRotation = Quaternion.identity;
                apartmentRig = aptGo.GetComponent<ApartmentLightRig>();
                return;
            }

            // Fallback: procedural boxes if modernApartmentPrefab is missing
            var floors = new Dictionary<RoomHint, MeshAccum>();
            var walls = new MeshAccum();
            var doors = new MeshAccum();
            var glass = new MeshAccum();
            var ceilings = new MeshAccum();

            foreach (var pair in layout.Cells)
            {
                var cell = pair.Value;
                if (!cell.HasFloor) continue;
                var room = layout.FindRoomById(cell.RoomId);
                var hint = room != null ? room.Hint : RoomHint.Living;
                if (!floors.TryGetValue(hint, out var floorAccum))
                {
                    floorAccum = new MeshAccum();
                    floors[hint] = floorAccum;
                }

                AddFloor(floorAccum, pair.Key);
                if (!layout.Cutaway) AddCeiling(ceilings, pair.Key);

                for (int d = 0; d < 4; d++)
                {
                    if (cell.Windows[d]) AddWindow(glass, walls, pair.Key, d);
                    else if (cell.Doors[d]) AddDoorFrame(doors, pair.Key, d);
                    else if (cell.Walls[d]) AddWall(walls, pair.Key, d, 0f, HomeLayout.WallHeight);
                }
            }

            foreach (var pair in floors)
            {
                var mesh = pair.Value.ToMesh();
                if (mesh == null) continue;
                var mat = RoomFloor(pair.Key);
                CreateMeshObject($"Floor_{pair.Key}", geometryRoot, mesh, mat, false);
            }

            CreateMeshObject("Walls", geometryRoot, walls.ToMesh(), wallMat, false);
            CreateMeshObject("Doors", geometryRoot, doors.ToMesh(), doorMat, false);
            CreateMeshObject("Glass", geometryRoot, glass.ToMesh(), glassMat, false);
        }

        private void CreateItemVisual(PlacedItem item)
        {
            var go = new GameObject($"Item_{item.DisplayName}_{item.InstanceId}");
            go.transform.SetParent(itemRoot, false);
            var view = go.AddComponent<HomeItemView>();

            if (apartmentRig != null)
            {
                var anchor = apartmentRig.GetAnchor(item.Kind, item.RoomHint);
                go.transform.position = anchor != null ? anchor.position : Vector3.zero;
            }
            else
            {
                go.transform.position = layout != null ? layout.CellCenter(item.Cell, 1.0f) : Vector3.zero;
            }

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.8f, 0.8f, 0.8f);

            view.Bind(item, null, null);
            views[item.InstanceId] = view;
        }

        private void ApplyItemState(HomeItemView view, WallpadManager manager)
        {
            if (view == null || view.Item == null) return;
            if (view.Visual == null) return;
            var renderer = view.Visual.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            var item = view.Item;

            Color color = new Color(0.65f, 0.68f, 0.75f);
            switch (item.Kind)
            {
                case HomeItemKind.Light:
                    var light = FindLight(manager, item.DeviceId);
                    bool lightOn = light != null && light.isOn;
                    color = lightOn ? new Color(1f, 0.94f, 0.55f) : new Color(0.45f, 0.48f, 0.55f);
                    var bulbLight = view.GetComponentInChildren<Light>(true);
                    if (bulbLight != null) bulbLight.enabled = lightOn;
                    break;
                case HomeItemKind.Heating:
                    var room = FindHeat(manager, item.DeviceId);
                    color = room != null && room.isPowered
                        ? (room.isAwayMode ? new Color(0.45f, 0.70f, 1.0f) : new Color(1.0f, 0.45f, 0.22f))
                        : new Color(0.38f, 0.40f, 0.46f);
                    break;
                case HomeItemKind.Gas:
                    bool open = manager != null && manager.Gas.isOpen;
                    color = open ? new Color(0.95f, 0.32f, 0.28f) : new Color(0.35f, 0.82f, 0.48f);
                    break;
                case HomeItemKind.Vent:
                    color = manager != null && manager.Ventilation.isPowered
                        ? new Color(0.4f, 0.78f, 1.0f)
                        : new Color(0.45f, 0.48f, 0.55f);
                    break;
                case HomeItemKind.Elevator:
                    color = manager != null && manager.Elevator.isCalled
                        ? new Color(0.38f, 0.65f, 1.0f)
                        : new Color(0.52f, 0.55f, 0.62f);
                    break;
                case HomeItemKind.ElectricCurtain:
                    color = new Color(0.85f, 0.78f, 0.68f);
                    break;
            }

            if (renderer.material != null)
            {
                renderer.material.color = color;
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", color * 0.8f);
                }
            }
        }

        private Transform SpawnKitOrPrimitive(Transform parent, GameObject prefab, PrimitiveType fallback, Vector3 scale)
        {
            if (prefab == null) return CreatePrimitive(parent, fallback, scale);
            var inst = Object.Instantiate(prefab, parent, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            return inst.transform;
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

        private Transform CreatePrimitive(Transform parent, PrimitiveType type, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = wallMat;
            return go.transform;
        }

        private void EnsureMaterials()
        {
            if (floorMat != null) return;
            floorMat = CreateMat("Mat_Floor", new Color(0.24f, 0.28f, 0.35f));
            wallMat = CreateMat("Mat_Wall", new Color(0.88f, 0.90f, 0.94f));
            doorMat = CreateMat("Mat_Door", new Color(0.55f, 0.42f, 0.32f));
            glassMat = CreateMat("Mat_Glass", new Color(0.55f, 0.75f, 0.95f, 0.35f));
            ceilingMat = CreateMat("Mat_Ceiling", new Color(0.92f, 0.93f, 0.96f));
            groundMat = CreateMat("Mat_Ground", new Color(0.06f, 0.07f, 0.10f));
            ghostMat = CreateMat("Mat_Ghost", new Color(0.35f, 0.75f, 1f, 0.45f));

            roomFloors[RoomHint.Living] = CreateMat("Floor_Living", new Color(0.26f, 0.30f, 0.38f));
            roomFloors[RoomHint.Master] = CreateMat("Floor_Master", new Color(0.32f, 0.28f, 0.34f));
            roomFloors[RoomHint.Bedroom] = CreateMat("Floor_Bedroom", new Color(0.25f, 0.32f, 0.30f));
            roomFloors[RoomHint.Bedroom2] = CreateMat("Floor_Bedroom2", new Color(0.28f, 0.30f, 0.34f));
            roomFloors[RoomHint.Kitchen] = CreateMat("Floor_Kitchen", new Color(0.30f, 0.30f, 0.26f));
            roomFloors[RoomHint.Entrance] = CreateMat("Floor_Entrance", new Color(0.22f, 0.24f, 0.28f));
        }

        private Material CreateMat(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Hidden/InternalErrorShader");
            var mat = new Material(shader) { name = name };
            SetMatColor(mat, color);
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

        private void AddFloor(MeshAccum accum, Vector2Int cell)
        {
            Vector3 c = layout.CellCenter(cell, 0f);
            float h = HomeLayout.CellSize * 0.5f;
            accum.AddQuad(
                new Vector3(c.x - h, 0f, c.z - h),
                new Vector3(c.x + h, 0f, c.z - h),
                new Vector3(c.x + h, 0f, c.z + h),
                new Vector3(c.x - h, 0f, c.z + h),
                Vector3.up);
        }

        private void AddCeiling(MeshAccum accum, Vector2Int cell)
        {
            Vector3 c = layout.CellCenter(cell, HomeLayout.WallHeight);
            float h = HomeLayout.CellSize * 0.5f;
            accum.AddQuad(
                new Vector3(c.x - h, HomeLayout.WallHeight, c.z + h),
                new Vector3(c.x + h, HomeLayout.WallHeight, c.z + h),
                new Vector3(c.x + h, HomeLayout.WallHeight, c.z - h),
                new Vector3(c.x - h, HomeLayout.WallHeight, c.z - h),
                Vector3.down);
        }

        private void AddWall(MeshAccum accum, Vector2Int cell, int dir, float y0, float y1)
        {
            Vector3 c = layout.CellCenter(cell, 0f);
            float h = HomeLayout.CellSize * 0.5f;
            Vector3 a, b;
            switch (dir)
            {
                case 0: a = new Vector3(c.x - h, 0f, c.z + h); b = new Vector3(c.x + h, 0f, c.z + h); break;
                case 1: a = new Vector3(c.x + h, 0f, c.z + h); b = new Vector3(c.x + h, 0f, c.z - h); break;
                case 2: a = new Vector3(c.x + h, 0f, c.z - h); b = new Vector3(c.x - h, 0f, c.z - h); break;
                default: a = new Vector3(c.x - h, 0f, c.z - h); b = new Vector3(c.x - h, 0f, c.z + h); break;
            }

            Vector3 n = Vector3.Cross(Vector3.up, b - a).normalized;
            accum.AddQuad(
                new Vector3(a.x, y0, a.z),
                new Vector3(b.x, y0, b.z),
                new Vector3(b.x, y1, b.z),
                new Vector3(a.x, y1, a.z),
                n);
        }

        private void AddWindow(MeshAccum glass, MeshAccum walls, Vector2Int cell, int dir)
        {
            AddWall(walls, cell, dir, 0f, 0.9f);
            AddWall(walls, cell, dir, 2.0f, HomeLayout.WallHeight);
            AddWall(glass, cell, dir, 0.9f, 2.0f);
        }

        private void AddDoorFrame(MeshAccum doors, Vector2Int cell, int dir)
        {
            AddWall(doors, cell, dir, 2.0f, HomeLayout.WallHeight);
        }

        private sealed class MeshAccum
        {
            private readonly List<Vector3> verts = new List<Vector3>();
            private readonly List<Vector3> norms = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> tris = new List<int>();

            public void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
            {
                int start = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                tris.Add(start + 0); tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start + 0); tris.Add(start + 2); tris.Add(start + 3);
            }

            public Mesh ToMesh()
            {
                if (verts.Count == 0) return null;
                var mesh = new Mesh { name = "Procedural" };
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
