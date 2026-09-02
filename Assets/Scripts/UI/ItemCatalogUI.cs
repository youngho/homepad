using System.Collections.Generic;
using Homepad.Home;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class ItemCatalogUI : MonoBehaviour
    {
        [SerializeField] private CatalogDrawerUI drawer;
        [SerializeField] private Text captionText;
        [SerializeField] private DeviceOverlayUI overlay;

        [Header("Scene Templates (Pretendard Inherited Slots)")]
        [SerializeField] private GameObject templateCard;
        [SerializeField] private GameObject templateSectionHeader;
        [SerializeField] private GameObject templateRoomChip;

        private RectTransform contentRoot;
        private RoomRecord focusedRoom;
        private ViewMode viewMode = ViewMode.Room;
        private readonly List<GameObject> activeInstances = new List<GameObject>();

        private static readonly Color BgCard = new Color(0.14f, 0.16f, 0.22f, 0.95f);
        private static readonly Color BgInstalled = new Color(0.11f, 0.13f, 0.18f, 0.95f);
        private static readonly Color Accent = new Color(0.25f, 0.65f, 1.0f, 1.0f);
        private static readonly Color AccentGreen = new Color(0.25f, 0.85f, 0.55f, 1.0f);
        private static readonly Color AccentDanger = new Color(0.95f, 0.35f, 0.35f, 1.0f);
        private static readonly Color ChipIdle = new Color(0.18f, 0.20f, 0.26f, 0.95f);
        private static readonly Color TextPrimary = new Color(0.95f, 0.96f, 0.98f, 1.0f);
        private static readonly Color TextSecondary = new Color(0.65f, 0.72f, 0.82f, 1.0f);
        private static readonly Color DisabledBg = new Color(0.10f, 0.11f, 0.14f, 0.6f);
        private static readonly Color DisabledText = new Color(0.45f, 0.50f, 0.58f, 0.5f);

        private static readonly HomeItemKind[] RoomDeviceKinds =
        {
            HomeItemKind.Light,
            HomeItemKind.Heating,
            HomeItemKind.ElectricCurtain,
            HomeItemKind.AirConditioner,
            HomeItemKind.Vent
        };

        private enum ViewMode
        {
            Room,
            AddRoom,
            Rename,
            ConfirmDelete
        }

        private void Awake()
        {
            FindContentRoot();
            ResolveTemplates();
            if (captionText == null)
            {
                var captionTrans = transform.Find("Caption");
                if (captionTrans != null) captionText = captionTrans.GetComponent<Text>();
            }

            if (overlay == null) overlay = GetComponentInParent<DeviceOverlayUI>();

            if (captionText != null)
            {
                captionText.fontStyle = FontStyle.Normal;
                captionText.resizeTextForBestFit = false;
                captionText.fontSize = Mathf.Max(26, captionText.fontSize);
            }
        }

        private void ResolveTemplates()
        {
            var templatesRoot = transform.Find("Templates");
            if (templatesRoot == null) return;

            if (templateCard == null)
            {
                var t = templatesRoot.Find("TemplateCard");
                if (t != null) templateCard = t.gameObject;
            }

            if (templateSectionHeader == null)
            {
                var t = templatesRoot.Find("TemplateSectionHeader");
                if (t != null) templateSectionHeader = t.gameObject;
            }

            if (templateRoomChip == null)
            {
                var t = templatesRoot.Find("TemplateRoomChip");
                if (t != null) templateRoomChip = t.gameObject;
            }
        }

        private void OnEnable()
        {
            ResolveTemplates();
            Subscribe(true);
            viewMode = ViewMode.Room;
            EnsureFocusedRoom();
            BuildUI();
        }

        private void Start()
        {
            ResolveTemplates();
            Subscribe(true);
            EnsureFocusedRoom();
            BuildUI();
        }

        private void OnDisable()
        {
            Subscribe(false);
        }

        private void Subscribe(bool on)
        {
            var home = HomeController.Instance;
            if (on && home == null) home = HomeController.EnsureExists();
            if (home == null) return;

            home.LayoutChanged -= HandleLayoutChanged;
            home.RoomSelected -= HandleRoomSelected;
            if (on)
            {
                home.LayoutChanged += HandleLayoutChanged;
                home.RoomSelected += HandleRoomSelected;
            }
        }

        private void HandleLayoutChanged()
        {
            EnsureFocusedRoom();
            BuildUI();
        }

        private void HandleRoomSelected(RoomRecord room)
        {
            focusedRoom = room;
            viewMode = ViewMode.Room;
            if (drawer != null && !drawer.IsOpen) drawer.Open();
            BuildUI();
        }

        private void EnsureFocusedRoom()
        {
            var home = HomeController.Instance;
            if (home != null && home.Layout != null && home.Layout.Rooms.Count > 0)
            {
                if (focusedRoom == null || home.Layout.FindRoomById(focusedRoom.Id) == null)
                {
                    focusedRoom = home.SelectedRoom != null
                        ? home.Layout.FindRoomById(home.SelectedRoom.Id)
                        : null;
                    if (focusedRoom == null) focusedRoom = home.Layout.Rooms[0];
                }
            }
            else
            {
                focusedRoom = null;
            }
        }

        private void FindContentRoot()
        {
            if (contentRoot != null) return;
            var scroll = transform.Find("Scroll");
            if (scroll != null)
            {
                var viewport = scroll.Find("Viewport");
                if (viewport != null) contentRoot = viewport.Find("Content") as RectTransform;
            }

            if (contentRoot == null) return;

            var layout = contentRoot.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.spacing = 10f;
                layout.padding = new RectOffset(16, 16, 16, 24);
            }

            var fitter = contentRoot.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private void ClearActiveInstances()
        {
            for (int i = 0; i < activeInstances.Count; i++)
            {
                if (activeInstances[i] != null) Destroy(activeInstances[i]);
            }

            activeInstances.Clear();
            if (contentRoot == null) return;

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var child = contentRoot.GetChild(i);
                if (child.name == "Templates") continue;
                Destroy(child.gameObject);
            }
        }

        public void Refresh()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            ResolveTemplates();
            FindContentRoot();
            if (contentRoot == null || templateCard == null) return;

            ClearActiveInstances();

            var home = HomeController.Instance;
            var layout = home != null ? home.Layout : null;
            if (layout == null || layout.Rooms.Count == 0)
            {
                BuildEmptyHouseUI();
                return;
            }

            switch (viewMode)
            {
                case ViewMode.AddRoom:
                    BuildAddRoomModeUI(layout);
                    break;
                case ViewMode.Rename when focusedRoom != null:
                    BuildRenameRoomModeUI();
                    break;
                case ViewMode.ConfirmDelete when focusedRoom != null:
                    BuildConfirmDeleteUI();
                    break;
                default:
                    BuildRoomDeviceModeUI(layout);
                    break;
            }
        }

        private void BuildEmptyHouseUI()
        {
            SetCaption("공간 만들기");
            activeInstances.Add(InstantiateSectionHeader("먼저 공간을 만드세요"));
            AddRoomPresetCards(null);
        }

        private void BuildRoomDeviceModeUI(HomeLayout layout)
        {
            if (focusedRoom == null)
            {
                BuildEmptyHouseUI();
                return;
            }

            SetCaption(focusedRoom.Name);
            BuildRoomSwitcher(layout);

            var installed = GetItemsInRoom(focusedRoom);
            if (installed.Count > 0)
            {
                activeInstances.Add(InstantiateSectionHeader("이 공간의 장치"));
                var home = HomeController.Instance;
                for (int i = 0; i < installed.Count; i++)
                {
                    var item = installed[i];
                    string emoji = HomeItemDef.CategoryRules.TryGetValue(item.Kind, out var rule) ? rule.Emoji : "·";
                    activeInstances.Add(InstantiateDeviceCard(
                        $"{emoji}  {item.DisplayName}",
                        "탭해서 제어",
                        () =>
                        {
                            overlay?.Show(item);
                            drawer?.Close();
                        },
                        () => home?.RemoveItem(item.InstanceId)));
                }
            }

            var addable = CollectAddableKinds(layout, focusedRoom);
            if (addable.Count > 0)
            {
                activeInstances.Add(InstantiateSectionHeader($"{focusedRoom.Name}에 장치 추가"));
                for (int i = 0; i < addable.Count; i++)
                {
                    var kind = addable[i];
                    if (!HomeItemDef.CategoryRules.TryGetValue(kind, out var rule)) continue;
                    string sub = kind == HomeItemKind.Light ? "이 공간에 바로 설치" : GetSurfaceDesc(rule.DefaultSurface);
                    activeInstances.Add(InstantiateCard(
                        $"{rule.Emoji}  {rule.CategoryName}",
                        sub,
                        true,
                        () => AddDeviceToFocusedRoom(kind)));
                }
            }
            else if (installed.Count == 0)
            {
                activeInstances.Add(InstantiateCard("추가할 장치가 없습니다", "다른 공간을 선택하거나 만들어 보세요", false, null));
            }

            activeInstances.Add(InstantiateSectionHeader("이 공간"));
            activeInstances.Add(InstantiateCard("이름 바꾸기", focusedRoom.Name, true, () =>
            {
                viewMode = ViewMode.Rename;
                BuildUI();
            }));
            activeInstances.Add(InstantiateCard("공간 삭제", "장치도 함께 제거됩니다", true, () =>
            {
                viewMode = ViewMode.ConfirmDelete;
                BuildUI();
            }, AccentDanger));
        }

        private void BuildRoomSwitcher(HomeLayout layout)
        {
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                bool selected = focusedRoom != null && focusedRoom.Id == room.Id;
                string label = $"{HomeItemDef.RoomEmoji(room.Hint)}  {room.Name}";
                activeInstances.Add(InstantiateRoomChip(label, selected, () =>
                {
                    focusedRoom = room;
                    viewMode = ViewMode.Room;
                    var home = HomeController.Instance;
                    if (home != null) home.SelectRoom(room);
                    else BuildUI();
                }));
            }

            activeInstances.Add(InstantiateRoomChip("공간 추가", false, () =>
            {
                viewMode = ViewMode.AddRoom;
                BuildUI();
            }, Accent));
        }

        private void BuildAddRoomModeUI(HomeLayout layout)
        {
            SetCaption("공간 추가");
            activeInstances.Add(InstantiateCard("돌아가기", focusedRoom != null ? focusedRoom.Name : "공간 목록", true, () =>
            {
                viewMode = ViewMode.Room;
                BuildUI();
            }, Accent));
            activeInstances.Add(InstantiateSectionHeader("아직 없는 공간"));
            AddRoomPresetCards(layout);
        }

        private void BuildRenameRoomModeUI()
        {
            SetCaption("이름 바꾸기");
            activeInstances.Add(InstantiateCard("돌아가기", focusedRoom.Name, true, () =>
            {
                viewMode = ViewMode.Room;
                BuildUI();
            }, Accent));
            activeInstances.Add(InstantiateSectionHeader("이름 선택"));

            var names = new[] { "거실", "안방", "서재", "아이방", "드레스룸", "알파룸", "홈카페", "작업실" };
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (name == focusedRoom.Name) continue;
                activeInstances.Add(InstantiateCard(name, "이 공간 이름으로", true, () => ApplyRename(name)));
            }
        }

        private void BuildConfirmDeleteUI()
        {
            SetCaption("공간 삭제");
            activeInstances.Add(InstantiateCard(
                $"{focusedRoom.Name}을(를) 삭제할까요?",
                "이 공간의 장치도 함께 제거됩니다",
                false,
                null));
            activeInstances.Add(InstantiateCard("삭제", focusedRoom.Name, true, DeleteCurrentRoom, AccentDanger));
            activeInstances.Add(InstantiateCard("취소", "이 공간 유지", true, () =>
            {
                viewMode = ViewMode.Room;
                BuildUI();
            }, Accent));
        }

        private void AddRoomPresetCards(HomeLayout layout)
        {
            var presets = RoomPreset.RecommendedPresets;
            int added = 0;
            for (int i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                if (layout != null && layout.FindRoom(preset.Hint) != null) continue;
                added++;
                activeInstances.Add(InstantiateCard(
                    $"{preset.Emoji}  {preset.DefaultName}",
                    "만든 뒤 장치를 넣습니다",
                    true,
                    () => CreateRoomWithPreset(preset.Hint, preset.DefaultName)));
            }

            if (added == 0)
            {
                activeInstances.Add(InstantiateCard("모든 공간이 있습니다", "이름은 각 공간에서 바꿀 수 있습니다", false, null));
            }
        }

        private List<HomeItemKind> CollectAddableKinds(HomeLayout layout, RoomRecord room)
        {
            var list = new List<HomeItemKind>();
            for (int i = 0; i < RoomDeviceKinds.Length; i++)
            {
                var kind = RoomDeviceKinds[i];
                var def = HomeItemDef.Create(kind, room.Hint, room.Name);
                if (layout != null && layout.IsCatalogBlocked(def)) continue;
                list.Add(kind);
            }

            if (room.Hint == RoomHint.Kitchen)
            {
                var gas = HomeItemDef.Create(HomeItemKind.Gas, room.Hint, room.Name);
                if (layout == null || !layout.IsCatalogBlocked(gas)) list.Add(HomeItemKind.Gas);
            }

            if (room.Hint == RoomHint.Entrance)
            {
                var elevator = HomeItemDef.Create(HomeItemKind.Elevator, room.Hint, room.Name);
                if (layout == null || !layout.IsCatalogBlocked(elevator)) list.Add(HomeItemKind.Elevator);
            }

            return list;
        }

        private void CreateRoomWithPreset(RoomHint hint, string name)
        {
            var home = HomeController.EnsureExists();
            if (home == null) return;
            if (home.Layout != null && home.Layout.FindRoom(hint) != null) return;

            var room = home.CreateRoom(hint, name);
            if (room == null) return;

            focusedRoom = room;
            viewMode = ViewMode.Room;
            BuildUI();
        }

        private void ApplyRename(string newName)
        {
            if (focusedRoom == null) return;
            HomeController.Instance?.RenameRoom(focusedRoom.Id, newName);
            viewMode = ViewMode.Room;
            BuildUI();
        }

        private void DeleteCurrentRoom()
        {
            if (focusedRoom == null) return;
            var home = HomeController.Instance;
            if (home == null) return;

            home.DeleteRoom(focusedRoom.Id);
            focusedRoom = null;
            viewMode = ViewMode.Room;
            EnsureFocusedRoom();
            BuildUI();
        }

        private void AddDeviceToFocusedRoom(HomeItemKind kind)
        {
            if (focusedRoom == null) return;
            var home = HomeController.EnsureExists();
            if (home == null) return;

            var def = HomeItemDef.Create(kind, focusedRoom.Hint, focusedRoom.Name);
            home.PlaceFromCatalog(def, focusedRoom);
        }

        private List<PlacedItem> GetItemsInRoom(RoomRecord room)
        {
            var list = new List<PlacedItem>();
            var home = HomeController.Instance;
            if (home == null || home.Layout == null || room == null) return list;

            for (int i = 0; i < home.Layout.Items.Count; i++)
            {
                var item = home.Layout.Items[i];
                if (item.RoomHint == room.Hint) list.Add(item);
            }

            return list;
        }

        private void SetCaption(string text)
        {
            if (captionText != null) captionText.text = text;
        }

        private GameObject InstantiateSectionHeader(string text)
        {
            var go = Instantiate(templateSectionHeader, contentRoot);
            go.SetActive(true);
            go.name = "Sec";
            ApplyText(go.GetComponent<Text>(), text, 22, new Color(0.75f, 0.82f, 0.92f, 1.0f));
            return go;
        }

        private GameObject InstantiateRoomChip(string label, bool selected, UnityEngine.Events.UnityAction onClick, Color? selectedText = null)
        {
            var chipGo = Instantiate(templateRoomChip, contentRoot);
            chipGo.SetActive(true);
            chipGo.name = "Chip";

            var rect = chipGo.transform as RectTransform;
            if (rect != null) rect.sizeDelta = new Vector2(0f, 44f);

            var le = chipGo.GetComponent<LayoutElement>();
            if (le == null) le = chipGo.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;
            le.flexibleWidth = 1f;

            var img = chipGo.GetComponent<Image>();
            if (img != null) img.color = selected ? Accent : ChipIdle;

            var btn = chipGo.GetComponent<Button>();
            BindButton(btn, true, onClick);

            var txt = chipGo.GetComponentInChildren<Text>();
            ApplyText(txt, label, 18, selected ? Color.white : (selectedText ?? TextSecondary));
            if (txt != null)
            {
                txt.alignment = TextAnchor.MiddleCenter;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            return chipGo;
        }

        private GameObject InstantiateDeviceCard(
            string title,
            string status,
            UnityEngine.Events.UnityAction onOpen,
            UnityEngine.Events.UnityAction onDelete)
        {
            var cardGo = InstantiateCard(title, status, true, onOpen, BgInstalled, AccentGreen);
            var delete = cardGo.transform.Find("Delete");
            if (delete != null)
            {
                delete.gameObject.SetActive(true);
                var delImg = delete.GetComponent<Image>();
                if (delImg != null) delImg.color = AccentDanger;
                BindButton(delete.GetComponent<Button>(), true, onDelete);
                var delTxt = delete.GetComponentInChildren<Text>();
                ApplyText(delTxt, "삭제", 18, Color.white);

                var textsRoot = cardGo.transform.Find("Texts") as RectTransform;
                if (textsRoot != null) textsRoot.offsetMax = new Vector2(-88f, textsRoot.offsetMax.y);
            }

            return cardGo;
        }

        private GameObject InstantiateCard(
            string title,
            string sub,
            bool interactable,
            UnityEngine.Events.UnityAction onClick,
            Color? bgColor = null,
            Color? subColor = null)
        {
            var cardGo = Instantiate(templateCard, contentRoot);
            cardGo.SetActive(true);
            cardGo.name = "Card";

            var delete = cardGo.transform.Find("Delete");
            if (delete != null) delete.gameObject.SetActive(false);

            Color bg = bgColor ?? (interactable ? BgCard : DisabledBg);
            var img = cardGo.GetComponent<Image>();
            if (img != null) img.color = bg;

            BindButton(cardGo.GetComponent<Button>(), interactable, onClick);

            Text titleTxt = null;
            Text subTxt = null;
            var texts = cardGo.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].transform.parent != null && texts[i].transform.parent.name == "Delete") continue;
                if (texts[i].name == "Title") titleTxt = texts[i];
                else if (texts[i].name == "Sub") subTxt = texts[i];
            }

            ApplyText(titleTxt, title, 20, interactable ? TextPrimary : DisabledText);
            Color subCol = interactable ? (subColor ?? TextSecondary) : DisabledText;
            ApplyText(subTxt, sub, 18, subCol);
            return cardGo;
        }

        private static void BindButton(Button btn, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            if (btn == null) return;
            btn.interactable = interactable;
            btn.onClick.RemoveAllListeners();
            if (onClick != null) btn.onClick.AddListener(onClick);

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.80f, 0.84f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            btn.colors = colors;
        }

        private static void ApplyText(Text txt, string value, int size, Color color)
        {
            if (txt == null) return;
            txt.text = value;
            txt.fontSize = size;
            txt.fontStyle = FontStyle.Normal;
            txt.resizeTextForBestFit = false;
            txt.color = color;
        }

        private static string GetSurfaceDesc(Surface surface)
        {
            return surface switch
            {
                Surface.Ceiling => "천장",
                Surface.Wall => "벽면",
                Surface.Window => "창문",
                _ => "바닥"
            };
        }
    }
}
