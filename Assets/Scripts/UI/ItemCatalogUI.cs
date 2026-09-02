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

        [Header("Scene Templates (Pretendard Inherited Slots)")]
        [SerializeField] private GameObject templateCard;
        [SerializeField] private GameObject templateSectionHeader;
        [SerializeField] private GameObject templateRoomChip;

        private RectTransform contentRoot;
        private RoomRecord focusedRoom = null;
        private bool isAddRoomMode = false;
        private bool isRenameMode = false;
        private readonly List<GameObject> activeInstances = new List<GameObject>();

        // Aesthetic Colors
        private static readonly Color BgCardColor = new Color(0.14f, 0.16f, 0.22f, 0.95f);
        private static readonly Color BgCardHover = new Color(0.20f, 0.25f, 0.35f, 1.0f);
        private static readonly Color AccentColor = new Color(0.25f, 0.65f, 1.0f, 1.0f);
        private static readonly Color AccentGreen = new Color(0.25f, 0.85f, 0.55f, 1.0f);
        private static readonly Color TextPrimary = new Color(0.95f, 0.96f, 0.98f, 1.0f);
        private static readonly Color TextSecondary = new Color(0.65f, 0.72f, 0.82f, 1.0f);
        private static readonly Color DisabledBgColor = new Color(0.10f, 0.11f, 0.14f, 0.6f);
        private static readonly Color DisabledTextColor = new Color(0.45f, 0.50f, 0.58f, 0.5f);

        private void Awake()
        {
            FindContentRoot();
            ResolveTemplates();
            if (captionText == null)
            {
                var captionTrans = transform.Find("Caption");
                if (captionTrans != null) captionText = captionTrans.GetComponent<Text>();
            }

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
            if (templatesRoot != null)
            {
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
        }

        private void OnEnable()
        {
            ResolveTemplates();
            Subscribe(true);
            isAddRoomMode = false;
            isRenameMode = false;
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
            isAddRoomMode = false;
            isRenameMode = false;
            if (drawer != null && !drawer.IsOpen)
            {
                drawer.Open();
            }
            BuildUI();
        }

        private void EnsureFocusedRoom()
        {
            var home = HomeController.Instance;
            if (home != null && home.Layout != null && home.Layout.Rooms.Count > 0)
            {
                if (focusedRoom == null || home.Layout.FindRoomById(focusedRoom.Id) == null)
                {
                    focusedRoom = home.Layout.Rooms[0];
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
                if (viewport != null)
                {
                    contentRoot = viewport.Find("Content") as RectTransform;
                }
            }

            if (contentRoot != null)
            {
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
        }

        private void ClearActiveInstances()
        {
            for (int i = 0; i < activeInstances.Count; i++)
            {
                if (activeInstances[i] != null) Destroy(activeInstances[i]);
            }
            activeInstances.Clear();

            if (contentRoot != null)
            {
                for (int i = contentRoot.childCount - 1; i >= 0; i--)
                {
                    var child = contentRoot.GetChild(i);
                    // Protect template container
                    if (child.name == "Templates") continue;
                    Destroy(child.gameObject);
                }
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
            if (contentRoot == null) return;

            ClearActiveInstances();

            if (isAddRoomMode)
            {
                BuildAddRoomModeUI();
            }
            else if (isRenameMode && focusedRoom != null)
            {
                BuildRenameRoomModeUI();
            }
            else
            {
                BuildRoomDeviceModeUI();
            }
        }

        // ==========================================
        // 1. Room-First Device Management View
        // ==========================================
        private void BuildRoomDeviceModeUI()
        {
            var home = HomeController.Instance;
            var layout = home != null ? home.Layout : null;

            if (captionText != null) captionText.text = "우리집 공간 & 장치 관리";

            if (layout != null && layout.Rooms.Count > 0)
            {
                BuildRoomChipsBar(layout);
            }

            if (focusedRoom == null)
            {
                var emptyCard = InstantiateCard("아직 생성된 공간이 없습니다.", "아래 버튼을 눌러 첫 번째 공간을 만드세요.", false, null);
                activeInstances.Add(emptyCard);

                var addBtn = InstantiateCard("➕  새로운 공간 만들기", "거실, 안방, 서재 등 추가", true, () =>
                {
                    isAddRoomMode = true;
                    BuildUI();
                }, AccentColor);
                activeInstances.Add(addBtn);
                return;
            }

            string roomEmoji = HomeItemDef.RoomEmoji(focusedRoom.Hint);
            string roomTitle = $"{roomEmoji}  {focusedRoom.Name}";
            var roomHeader = InstantiateCard(roomTitle, "공간 이름 변경 및 설정", true, () =>
            {
                isRenameMode = true;
                BuildUI();
            }, new Color(0.18f, 0.22f, 0.32f, 1.0f));
            activeInstances.Add(roomHeader);

            var installedItems = GetItemsInRoom(focusedRoom.Hint);
            if (installedItems.Count > 0)
            {
                var secLabel = InstantiateSectionHeader($"현재 설치된 기기 ({installedItems.Count}개)");
                activeInstances.Add(secLabel);

                for (int i = 0; i < installedItems.Count; i++)
                {
                    var item = installedItems[i];
                    string itemEmoji = HomeItemDef.CategoryRules.TryGetValue(item.Kind, out var r) ? r.Emoji : "📦";
                    string status = item.Kind == HomeItemKind.Light ? "💡 조명 (클릭하여 제어)" : $"{GetSurfaceDesc(item.Surface)} 설치됨";
                    var devCard = InstantiateCard($"{itemEmoji}  {item.DisplayName}", status, true, null, new Color(0.11f, 0.13f, 0.18f, 0.9f), AccentGreen);
                    activeInstances.Add(devCard);
                }
            }

            var addSecLabel = InstantiateSectionHeader("➕  이 공간에 장치 추가하기");
            activeInstances.Add(addSecLabel);

            var categories = new[]
            {
                HomeItemKind.Light,
                HomeItemKind.Heating,
                HomeItemKind.ElectricCurtain,
                HomeItemKind.AirConditioner,
                HomeItemKind.Vent,
                HomeItemKind.Gas,
                HomeItemKind.Elevator
            };

            for (int i = 0; i < categories.Length; i++)
            {
                var kind = categories[i];
                if (!HomeItemDef.CategoryRules.TryGetValue(kind, out var rule)) continue;

                var def = HomeItemDef.Create(kind, focusedRoom.Hint, focusedRoom.Name);
                bool blocked = layout != null && layout.IsCatalogBlocked(def);

                string title = $"{rule.Emoji}  {rule.CategoryName} 추가";
                string sub = blocked ? "✓ 이미 설치됨" : $"설치 위치: {GetSurfaceDesc(rule.DefaultSurface)} | {rule.Description}";

                var addDevBtn = InstantiateCard(title, sub, !blocked, () =>
                {
                    AddDeviceToFocusedRoom(kind);
                }, blocked ? DisabledBgColor : BgCardColor);
                activeInstances.Add(addDevBtn);
            }

            var addOtherRoomBtn = InstantiateCard("➕  새로운 공간 추가하기", "서재, 아이방, 드레스룸 등 다른 방 만들기", true, () =>
            {
                isAddRoomMode = true;
                BuildUI();
            }, new Color(0.18f, 0.22f, 0.30f));
            activeInstances.Add(addOtherRoomBtn);
        }

        private void BuildRoomChipsBar(HomeLayout layout)
        {
            var chipsContainer = new GameObject("RoomChipsBar");
            chipsContainer.transform.SetParent(contentRoot, false);
            var rect = chipsContainer.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 48f);

            var hGroup = chipsContainer.AddComponent<HorizontalLayoutGroup>();
            hGroup.childAlignment = TextAnchor.MiddleLeft;
            hGroup.childControlWidth = false;
            hGroup.childControlHeight = true;
            hGroup.childForceExpandWidth = false;
            hGroup.childForceExpandHeight = true;
            hGroup.spacing = 6f;

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                bool isSelected = focusedRoom != null && focusedRoom.Id == room.Id;
                string emoji = HomeItemDef.RoomEmoji(room.Hint);
                string chipText = $"{emoji} {room.Name}";

                GameObject chipGo;
                if (templateRoomChip != null)
                {
                    chipGo = Instantiate(templateRoomChip, chipsContainer.transform);
                }
                else
                {
                    chipGo = new GameObject($"Chip_{room.Name}");
                    chipGo.transform.SetParent(chipsContainer.transform, false);
                    chipGo.AddComponent<RectTransform>().sizeDelta = new Vector2(110f, 44f);
                    chipGo.AddComponent<Image>();
                    chipGo.AddComponent<Button>();
                }
                chipGo.SetActive(true);
                chipGo.name = $"Chip_{room.Name}";

                var chipImg = chipGo.GetComponent<Image>();
                if (chipImg != null)
                {
                    chipImg.color = isSelected ? AccentColor : new Color(0.18f, 0.20f, 0.26f, 0.95f);
                }

                var chipBtn = chipGo.GetComponent<Button>();
                if (chipBtn != null)
                {
                    var capturedRoom = room;
                    chipBtn.onClick.RemoveAllListeners();
                    chipBtn.onClick.AddListener(() =>
                    {
                        focusedRoom = capturedRoom;
                        BuildUI();
                    });
                }

                var txt = chipGo.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.text = chipText;
                    txt.fontStyle = FontStyle.Normal;
                    txt.fontSize = 18;
                    txt.resizeTextForBestFit = false;
                    txt.color = isSelected ? Color.white : TextSecondary;
                }
            }

            activeInstances.Add(chipsContainer);
        }

        // ==========================================
        // 2. Add New Room View (Preset Chips)
        // ==========================================
        private void BuildAddRoomModeUI()
        {
            if (captionText != null) captionText.text = "새로운 공간(방) 추가";

            var backBtn = InstantiateCard("← 돌아가기", "공간 및 기기 관리 목록으로", true, () =>
            {
                isAddRoomMode = false;
                BuildUI();
            }, AccentColor);
            activeInstances.Add(backBtn);

            var presetSec = InstantiateSectionHeader("추천 공간 프리셋 (원터치 1초 생성)");
            activeInstances.Add(presetSec);

            var presets = RoomPreset.RecommendedPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var presetBtn = InstantiateCard($"{preset.Emoji}  {preset.DefaultName}", "클릭 즉시 3D 디오라마에 공간 생성", true, () =>
                {
                    CreateRoomWithPreset(preset.Hint, preset.DefaultName);
                }, BgCardColor);
                activeInstances.Add(presetBtn);
            }
        }

        // ==========================================
        // 3. Rename Room Mode
        // ==========================================
        private void BuildRenameRoomModeUI()
        {
            if (captionText != null) captionText.text = $"[{focusedRoom.Name}] 이름 변경";

            var backBtn = InstantiateCard("← 취소 및 돌아가기", "이름 변경을 취소합니다", true, () =>
            {
                isRenameMode = false;
                BuildUI();
            }, AccentColor);
            activeInstances.Add(backBtn);

            var renamePresets = new[] { "서재", "아이방", "드레스룸", "알파룸", "홈카페", "게임룸", "작업실", "게스트룸", "냥이방" };
            var secLabel = InstantiateSectionHeader("추천 이름 선택");
            activeInstances.Add(secLabel);

            for (int i = 0; i < renamePresets.Length; i++)
            {
                string rName = renamePresets[i];
                var nameBtn = InstantiateCard($"✏️  '{rName}'(으)로 변경", "공간 및 기기 표시명 즉시 갱신", true, () =>
                {
                    ApplyRename(rName);
                }, BgCardColor);
                activeInstances.Add(nameBtn);
            }
        }

        private void CreateRoomWithPreset(RoomHint hint, string name)
        {
            var home = HomeController.EnsureExists();
            if (home == null) return;

            var room = home.CreateRoom(hint, name);
            if (room != null)
            {
                focusedRoom = room;
                isAddRoomMode = false;
                BuildUI();
            }
        }

        private void ApplyRename(string newName)
        {
            if (focusedRoom == null) return;
            var home = HomeController.Instance;
            if (home != null)
            {
                home.RenameRoom(focusedRoom.Id, newName);
            }
            isRenameMode = false;
            BuildUI();
        }

        private void AddDeviceToFocusedRoom(HomeItemKind kind)
        {
            if (focusedRoom == null) return;
            var home = HomeController.EnsureExists();
            if (home == null) return;

            var def = HomeItemDef.Create(kind, focusedRoom.Hint, focusedRoom.Name);
            bool success = home.PlaceFromCatalog(def);
            if (success)
            {
                home.Save();
                home.FrameCamera();
                BuildUI();
            }
        }

        private List<PlacedItem> GetItemsInRoom(RoomHint hint)
        {
            var list = new List<PlacedItem>();
            var home = HomeController.Instance;
            if (home == null || home.Layout == null) return list;

            for (int i = 0; i < home.Layout.Items.Count; i++)
            {
                var it = home.Layout.Items[i];
                if (it.RoomHint == hint) list.Add(it);
            }
            return list;
        }

        // ==========================================
        // Template Instantiation Factory Methods
        // ==========================================
        private GameObject InstantiateSectionHeader(string text)
        {
            GameObject go;
            if (templateSectionHeader != null)
            {
                go = Instantiate(templateSectionHeader, contentRoot);
            }
            else
            {
                go = new GameObject($"Sec_{text}");
                go.transform.SetParent(contentRoot, false);
                go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 36f);
                go.AddComponent<Text>();
            }
            go.SetActive(true);
            go.name = $"Sec_{text}";

            var txt = go.GetComponent<Text>();
            if (txt != null)
            {
                txt.text = text;
                txt.fontSize = 22;
                txt.fontStyle = FontStyle.Normal;
                txt.resizeTextForBestFit = false;
                txt.color = new Color(0.75f, 0.82f, 0.92f, 1.0f);
            }
            return go;
        }

        private GameObject InstantiateCard(
            string title,
            string sub,
            bool interactable,
            UnityEngine.Events.UnityAction onClick,
            Color? bgColor = null,
            Color? subTextColor = null)
        {
            GameObject cardGo;
            if (templateCard != null)
            {
                cardGo = Instantiate(templateCard, contentRoot);
            }
            else
            {
                cardGo = new GameObject($"Card_{title}");
                cardGo.transform.SetParent(contentRoot, false);
                cardGo.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 76f);
                cardGo.AddComponent<Image>();
                cardGo.AddComponent<Button>();
            }
            cardGo.SetActive(true);
            cardGo.name = $"Card_{title}";

            Color bg = bgColor ?? (interactable ? BgCardColor : DisabledBgColor);
            var img = cardGo.GetComponent<Image>();
            if (img != null) img.color = bg;

            var btn = cardGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = interactable;
                btn.onClick.RemoveAllListeners();
                if (onClick != null) btn.onClick.AddListener(onClick);

                var colors = btn.colors;
                colors.normalColor = bg;
                colors.highlightedColor = BgCardHover;
                colors.pressedColor = AccentColor * 0.75f;
                colors.selectedColor = bg;
                colors.disabledColor = DisabledBgColor;
                btn.colors = colors;
            }

            var texts = cardGo.GetComponentsInChildren<Text>(true);
            Text titleTxt = null;
            Text subTxt = null;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name == "Title" || i == 0) titleTxt = texts[i];
                if (texts[i].name == "Sub" || (i == 1 && texts.Length > 1)) subTxt = texts[i];
            }

            if (titleTxt != null)
            {
                titleTxt.text = title;
                titleTxt.fontSize = 20;
                titleTxt.fontStyle = FontStyle.Normal;
                titleTxt.resizeTextForBestFit = false;
                titleTxt.color = interactable ? TextPrimary : DisabledTextColor;
            }

            if (subTxt != null)
            {
                subTxt.text = sub;
                subTxt.fontSize = 18;
                subTxt.fontStyle = FontStyle.Normal;
                subTxt.resizeTextForBestFit = false;
                subTxt.color = interactable
                    ? (subTextColor ?? (title.StartsWith("←") ? AccentColor : TextSecondary))
                    : DisabledTextColor;
            }

            return cardGo;
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
