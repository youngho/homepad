using System;
using System.Collections.Generic;
using Homepad.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Homepad.Home
{
    [DefaultExecutionOrder(-50)]
    public class HomeController : MonoBehaviour
    {
        public const string SaveKey = "Homepad.MyHome.v2";

        public static HomeController Instance { get; private set; }

        public event Action LayoutChanged;
        public event Action<PlacedItem> ItemClicked;
        public event Action OverlayDismissed;

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
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
                OverlayDismissed?.Invoke();
            }

            if (mouse.rightButton.wasPressedThisFrame && !PointerOverUi())
            {
                CancelPlacement();
                OverlayDismissed?.Invoke();
            }

            Vector2 mousePos = mouse.position.ReadValue();
            bool overUi = PointerOverUi();

            if (pendingDef != null && !overUi)
            {
                UpdateGhost(mousePos);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    ConfirmPlacement(mousePos);
                }

                return;
            }

            if (overUi)
            {
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    pointerHeldOnItem = false;
                    dragging = false;
                    dragView = null;
                }

                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pointerDownPos = mousePos;
                dragView = RaycastItem(mousePos);
                pointerHeldOnItem = dragView != null;
                dragging = false;
                if (dragView == null)
                {
                    OverlayDismissed?.Invoke();
                }
            }

            if (pointerHeldOnItem && dragView != null && mouse.leftButton.isPressed)
            {
                if (!dragging && (mousePos - pointerDownPos).sqrMagnitude > DragThreshold * DragThreshold)
                {
                    dragging = true;
                    OverlayDismissed?.Invoke();
                }

                if (dragging)
                {
                    DragItem(dragView, mousePos);
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (dragging && dragView != null)
                {
                    FinishDrag(dragView, mousePos);
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

        public void BeginPlacement(HomeItemDef def)
        {
            PlaceFromCatalog(def);
        }

        public bool PlaceFromCatalog(HomeItemDef def)
        {
            if (def == null) return false;
            if (layout.IsCatalogBlocked(def)) return false;
            CancelPlacement();

            RoomRecord room;
            if (def.Kind == HomeItemKind.ElectricCurtain && layout.Rooms.Count > 0)
            {
                room = layout.Rooms[0];
            }
            else
            {
                room = service.EnsureRoom(def.RoomHint);
            }

            var cell = service.DefaultCell(def, room);
            int wallDir = service.DefaultWallDir(def, room, cell);
            var item = service.Place(def, cell, wallDir);
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
            if (!Physics.Raycast(ray, out var hit, 200f)) return null;
            return hit.collider.GetComponentInParent<HomeItemView>();
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

        private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        private static bool PointerOverUi()
        {
            if (EventSystem.current == null || Mouse.current == null) return false;
            var data = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };
            UiHits.Clear();
            EventSystem.current.RaycastAll(data, UiHits);
            return UiHits.Count > 0;
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
            Vector3 center = new Vector3(0f, 0.5f, 0f);
            float size = 7.5f;

            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.transform.rotation = Quaternion.Euler(35.264f, 45f, 0f);
            cam.transform.position = center + cam.transform.rotation * new Vector3(0f, 0f, -24f);
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
                            return;
                        }
                    }
                    catch (Exception)
                    {
                        // ignore corrupt save
                    }
                }
            }

            // Default Setup: populate all rooms and items for modern apartment
            PopulateDefaultHome();
        }

        public void PopulateDefaultHome()
        {
            layout.Rooms.Clear();
            layout.Items.Clear();

            service.EnsureRoom(RoomHint.Living);
            service.EnsureRoom(RoomHint.Master);
            service.EnsureRoom(RoomHint.Bedroom);
            service.EnsureRoom(RoomHint.Bedroom2);
            service.EnsureRoom(RoomHint.Kitchen);

            // Default: Living Room Light, Living Room Heat, Electric Curtain
            var lightDef = HomeItemDef.Find("light_living");
            if (lightDef != null) PlaceFromCatalog(lightDef);

            var heatDef = HomeItemDef.Find("heat_living");
            if (heatDef != null) PlaceFromCatalog(heatDef);

            var curtainDef = HomeItemDef.Find("curtain");
            if (curtainDef != null) PlaceFromCatalog(curtainDef);
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
