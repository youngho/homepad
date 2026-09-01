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

        [SerializeField] private GameObject livingRoomPrefab;
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
        private RoomLightRig livingRig;

        public IReadOnlyDictionary<string, HomeItemView> Views => views;

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
            Color c = valid ? new Color(0.35f, 0.75f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f, 0.35f);
            SetMatColor(ghostMat, c);

            if (def.Surface == Surface.Ceiling)
            {
                t.position = layout.CellCenter(cell, HomeLayout.WallHeight - 0.25f);
                t.localScale = new Vector3(0.55f, 0.2f, 0.55f);
                t.rotation = Quaternion.identity;
            }
            else if (def.Surface == Surface.Floor)
            {
                t.position = layout.CellCenter(cell, 0.45f);
                t.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                t.rotation = Quaternion.identity;
            }
            else
            {
                t.position = layout.WallCenter(cell, wallDir, 1.2f);
                t.rotation = Quaternion.LookRotation(new Vector3(HomeLayout.DirVec[wallDir].x, 0f, HomeLayout.DirVec[wallDir].y));
                t.localScale = def.Kind == HomeItemKind.ElectricCurtain
                    ? new Vector3(HomeLayout.CellSize * 0.85f, 1.4f, 0.08f)
                    : new Vector3(0.35f, 0.5f, 0.12f);
            }
        }

        public void HideGhost()
        {
            if (ghostRoot != null) ghostRoot.gameObject.SetActive(false);
        }

        private void BuildGround()
        {
            float size = 48f;
            var mesh = CreateQuadMesh(
                new Vector3(-size, -0.02f, -size),
                new Vector3(size, -0.02f, -size),
                new Vector3(size, -0.02f, size),
                new Vector3(-size, -0.02f, size),
                Vector3.up);
            CreateMeshObject("Ground", geometryRoot, mesh, groundMat, false);
        }

        private void BuildRooms()
        {
            livingRig = null;
            var living = layout.FindRoom(RoomHint.Living);
            bool kitLiving = living != null && livingRoomPrefab != null;
            if (kitLiving)
            {
                var roomGo = Object.Instantiate(livingRoomPrefab, geometryRoot);
                roomGo.name = "LivingRoom";
                roomGo.transform.position = new Vector3(
                    living.Origin.x * HomeLayout.CellSize,
                    0f,
                    living.Origin.y * HomeLayout.CellSize);
                livingRig = roomGo.GetComponent<RoomLightRig>();
                livingRig?.SetLit(false);
            }

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
                if (kitLiving && room != null && room.Hint == RoomHint.Living) continue;
                var hint = room != null ? room.Hint : RoomHint.Living;
                if (!floors.TryGetValue(hint, out var floorAccum))
                {
                    floorAccum = new MeshAccum();
                    floors[hint] = floorAccum;
                }

                AddFloor(floorAccum, pair.Key);
                if (!layout.Cutaway)
                {
                    AddCeiling(ceilings, pair.Key);
                }

                for (int d = 0; d < 4; d++)
                {
                    if (cell.Windows[d])
                    {
                        AddWindow(glass, walls, pair.Key, d);
                        continue;
                    }

                    if (layout.Cutaway && IsNearWall(d)) continue;
                    if (cell.Doors[d])
                    {
                        AddDoorFrame(doors, pair.Key, d);
                    }
                    else if (cell.Walls[d])
                    {
                        AddWall(walls, pair.Key, d, 0f, HomeLayout.WallHeight);
                    }
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
            if (!layout.Cutaway)
            {
                CreateMeshObject("Ceilings", geometryRoot, ceilings.ToMesh(), ceilingMat, false);
            }
        }

        private void CreateItemVisual(PlacedItem item)
        {
            var go = new GameObject($"Item_{item.DisplayName}_{item.InstanceId}");
            go.transform.SetParent(itemRoot, false);
            var view = go.AddComponent<HomeItemView>();
            Transform visual;
            Transform curtainLeaf = null;

            switch (item.Kind)
            {
                case HomeItemKind.Light:
                    visual = SpawnKitOrPrimitive(go.transform, ceilingLampPrefab, PrimitiveType.Sphere, new Vector3(0.28f, 0.18f, 0.28f));
                    go.transform.position = layout.CellCenter(item.Cell, HomeLayout.WallHeight - 0.35f);
                    break;
                case HomeItemKind.Vent:
                    visual = CreatePrimitive(go.transform, PrimitiveType.Cylinder, new Vector3(0.45f, 0.04f, 0.45f));
                    go.transform.position = layout.CellCenter(item.Cell, HomeLayout.WallHeight - 0.12f);
                    break;
                case HomeItemKind.Elevator:
                    visual = CreatePrimitive(go.transform, PrimitiveType.Cube, new Vector3(0.7f, 1.6f, 0.7f));
                    go.transform.position = layout.CellCenter(item.Cell, 0.8f);
                    break;
                case HomeItemKind.ElectricCurtain:
                    go.transform.position = layout.WallCenter(item.Cell, item.WallDir, 1.15f);
                    go.transform.rotation = Quaternion.LookRotation(
                        new Vector3(HomeLayout.DirVec[item.WallDir].x, 0f, HomeLayout.DirVec[item.WallDir].y));
                    visual = SpawnKitOrPrimitive(go.transform, curtainPrefab, PrimitiveType.Cube, new Vector3(HomeLayout.CellSize * 0.9f, 1.5f, 0.06f));
                    curtainLeaf = visual;
                    break;
                case HomeItemKind.Heating:
                    go.transform.position = layout.WallCenter(item.Cell, item.WallDir, 1.05f);
                    go.transform.rotation = Quaternion.LookRotation(
                        new Vector3(HomeLayout.DirVec[item.WallDir].x, 0f, HomeLayout.DirVec[item.WallDir].y));
                    visual = SpawnKitOrPrimitive(go.transform, wallHeaterPrefab, PrimitiveType.Cube, new Vector3(0.32f, 0.48f, 0.1f));
                    break;
                default:
                    go.transform.position = layout.WallCenter(item.Cell, item.WallDir, 1.15f);
                    go.transform.rotation = Quaternion.LookRotation(
                        new Vector3(HomeLayout.DirVec[item.WallDir].x, 0f, HomeLayout.DirVec[item.WallDir].y));
                    visual = CreatePrimitive(go.transform, PrimitiveType.Cube, new Vector3(0.32f, 0.48f, 0.1f));
                    break;
            }

            var box = go.AddComponent<BoxCollider>();
            box.size = item.Kind == HomeItemKind.ElectricCurtain
                ? new Vector3(HomeLayout.CellSize * 0.9f, 1.5f, 0.2f)
                : item.Kind == HomeItemKind.Elevator
                    ? new Vector3(0.75f, 1.6f, 0.75f)
                    : item.Surface == Surface.Ceiling
                        ? new Vector3(0.6f, 0.35f, 0.6f)
                        : new Vector3(0.4f, 0.55f, 0.25f);

            view.Bind(item, visual, curtainLeaf);
            views[item.InstanceId] = view;
        }

        private void ApplyItemState(HomeItemView view, WallpadManager manager)
        {
            if (view == null || view.Item == null) return;
            if (ApplyKitState(view, manager)) return;
            if (view.Visual == null) return;
            var renderer = view.Visual.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            Color color = new Color(0.55f, 0.58f, 0.62f);
            var item = view.Item;

            switch (item.Kind)
            {
                case HomeItemKind.Light:
                    var light = FindLight(manager, item.DeviceId);
                    color = light != null && light.isOn ? new Color(1f, 0.92f, 0.45f) : new Color(0.55f, 0.55f, 0.5f);
                    break;
                case HomeItemKind.Heating:
                    var room = FindHeat(manager, item.DeviceId);
                    color = room != null && room.isPowered
                        ? (room.isAwayMode ? new Color(0.45f, 0.65f, 0.95f) : new Color(0.95f, 0.45f, 0.28f))
                        : new Color(0.4f, 0.4f, 0.45f);
                    break;
                case HomeItemKind.Gas:
                    bool open = manager != null && manager.Gas.isOpen;
                    color = open ? new Color(0.9f, 0.3f, 0.28f) : new Color(0.3f, 0.75f, 0.4f);
                    break;
                case HomeItemKind.Vent:
                    color = manager != null && manager.Ventilation.isPowered
                        ? new Color(0.4f, 0.75f, 0.95f)
                        : new Color(0.45f, 0.48f, 0.52f);
                    break;
                case HomeItemKind.Elevator:
                    color = manager != null && manager.Elevator.isCalled
                        ? new Color(0.35f, 0.55f, 0.95f)
                        : new Color(0.5f, 0.52f, 0.58f);
                    break;
                case HomeItemKind.ElectricCurtain:
                    color = new Color(0.55f, 0.38f, 0.55f);
                    if (view.CurtainLeaf != null)
                    {
                        float openAmt = item.CurtainOpen;
                        view.CurtainLeaf.localScale = new Vector3(
                            Mathf.Lerp(HomeLayout.CellSize * 0.9f, HomeLayout.CellSize * 0.12f, openAmt),
                            1.5f,
                            0.06f);
                        view.CurtainLeaf.localPosition = new Vector3(
                            Mathf.Lerp(0f, -HomeLayout.CellSize * 0.38f, openAmt),
                            0f,
                            0f);
                    }

                    break;
            }

            renderer.material.color = color;
            SetMatColor(renderer.material, color);
        }

        private bool ApplyKitState(HomeItemView view, WallpadManager manager)
        {
            var item = view.Item;
            if (item.Kind == HomeItemKind.Light)
            {
                var bulb = view.GetComponentInChildren<Light>(true);
                if (bulb == null && livingRig == null) return false;
                var state = FindLight(manager, item.DeviceId);
                bool on = state != null && state.isOn;
                if (bulb != null)
                {
                    bulb.enabled = true;
                    bulb.intensity = on ? 7.5f : 0.15f;
                    bulb.color = on ? new Color(1f, 0.93f, 0.78f) : new Color(0.35f, 0.42f, 0.55f);
                }

                livingRig?.SetLit(on);
                var shade = view.transform.Find("CeilingLamp/Shade") ?? view.transform.Find("Shade");
                var shadeRenderer = shade != null ? shade.GetComponent<MeshRenderer>() : null;
                if (shadeRenderer != null)
                {
                    var mat = shadeRenderer.material;
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", on ? new Color(2.2f, 1.9f, 1.2f) : new Color(0.05f, 0.05f, 0.06f));
                    }
                }

                return bulb != null || livingRig != null;
            }

            if (item.Kind == HomeItemKind.Heating)
            {
                var glow = view.GetComponentInChildren<HeaterGlow>(true);
                if (glow == null) return false;
                var room = FindHeat(manager, item.DeviceId);
                glow.SetOn(room != null && room.isPowered && !room.isAwayMode);
                return true;
            }

            if (item.Kind == HomeItemKind.ElectricCurtain)
            {
                var cloth = view.GetComponentInChildren<CurtainCloth>(true);
                if (cloth == null) return false;
                cloth.SetOpen(item.CurtainOpen);
                return true;
            }

            return false;
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

        private static HeatingState FindHeat(WallpadManager manager, int id)
        {
            if (manager == null) return null;
            for (int i = 0; i < manager.HeatingRooms.Count; i++)
            {
                if (manager.HeatingRooms[i].roomId == id) return manager.HeatingRooms[i];
            }

            return null;
        }

        private Transform CreatePrimitive(Transform parent, PrimitiveType type, Vector3 scale)
        {
            var prim = GameObject.CreatePrimitive(type);
            prim.transform.SetParent(parent, false);
            prim.transform.localPosition = Vector3.zero;
            prim.transform.localRotation = Quaternion.identity;
            prim.transform.localScale = scale;
            var col = prim.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var renderer = prim.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.material = new Material(UnlitShader());
            SetMatColor(renderer.material, Color.gray);
            materials.Add(renderer.material);
            return prim.transform;
        }

        private static bool IsNearWall(int dir)
        {
            return dir == (int)WallDir.South || dir == (int)WallDir.West;
        }

        private void AddFloor(MeshAccum accum, Vector2Int cell)
        {
            float s = HomeLayout.CellSize;
            float x = cell.x * s;
            float z = cell.y * s;
            accum.AddQuad(
                new Vector3(x, 0f, z),
                new Vector3(x + s, 0f, z),
                new Vector3(x + s, 0f, z + s),
                new Vector3(x, 0f, z + s),
                Vector3.up);
            accum.AddQuad(
                new Vector3(x, 0f, z),
                new Vector3(x, 0f, z + s),
                new Vector3(x + s, 0f, z + s),
                new Vector3(x + s, 0f, z),
                Vector3.down);
        }

        private void AddCeiling(MeshAccum accum, Vector2Int cell)
        {
            float s = HomeLayout.CellSize;
            float h = HomeLayout.WallHeight;
            float x = cell.x * s;
            float z = cell.y * s;
            accum.AddQuad(
                new Vector3(x, h, z + s),
                new Vector3(x + s, h, z + s),
                new Vector3(x + s, h, z),
                new Vector3(x, h, z),
                Vector3.down);
        }

        private void AddWall(MeshAccum accum, Vector2Int cell, int dir, float y0, float y1)
        {
            GetWallCorners(cell, dir, y0, y1, out var a, out var b, out var c, out var d, out var n);
            accum.AddQuad(a, b, c, d, n);
            accum.AddQuad(b, a, d, c, -n);
        }

        private void AddDoorFrame(MeshAccum accum, Vector2Int cell, int dir)
        {
            float doorWidth = HomeLayout.CellSize * 0.36f;
            float side = (HomeLayout.CellSize - doorWidth) * 0.5f;
            AddWallSegment(accum, cell, dir, 0f, side, 0f, HomeLayout.WallHeight);
            AddWallSegment(accum, cell, dir, HomeLayout.CellSize - side, HomeLayout.CellSize, 0f, HomeLayout.WallHeight);
            AddWallSegment(accum, cell, dir, side, HomeLayout.CellSize - side, 2.05f, HomeLayout.WallHeight);
        }

        private void AddWindow(MeshAccum glass, MeshAccum frames, Vector2Int cell, int dir)
        {
            float sill = 0.45f;
            float header = 2.05f;
            AddWall(frames, cell, dir, 0f, sill);
            AddWall(frames, cell, dir, header, HomeLayout.WallHeight);
            GetWallCorners(cell, dir, sill, header, out var a, out var b, out var c, out var d, out var n);
            Vector3 inset = n * 0.02f;
            glass.AddQuad(a + inset, b + inset, c + inset, d + inset, n);
            glass.AddQuad(b - inset, a - inset, d - inset, c - inset, -n);
        }

        private void AddWallSegment(MeshAccum accum, Vector2Int cell, int dir, float t0, float t1, float y0, float y1)
        {
            GetWallCorners(cell, dir, y0, y1, out var a, out var b, out var c, out var d, out var n);
            Vector3 along = b - a;
            Vector3 a0 = a + along * (t0 / HomeLayout.CellSize);
            Vector3 b0 = a + along * (t1 / HomeLayout.CellSize);
            Vector3 d0 = d + (c - d) * (t0 / HomeLayout.CellSize);
            Vector3 c0 = d + (c - d) * (t1 / HomeLayout.CellSize);
            accum.AddQuad(a0, b0, c0, d0, n);
            accum.AddQuad(b0, a0, d0, c0, -n);
        }

        private static void GetWallCorners(Vector2Int cell, int dir, float y0, float y1,
            out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d, out Vector3 n)
        {
            float s = HomeLayout.CellSize;
            float x = cell.x * s;
            float z = cell.y * s;
            switch (dir)
            {
                case (int)WallDir.North:
                    a = new Vector3(x, y0, z + s);
                    b = new Vector3(x + s, y0, z + s);
                    c = new Vector3(x + s, y1, z + s);
                    d = new Vector3(x, y1, z + s);
                    n = Vector3.forward;
                    break;
                case (int)WallDir.East:
                    a = new Vector3(x + s, y0, z);
                    b = new Vector3(x + s, y0, z + s);
                    c = new Vector3(x + s, y1, z + s);
                    d = new Vector3(x + s, y1, z);
                    n = Vector3.right;
                    break;
                case (int)WallDir.South:
                    a = new Vector3(x + s, y0, z);
                    b = new Vector3(x, y0, z);
                    c = new Vector3(x, y1, z);
                    d = new Vector3(x + s, y1, z);
                    n = Vector3.back;
                    break;
                default:
                    a = new Vector3(x, y0, z + s);
                    b = new Vector3(x, y0, z);
                    c = new Vector3(x, y1, z);
                    d = new Vector3(x, y1, z + s);
                    n = Vector3.left;
                    break;
            }
        }

        private void EnsureMaterials()
        {
            if (floorMat != null) return;
            var shader = UnlitShader();
            groundMat = Make(shader, new Color(0.10f, 0.13f, 0.16f), false);
            wallMat = Make(shader, new Color(0.82f, 0.80f, 0.74f), false);
            doorMat = Make(shader, new Color(0.62f, 0.48f, 0.34f), false);
            ceilingMat = Make(shader, new Color(0.90f, 0.90f, 0.88f), false);
            glassMat = Make(shader, new Color(0.45f, 0.72f, 0.88f, 0.35f), true);
            ghostMat = Make(shader, new Color(0.4f, 0.7f, 1f, 0.4f), true);
            floorMat = Make(shader, new Color(0.55f, 0.50f, 0.42f), false);
            roomFloors[RoomHint.Living] = Make(shader, new Color(0.62f, 0.56f, 0.46f), false);
            roomFloors[RoomHint.Master] = Make(shader, new Color(0.48f, 0.52f, 0.62f), false);
            roomFloors[RoomHint.Bedroom] = Make(shader, new Color(0.58f, 0.46f, 0.52f), false);
            roomFloors[RoomHint.Bedroom2] = Make(shader, new Color(0.46f, 0.56f, 0.50f), false);
            roomFloors[RoomHint.Kitchen] = Make(shader, new Color(0.60f, 0.54f, 0.42f), false);
            roomFloors[RoomHint.Entrance] = Make(shader, new Color(0.42f, 0.43f, 0.46f), false);
        }

        private Material RoomFloor(RoomHint hint)
        {
            return roomFloors.TryGetValue(hint, out var mat) ? mat : floorMat;
        }

        private Material Make(Shader shader, Color color, bool transparent)
        {
            var mat = new Material(shader);
            SetMatColor(mat, color);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            materials.Add(mat);
            return mat;
        }

        private static void SetMatColor(Material mat, Color color)
        {
            if (mat == null) return;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        private static Shader UnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                   ?? Shader.Find("Sprites/Default")
                   ?? Shader.Find("Unlit/Color")
                   ?? Shader.Find("Standard");
        }

        private static Mesh CreateQuadMesh(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            var accum = new MeshAccum();
            accum.AddQuad(a, b, c, d, n);
            return accum.ToMesh();
        }

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material mat, bool collider)
        {
            if (mesh == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (collider)
            {
                go.AddComponent<MeshCollider>();
            }

            return go;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(root.GetChild(i).gameObject);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] != null) Object.Destroy(materials[i]);
            }
        }

        private sealed class MeshAccum
        {
            private readonly List<Vector3> verts = new List<Vector3>();
            private readonly List<int> tris = new List<int>();
            private readonly List<Vector3> norms = new List<Vector3>();

            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                int i = verts.Count;
                verts.Add(a);
                verts.Add(b);
                verts.Add(c);
                verts.Add(d);
                norms.Add(n);
                norms.Add(n);
                norms.Add(n);
                norms.Add(n);
                tris.Add(i);
                tris.Add(i + 2);
                tris.Add(i + 1);
                tris.Add(i);
                tris.Add(i + 3);
                tris.Add(i + 2);
            }

            public Mesh ToMesh()
            {
                if (verts.Count == 0) return null;
                var mesh = new Mesh { name = "HomeMesh" };
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.SetNormals(norms);
                return mesh;
            }
        }
    }
}
