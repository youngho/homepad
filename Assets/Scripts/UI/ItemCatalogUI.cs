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

        // Aesthetic Clean Palette
        private static readonly Color BgCardColor = new Color(0.14f, 0.16f, 0.22f, 0.95f);
        private static readonly Color BgCardHover = new Color(0.20f, 0.25f, 0.35f, 1.0f);
        private static readonly Color AccentColor = new Color(0.25f, 0.65f, 1.0f, 1.0f);
        private static readonly Color AccentGreen = new Color(0.25f, 0.85f, 0.55f, 1.0f);
        private static readonly Color AccentDanger = new Color(0.95f, 0.35f, 0.35f, 1.0f);
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

            var home = HomeController.Instance;
            var layout = home != null ? home.Layout : null;

            // 1. If house has NO rooms at all: Show ONLY Space Creation flow
            if (layout == null || layout.Rooms.Count == 0)
            {
                BuildEmptyHouseUI();
                return;
            }

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
        // 1. Empty House Mode (공간 만들기만 노출)
        // ==========================================
        private void BuildEmptyHouseUI()
        {
            if (captionText != null) captionText.text = "공간 만들기";

            var sec = InstantiateSectionHeader("우리집 첫 공간을 만드세요");
            activeInstances.Add(sec);

            var presets = RoomPreset.RecommendedPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var presetBtn = InstantiateCard($"{preset.Emoji}  {preset.DefaultName} 만들기", "클릭 즉시 3D 디오라마에 공간 생성", true, () =>
                {
                    CreateRoomWithPreset(preset.Hint, preset.DefaultName);
                }, BgCardColor);
                activeInstances.Add(presetBtn);
            }
        }

        // ==========================================
        // 2. Focused Room Management View
        // ==========================================
        private void BuildRoomDeviceModeUI()
        {
            var home = HomeController.Instance;
            var layout = home != null ? home.Layout : null;

            if (captionText != null) captionText.text = "공간 및 장치 관리";

            // A. Room Chips Bar (상단 공간 탭)
            if (layout != null && layout.Rooms.Count > 0)
            {
                BuildRoomChipsBar(layout);
            }

            if (focusedRoom == null) return;

            // B. Focused Room Header (이름 변경 / 공간 삭제)
            string roomEmoji = HomeItemDef.RoomEmoji(focusedRoom.Hint);
            string roomTitle = $"{roomEmoji}  {focusedRoom.Name}";
            var roomHeader = InstantiateRoomHeaderCard(roomTitle, () =>
            {
                isRenameMode = true;
                BuildUI();
            }, () =>
            {
                DeleteCurrentRoom();
            });
            activeInstances.Add(roomHeader);

            // C. Section: 현재 설치된 기기 목록 (개별 삭제 버튼 포함)
            var installedItems = GetItemsInRoom(focusedRoom.Hint);
            if (installedItems.Count > 0)
            {
                var secLabel = InstantiateSectionHeader($"설치된 기기 ({installedItems.Count}개)");
                activeInstances.Add(secLabel);

                for (int i = 0; i < installedItems.Count; i++)
                {
                    var item = installedItems[i];
                    string itemEmoji = HomeItemDef.CategoryRules.TryGetValue(item.Kind, out var r) ? r.Emoji : "📦";
                    string status = item.Kind == HomeItemKind.Light ? "💡 조명" : $"{GetSurfaceDesc(item.Surface)} 설치";

                    var devCard = InstantiateDeviceWithDeleteCard(
                        $"{itemEmoji}  {item.DisplayName}",
                        status,
                        () =>
                        {
                            home?.RemoveItem(item.InstanceId);
                        });
                    activeInstances.Add(devCard);
                }
            }

            // D. Section: 보고 있는 방에 장치 추가 (묻지 않고 바로 추가!)
            var addSecLabel = InstantiateSectionHeader($"➕  {focusedRoom.Name}에 장치 추가");
            activeInstances.Add(addSecLabel);

            // Room-attachable device kinds (Gas and Elevator are separate infrastructure)
            var roomDeviceKinds = new[]
            {
                HomeItemKind.Light,
                HomeItemKind.Heating,
                HomeItemKind.ElectricCurtain,
                HomeItemKind.AirConditioner,
                HomeItemKind.Vent
            };

            for (int i = 0; i < roomDeviceKinds.Length; i++)
            {
                var kind = roomDeviceKinds[i];
                if (!HomeItemDef.CategoryRules.TryGetValue(kind, out var rule)) continue;

                var def = HomeItemDef.Create(kind, focusedRoom.Hint, focusedRoom.Name);
                bool blocked = layout != null && layout.IsCatalogBlocked(def);

                string title = $"{rule.Emoji}  {rule.CategoryName} 추가";
                string sub = blocked ? "✓ 이미 설치됨" : $"설치 위치: {GetSurfaceDesc(rule.DefaultSurface)}";

                var addDevBtn = InstantiateCard(title, sub, !blocked, () =>
                {
                    AddDeviceToFocusedRoom(kind);
                }, blocked ? DisabledBgColor : BgCardColor);
                activeInstances.Add(addDevBtn);
            }

            // E. Section: 전체 시설 (가스·엘리베이터: 방 묻지 않고 전용 위치 자동 배치)
            var infraSecLabel = InstantiateSectionHeader("🏢  세대 공용 / 안전 시설");
            activeInstances.Add(infraSecLabel);

            var gasDef = HomeItemDef.Create(HomeItemKind.Gas, RoomHint.Kitchen, "주방");
            bool gasBlocked = layout != null && layout.IsCatalogBlocked(gasDef);
            var gasBtn = InstantiateCard("🛡️  가스 밸브", gasBlocked ? "✓ 주방에 설치됨" : "주방 안전 자동 차단 밸브", !gasBlocked, () =>
            {
                home?.PlaceFromCatalog(gasDef);
            }, gasBlocked ? DisabledBgColor : BgCardColor);
            activeInstances.Add(gasBtn);

            var elDef = HomeItemDef.Create(HomeItemKind.Elevator, RoomHint.Entrance, "현관");
            bool elBlocked = layout != null && layout.IsCatalogBlocked(elDef);
            var elBtn = InstantiateCard("🛗  엘리베이터", elBlocked ? "✓ 현관에 설치됨" : "현관 사전 호출 제어기", !elBlocked, () =>
            {
                home?.PlaceFromCatalog(elDef);
            }, elBlocked ? DisabledBgColor : BgCardColor);
            activeInstances.Add(elBtn);
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

                var chipGo = Instantiate(templateRoomChip, chipsContainer.transform);
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

            // Add Room Chip Button
            var addChipGo = Instantiate(templateRoomChip, chipsContainer.transform);
            addChipGo.SetActive(true);
            addChipGo.name = "Chip_AddRoom";
            var addImg = addChipGo.GetComponent<Image>();
            if (addImg != null) addImg.color = new Color(0.20f, 0.24f, 0.32f, 0.95f);
            var addBtn = addChipGo.GetComponent<Button>();
            if (addBtn != null)
            {
                addBtn.onClick.RemoveAllListeners();
                addBtn.onClick.AddListener(() =>
                {
                    isAddRoomMode = true;
                    BuildUI();
                });
            }
            var addTxt = addChipGo.GetComponentInChildren<Text>();
            if (addTxt != null)
            {
                addTxt.text = "➕ 공간 추가";
                addTxt.fontStyle = FontStyle.Normal;
                addTxt.fontSize = 18;
                addTxt.color = AccentColor;
            }

            activeInstances.Add(chipsContainer);
        }

        // ==========================================
        // 3. Add New Room View (Preset Chips)
        // ==========================================
        private void BuildAddRoomModeUI()
        {
            if (captionText != null) captionText.text = "새로운 공간(방) 추가";

            var backBtn = InstantiateCard("← 돌아가기", "공간 및 기기 목록으로 복귀", true, () =>
            {
                isAddRoomMode = false;
                BuildUI();
            }, AccentColor);
            activeInstances.Add(backBtn);

            var presetSec = InstantiateSectionHeader("추천 공간 프리셋 (원터치 생성)");
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
        // 4. Rename Room Mode
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

        private void DeleteCurrentRoom()
        {
            if (focusedRoom == null) return;
            var home = HomeController.Instance;
            if (home != null)
            {
                home.DeleteRoom(focusedRoom.Id);
                focusedRoom = null;
                EnsureFocusedRoom();
                BuildUI();
            }
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
        // Template Instantiation Methods
        // ==========================================
        private GameObject InstantiateSectionHeader(string text)
        {
            var go = Instantiate(templateSectionHeader, contentRoot);
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

        private GameObject InstantiateRoomHeaderCard(string title, UnityEngine.Events.UnityAction onRename, UnityEngine.Events.UnityAction onDelete)
        {
            var cardGo = Instantiate(templateCard, contentRoot);
            cardGo.SetActive(true);
            cardGo.name = $"RoomHeader_{title}";

            var img = cardGo.GetComponent<Image>();
            if (img != null) img.color = new Color(0.18f, 0.22f, 0.32f, 1.0f);

            var texts = cardGo.GetComponentsInChildren<Text>(true);
            if (texts.Length > 0 && texts[0] != null)
            {
                texts[0].text = title;
                texts[0].fontSize = 22;
                texts[0].fontStyle = FontStyle.Normal;
                texts[0].color = TextPrimary;
            }
            if (texts.Length > 1 && texts[1] != null)
            {
                texts[1].text = "✏️ 이름 변경  |  🗑️ 공간 삭제";
                texts[1].fontSize = 18;
                texts[1].fontStyle = FontStyle.Normal;
                texts[1].color = AccentColor;
            }

            var btn = cardGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(onRename);
            }

            return cardGo;
        }

        private GameObject InstantiateDeviceWithDeleteCard(string title, string status, UnityEngine.Events.UnityAction onDelete)
        {
            var cardGo = Instantiate(templateCard, contentRoot);
            cardGo.SetActive(true);
            cardGo.name = $"Installed_{title}";

            var img = cardGo.GetComponent<Image>();
            if (img != null) img.color = new Color(0.11f, 0.13f, 0.18f, 0.95f);

            var texts = cardGo.GetComponentsInChildren<Text>(true);
            if (texts.Length > 0 && texts[0] != null)
            {
                texts[0].text = title;
                texts[0].fontSize = 20;
                texts[0].fontStyle = FontStyle.Normal;
                texts[0].color = TextPrimary;
            }
            if (texts.Length > 1 && texts[1] != null)
            {
                texts[1].text = $"{status}   [🗑️ 삭제]";
                texts[1].fontSize = 18;
                texts[1].fontStyle = FontStyle.Normal;
                texts[1].color = AccentGreen;
            }

            var btn = cardGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(onDelete);
            }

            return cardGo;
        }

        private GameObject InstantiateCard(
            string title,
            string sub,
            bool interactable,
            UnityEngine.Events.UnityAction onClick,
            Color? bgColor = null)
        {
            var cardGo = Instantiate(templateCard, contentRoot);
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
                subTxt.color = interactable ? (title.StartsWith("←") ? AccentColor : TextSecondary) : DisabledTextColor;
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
