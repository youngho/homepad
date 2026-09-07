using System;
using System.Collections.Generic;
using Homepad.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Homepad.Home
{
    [DefaultExecutionOrder(-50)]
    public class HomeController : MonoBehaviour
    {
        public const string SaveKey = "Homepad.MyHome.v4";

        public static HomeController Instance { get; private set; }

        public event Action LayoutChanged;
        public event Action<PlacedItem> ItemClicked;
        public event Action<RoomRecord> RoomSelected;
        public event Action OverlayDismissed;

        public RoomRecord SelectedRoom { get; private set; }

        private HomeLayout layout;
        private HomeLayoutService service;
        private IsometricHomeBuilder builder;
        private HomeItemDef pendingDef;
        private bool createdRoomForPlace;
        private HomeItemView dragView;
        private Vector2 pointerDownPos;
        private bool dragging;
        private bool pointerHeldOnItem;
        private const float DragThreshold = 14f;

        public HomeLayout Layout => layout;
        public HomeLayoutService Service => service;
        public bool IsPlacing => pendingDef != null;
        public HomeItemDef PendingDef => pendingDef;

        public static HomeController EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<HomeController>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            // Scene teardown / play-mode exit must not spawn a leftover MyHome.
            if (!Application.isPlaying) return null;
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return null;
#endif

            var go = new GameObject("MyHome");
            return go.AddComponent<HomeController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            layout = new HomeLayout();
            service = new HomeLayoutService(layout);
            builder = gameObject.GetComponent<IsometricHomeBuilder>() ?? gameObject.AddComponent<IsometricHomeBuilder>();
            builder.Initialize(layout);
            ConfigureCamera();
            LoadOrEmpty();
            builder.Rebuild();
            FrameCamera();
        }

        private void Start()
        {
            service?.EnsureBoundDevices();
            builder?.RefreshItemStates();
        }

        private void OnEnable()
        {
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnStateChanged += OnManagerState;
            }
        }

        private void OnDisable()
        {
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnStateChanged -= OnManagerState;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
                OverlayDismissed?.Invoke();
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && !PointerInput.OverUi(mouse.position.ReadValue()))
            {
                CancelPlacement();
                OverlayDismissed?.Invoke();
            }

            if (!PointerInput.TryPrimary(out Vector2 pointerPos, out bool down, out bool held, out bool up))
            {
                return;
            }

            bool overUi = PointerInput.OverUi(pointerPos);

            if (pendingDef != null && !overUi)
            {
                UpdateGhost(pointerPos);
                if (down)
                {
                    ConfirmPlacement(pointerPos);
                }

                return;
            }

            if (overUi)
            {
                if (up)
                {
                    pointerHeldOnItem = false;
                    dragging = false;
                    dragView = null;
                }

                return;
            }

            if (down)
            {
                pointerDownPos = pointerPos;
                dragView = RaycastItem(pointerPos);
                pointerHeldOnItem = dragView != null;
                dragging = false;
                if (dragView == null)
                {
                    OverlayDismissed?.Invoke();
                    var hitRoom = RaycastRoom(pointerPos);
                    if (hitRoom != null)
                    {
                        SelectRoom(hitRoom);
                    }
                }
            }

            if (pointerHeldOnItem && dragView != null && held)
            {
                if (!dragging && (pointerPos - pointerDownPos).sqrMagnitude > DragThreshold * DragThreshold)
                {
                    dragging = true;
                    OverlayDismissed?.Invoke();
                }

                if (dragging)
                {
                    DragItem(dragView, pointerPos);
                }
            }

            if (up)
            {
                if (dragging && dragView != null)
                {
                    FinishDrag(dragView, pointerPos);
                }
                else if (pointerHeldOnItem && dragView != null)
                {
                    ItemClicked?.Invoke(dragView.Item);
                }

                pointerHeldOnItem = false;
                dragging = false;
                dragView = null;
            }
        }

        public void SelectRoom(RoomRecord room)
        {
            SelectedRoom = room;
            if (room != null)
            {
                RoomSelected?.Invoke(room);
            }
        }

        public RoomRecord CreateRoom(RoomHint hint, string customName = null)
        {
            if (service == null) return null;
            var room = service.CreateRoom(hint, customName);
            if (room != null)
            {
                if (builder != null) builder.Rebuild();
                Save();
                FrameCamera();
                SelectRoom(room);
                NotifyLayoutChanged();
            }
            return room;
        }

        public bool RenameRoom(int roomId, string newName)
        {
            if (service == null) return false;
            bool success = service.RenameRoom(roomId, newName);
            if (success)
            {
                Save();
                NotifyLayoutChanged();
            }
            return success;
        }

        public bool DeleteRoom(int roomId)
        {
            if (service == null) return false;
            bool success = service.DeleteRoom(roomId);
            if (success)
            {
                if (SelectedRoom != null && SelectedRoom.Id == roomId)
                {
                    SelectedRoom = layout.Rooms.Count > 0 ? layout.Rooms[0] : null;
                }
                if (builder != null) builder.Rebuild();
                Save();
                FrameCamera();
                NotifyLayoutChanged();
            }
            return success;
        }

        public bool RemoveItem(string instanceId)
        {
            if (service == null) return false;
            bool success = service.RemoveItem(instanceId);
            if (success)
            {
                if (builder != null) builder.Rebuild();
                Save();
                NotifyLayoutChanged();
            }
            return success;
        }

        public RoomRecord RaycastRoom(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null || layout == null) return null;
            var ray = cam.ScreenPointToRay(screenPos);
            var hits = Physics.RaycastAll(ray, 100f);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                var cell = layout.WorldToCell(hit.point);
                var room = layout.RoomAt(cell);
                if (room != null) return room;
            }
            return null;
        }

        public void BeginPlacement(HomeItemDef def)
        {
            PlaceFromCatalog(def);
        }

        public bool PlaceFromCatalog(HomeItemDef def)
        {
            return PlaceFromCatalog(def, null);
        }

        public bool PlaceFromCatalog(HomeItemDef def, RoomRecord room)
        {
            if (def == null || layout == null || service == null) return false;
            if (room == null) room = layout.FindRoom(def.RoomHint);
            if (room == null) return false;
            if (layout.IsCatalogBlocked(def)) return false;
            CancelPlacement();

            var cell = service.DefaultCell(def, room);
            int wallDir = service.DefaultWallDir(def, room, cell);
            var item = service.PlaceIntoRoom(def, room, cell, wallDir);
            if (item == null) return false;

            builder.Rebuild();
            FrameCamera();
            Save();
            LayoutChanged?.Invoke();
            return true;
        }

        public void CancelPlacement()
        {
            if (pendingDef != null && createdRoomForPlace)
            {
                service.RemoveRoomIfEmpty(pendingDef.RoomHint);
            }

            pendingDef = null;
            createdRoomForPlace = false;
            builder.HideGhost();
            builder.Rebuild();
            FrameCamera();
            LayoutChanged?.Invoke();
        }

        public void SetCutaway(bool cutaway)
        {
            layout.Cutaway = cutaway;
            builder.Rebuild();
            Save();
            LayoutChanged?.Invoke();
        }

        public void SetCurtainOpen(string instanceId, float open)
        {
            service.SetCurtainOpen(instanceId, open);
            builder.RefreshItemStates();
            Save();
        }

        public void NotifyLayoutChanged()
        {
            LayoutChanged?.Invoke();
        }

        private void ConfirmPlacement(Vector2 mousePos)
        {
            if (!TryWorldPoint(mousePos, out var world)) return;
            var def = pendingDef;
            if (!service.TrySnapPlacement(def, world, out var cell, out var wallDir))
            {
                var room = service.EnsureRoom(def.RoomHint);
                cell = service.DefaultCell(def, room);
                wallDir = service.DefaultWallDir(def, room, cell);
            }

            var item = service.Place(def, cell, wallDir);
            pendingDef = null;
            createdRoomForPlace = false;
            builder.HideGhost();
            builder.Rebuild();
            FrameCamera();
            Save();
            LayoutChanged?.Invoke();
            if (item != null) ItemClicked?.Invoke(item);
        }

        private void UpdateGhost(Vector2 mousePos)
        {
            if (!TryWorldPoint(mousePos, out var world))
            {
                builder.HideGhost();
                return;
            }

            bool valid = service.TrySnapPlacement(pendingDef, world, out var cell, out var wallDir);
            if (!valid)
            {
                var room = layout.FindRoom(pendingDef.RoomHint);
                if (room != null)
                {
                    cell = service.DefaultCell(pendingDef, room);
                    wallDir = service.DefaultWallDir(pendingDef, room, cell);
                    valid = true;
                }
            }

            if (valid) builder.ShowGhost(pendingDef, cell, wallDir, true);
            else builder.HideGhost();
        }

        private void DragItem(HomeItemView view, Vector2 mousePos)
        {
            if (view == null || view.Item == null) return;
            if (!TryWorldPoint(mousePos, out var world)) return;
            var def = HomeItemDef.Find(view.Item.CatalogId);
            if (def == null) return;
            var room = layout.FindRoom(view.Item.RoomHint);
            if (room == null || !service.TrySnapPlacement(def, world, out var cell, out var wallDir)) return;
            var at = layout.RoomAt(cell);
            if (at == null || at.Hint != view.Item.RoomHint)
            {
                cell = view.Item.Cell;
                wallDir = view.Item.WallDir;
            }

            if (def.Surface == Surface.Ceiling)
            {
                view.transform.position = layout.CellCenter(cell, HomeLayout.WallHeight - 0.35f);
            }
            else if (def.Surface == Surface.Floor)
            {
                view.transform.position = layout.CellCenter(cell, 0.8f);
            }
            else
            {
                view.transform.position = layout.WallCenter(cell, wallDir, 1.2f);
                view.transform.rotation = Quaternion.LookRotation(
                    new Vector3(HomeLayout.DirVec[wallDir].x, 0f, HomeLayout.DirVec[wallDir].y));
            }
        }

        private void FinishDrag(HomeItemView view, Vector2 mousePos)
        {
            if (view == null || view.Item == null) return;
            if (!TryWorldPoint(mousePos, out var world))
            {
                builder.RebuildItems();
                return;
            }

            var def = HomeItemDef.Find(view.Item.CatalogId);
            if (def == null || !service.TrySnapPlacement(def, world, out var cell, out var wallDir))
            {
                builder.RebuildItems();
                return;
            }

            var room = layout.RoomAt(cell);
            if (room == null || room.Hint != view.Item.RoomHint)
            {
                builder.RebuildItems();
                return;
            }

            service.MoveItem(view.Item.InstanceId, cell, wallDir);
            builder.Rebuild();
            Save();
            LayoutChanged?.Invoke();
        }

        private HomeItemView RaycastItem(Vector2 mousePos)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var ray = cam.ScreenPointToRay(mousePos);
            var hits = Physics.RaycastAll(ray, 200f);
            HomeItemView best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var view = hits[i].collider.GetComponentInParent<HomeItemView>();
                if (view == null || hits[i].distance >= bestDist) continue;
                best = view;
                bestDist = hits[i].distance;
            }

            return best;
        }

        private static bool TryWorldPoint(Vector2 mousePos, out Vector3 world)
        {
            world = Vector3.zero;
            var cam = Camera.main;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(mousePos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float dist)) return false;
            world = ray.GetPoint(dist);
            return true;
        }

        private void OnManagerState()
        {
            builder.RefreshItemStates();
        }

        public static void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
            cam.transform.rotation = Quaternion.Euler(35.264f, 45f, 0f);
            cam.transform.position = new Vector3(-13.5f, 14.5f, -13.5f);
        }

        public void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var rot = Quaternion.Euler(35.264f, 45f, 0f);
            TryCameraFrame(rot, out Vector3 pivot, out float size);

            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.transform.rotation = rot;
            cam.transform.position = pivot + rot * new Vector3(0f, 0f, -24f);
        }

        private bool TryCameraFrame(Quaternion rot, out Vector3 pivot, out float size)
        {
            pivot = new Vector3(0f, 0.5f, 0f);
            size = 7.5f;
            if (layout == null) return false;

            var points = new List<Vector3>(16);
            CollectFramePoints(points);
            if (points.Count == 0) return false;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < points.Count; i++) sum += points[i];
            pivot = sum / points.Count;
            pivot.y = 0.5f;

            Quaternion inv = Quaternion.Inverse(rot);
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 local = inv * (points[i] - pivot);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
            }

            Vector3 mid = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            pivot += rot * mid;

            float halfW = (maxX - minX) * 0.5f;
            float halfH = (maxY - minY) * 0.5f;
            float aspect = Screen.height > 0
                ? (float)Screen.width / Screen.height
                : Mathf.Max(0.15f, Camera.main != null ? Camera.main.aspect : 1.777f);
            float fit = Mathf.Max(halfH, halfW / Mathf.Max(0.15f, aspect));

            // 장치는 화면 안에 두고, 바닥 모서리는 잘려도 방을 크게 본다.
            size = Mathf.Clamp(fit * 1.2f, 5.2f, 11.0f);
            return true;
        }

        private void CollectFramePoints(List<Vector3> points)
        {
            if (layout.Items != null)
            {
                for (int i = 0; i < layout.Items.Count; i++)
                {
                    var item = layout.Items[i];
                    if (item == null) continue;
                    points.Add(ItemFramePoint(item));
                }
            }

            if (points.Count > 0) return;
            if (layout.Rooms == null) return;
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                if (room == null) continue;
                points.Add(layout.RoomCenter(room, 0.9f));
            }
        }

        private Vector3 ItemFramePoint(PlacedItem item)
        {
            switch (item.Kind)
            {
                case HomeItemKind.Light:
                {
                    var room = layout.FindRoom(item.RoomHint);
                    return room != null
                        ? layout.RoomCenter(room, HomeDioramaBuilder.HighWallHeight - 0.18f)
                        : layout.CellCenter(item.Cell, HomeDioramaBuilder.HighWallHeight - 0.18f);
                }
                case HomeItemKind.Heating:
                    return layout.WallCenter(item.Cell, item.WallDir, 0.52f);
                case HomeItemKind.Vent:
                    return layout.WallCenter(item.Cell, item.WallDir, 1.18f);
                case HomeItemKind.Gas:
                    return layout.WallCenter(item.Cell, item.WallDir, 1.12f);
                case HomeItemKind.ElectricCurtain:
                    return layout.WallCenter(item.Cell, item.WallDir, 1.05f);
                default:
                    return layout.CellCenter(item.Cell, 1.0f);
            }
        }

        private void LoadOrEmpty()
        {
            if (PlayerPrefs.HasKey(SaveKey))
            {
                string json = PlayerPrefs.GetString(SaveKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var data = JsonUtility.FromJson<HomeSaveData>(json);
                        if (data != null && (data.rooms.Count > 0 || data.items.Count > 0))
                        {
                            ApplySave(data);
                            SwapLivingHeatAndVentIfNeeded();
                            EnsureLivingVent();
                            EnsureKitchen();
                            EnsureRoomHeaters();
                            return;
                        }
                    }
                    catch (Exception)
                    {
                        // ignore corrupt save
                    }
                }
            }

            SeedCheotmaeulDemo();
        }

        // 임시 테스트 세대. 나중에 집 설정 화면이 생기면 이 기본값만 빼면 된다.
        // 배치: 방1 / 거실 | 주방 | 방3 | 방2
        private void SeedCheotmaeulDemo()
        {
            int step = HomeLayout.RoomSize;
            var living = service.CreateRoom(RoomHint.Living, Vector2Int.zero, "거실");
            service.CreateRoom(RoomHint.Kitchen, new Vector2Int(step, 0), "주방");
            var room3 = service.CreateRoom(RoomHint.Bedroom2, new Vector2Int(step * 2, 0), "방3");
            var room2 = service.CreateRoom(RoomHint.Bedroom, new Vector2Int(step * 3, 0), "방2");
            var room1 = service.CreateRoom(RoomHint.Master, new Vector2Int(0, step), "방1");

            PlaceDemoLights(living);
            PlaceDemoLights(room1);
            PlaceDemoLights(room2);
            PlaceDemoLights(room3);
            EnsureLivingVent();
            EnsureKitchen();
            EnsureRoomHeaters();

            SelectRoom(living);
            Save();
        }

        private void EnsureKitchen()
        {
            bool added = false;
            var kitchen = layout.FindRoom(RoomHint.Kitchen);
            if (kitchen == null)
            {
                int step = HomeLayout.RoomSize;
                var origin = new Vector2Int(step, 0);
                kitchen = layout.RoomAt(origin) == null
                    ? service.CreateRoom(RoomHint.Kitchen, origin, "주방")
                    : service.CreateRoom(RoomHint.Kitchen, "주방");
                added = kitchen != null;
            }

            for (int i = layout.Items.Count - 1; i >= 0; i--)
            {
                var item = layout.Items[i];
                if (item.RoomHint != RoomHint.Kitchen) continue;
                if (item.Kind != HomeItemKind.Light && item.Kind != HomeItemKind.Heating) continue;
                if (service.RemoveItem(item.InstanceId)) added = true;
            }

            if (kitchen != null && !layout.HasSingleton(HomeItemKind.Gas, RoomHint.Kitchen))
            {
                var def = HomeItemDef.Create(HomeItemKind.Gas, kitchen.Hint, kitchen.Name);
                var cell = service.DefaultCell(def, kitchen);
                int wallDir = service.DefaultWallDir(def, kitchen, cell);
                if (service.PlaceIntoRoom(def, kitchen, cell, wallDir) != null)
                {
                    added = true;
                }
            }

            if (added) Save();
        }

        private void EnsureLivingVent()
        {
            if (layout.HasSingleton(HomeItemKind.Vent, RoomHint.Living)) return;
            var living = layout.FindRoom(RoomHint.Living);
            if (living == null) return;

            var def = HomeItemDef.Create(HomeItemKind.Vent, living.Hint, living.Name);
            int wallDir = service.DefaultWallDir(def, living, service.DefaultCell(def, living));
            wallDir = (wallDir + 1) & 3;
            var cell = layout.EdgeCell(living, wallDir);
            if (service.PlaceIntoRoom(def, living, cell, wallDir) != null)
            {
                Save();
            }
        }

        private void EnsureRoomHeaters()
        {
            bool added = false;
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                if (room == null || room.Hint == RoomHint.Kitchen || layout.HasHeatingInHint(room.Hint)) continue;

                var def = HomeItemDef.Create(HomeItemKind.Heating, room.Hint, room.Name);
                var cell = service.DefaultCell(def, room);
                int wallDir = service.DefaultWallDir(def, room, cell);

                if (service.PlaceIntoRoom(def, room, cell, wallDir) != null)
                {
                    added = true;
                }
            }

            if (added) Save();
        }

        private void SwapLivingHeatAndVentIfNeeded()
        {
            PlacedItem heat = null;
            PlacedItem vent = null;
            for (int i = 0; i < layout.Items.Count; i++)
            {
                var item = layout.Items[i];
                if (item.RoomHint != RoomHint.Living) continue;
                if (item.Kind == HomeItemKind.Heating) heat = item;
                else if (item.Kind == HomeItemKind.Vent) vent = item;
            }

            if (heat == null || vent == null) return;

            var cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : new Vector3(1f, -1f, 1f);
            int back = HomeLayout.CutawayView.FromCamera(forward).PrimaryBack;
            int side = (back + 1) & 3;
            if (heat.WallDir != side || vent.WallDir != back) return;

            var cell = heat.Cell;
            int dir = heat.WallDir;
            heat.Cell = vent.Cell;
            heat.WallDir = vent.WallDir;
            vent.Cell = cell;
            vent.WallDir = dir;
            Save();
        }

        private void PlaceDemoLights(RoomRecord room)
        {
            if (room == null) return;
            var def = HomeItemDef.Create(HomeItemKind.Light, room.Hint, room.Name);
            service.PlaceIntoRoom(def, room, room.Origin + new Vector2Int(1, 2), 0);
            service.PlaceIntoRoom(def, room, room.Origin + new Vector2Int(2, 1), 0);
        }

        private void ApplySave(HomeSaveData data)
        {
            layout.Cutaway = data.cutaway;
            if (data.rooms != null)
            {
                for (int i = 0; i < data.rooms.Count; i++)
                {
                    var r = data.rooms[i];
                    service.RestoreRoom(new RoomRecord
                    {
                        Id = r.id,
                        Hint = (RoomHint)r.hint,
                        Origin = new Vector2Int(r.ox, r.oy),
                        Size = new Vector2Int(Mathf.Max(1, r.sx), Mathf.Max(1, r.sy)),
                        Name = string.IsNullOrEmpty(r.name) ? HomeItemDef.RoomName((RoomHint)r.hint) : r.name
                    });
                }

                service.RebuildWalls();
            }

            if (data.items == null) return;
            for (int i = 0; i < data.items.Count; i++)
            {
                var s = data.items[i];
                service.Restore(new PlacedItem
                {
                    InstanceId = string.IsNullOrEmpty(s.instanceId) ? Guid.NewGuid().ToString("N").Substring(0, 8) : s.instanceId,
                    CatalogId = s.catalogId,
                    Kind = (HomeItemKind)s.kind,
                    RoomHint = (RoomHint)s.roomHint,
                    Surface = (Surface)s.surface,
                    DisplayName = s.displayName,
                    Cell = new Vector2Int(s.cx, s.cy),
                    WallDir = s.wallDir,
                    DeviceId = s.deviceId,
                    CurtainOpen = s.curtainOpen
                });
            }
        }

        public void Save()
        {
            var data = new HomeSaveData { cutaway = layout.Cutaway };
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var r = layout.Rooms[i];
                data.rooms.Add(new RoomSave
                {
                    id = r.Id,
                    hint = (int)r.Hint,
                    ox = r.Origin.x,
                    oy = r.Origin.y,
                    sx = r.Size.x,
                    sy = r.Size.y,
                    name = r.Name
                });
            }

            for (int i = 0; i < layout.Items.Count; i++)
            {
                var it = layout.Items[i];
                data.items.Add(new ItemSave
                {
                    instanceId = it.InstanceId,
                    catalogId = it.CatalogId,
                    kind = (int)it.Kind,
                    roomHint = (int)it.RoomHint,
                    surface = (int)it.Surface,
                    displayName = it.DisplayName,
                    cx = it.Cell.x,
                    cy = it.Cell.y,
                    wallDir = it.WallDir,
                    deviceId = it.DeviceId,
                    curtainOpen = it.CurtainOpen
                });
            }

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
