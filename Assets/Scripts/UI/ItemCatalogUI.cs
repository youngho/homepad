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

        private RectTransform contentRoot;
        private RoomRecord focusedRoom = null;
        private bool isAddRoomMode = false;
        private bool isRenameMode = false;
        private string customInputName = "";
        private readonly List<GameObject> activeCards = new List<GameObject>();

        // Aesthetic Colors
        private static readonly Color BgCardColor = new Color(0.14f, 0.16f, 0.22f, 0.95f);
        private static readonly Color BgCardHover = new Color(0.20f, 0.25f, 0.35f, 1.0f);
        private static readonly Color AccentColor = new Color(0.25f, 0.65f, 1.0f, 1.0f);
        private static readonly Color AccentGreen = new Color(0.25f, 0.85f, 0.55f, 1.0f);
        private static readonly Color AccentAmber = new Color(1.0f, 0.65f, 0.20f, 1.0f);
        private static readonly Color TextPrimary = new Color(0.95f, 0.96f, 0.98f, 1.0f);
        private static readonly Color TextSecondary = new Color(0.65f, 0.72f, 0.82f, 1.0f);
        private static readonly Color DisabledColor = new Color(0.10f, 0.11f, 0.14f, 0.6f);

        private void Awake()
        {
            FindContentRoot();
            if (captionText == null)
            {
                var captionTrans = transform.Find("Caption");
                if (captionTrans != null) captionText = captionTrans.GetComponent<Text>();
            }
        }

        private void OnEnable()
        {
            Subscribe(true);
            isAddRoomMode = false;
            isRenameMode = false;
            EnsureFocusedRoom();
            BuildUI();
        }

        private void Start()
        {
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
                    layout.spacing = 8f;
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

        private void ClearCards()
        {
            for (int i = 0; i < activeCards.Count; i++)
            {
                if (activeCards[i] != null) Destroy(activeCards[i]);
            }
            activeCards.Clear();

            if (contentRoot != null)
            {
                for (int i = contentRoot.childCount - 1; i >= 0; i--)
                {
                    var child = contentRoot.GetChild(i);
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
            FindContentRoot();
            if (contentRoot == null) return;

            ClearCards();

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

            // A. Room Selector Chips
            if (layout != null && layout.Rooms.Count > 0)
            {
                BuildRoomChipsBar(layout);
            }

            if (focusedRoom == null)
            {
                // No room yet
                var emptyCard = CreateInfoCard("아직 생성된 공간이 없습니다.", "아래 버튼을 눌러 첫 번째 공간(거실, 방)을 만들어보세요.");
                activeCards.Add(emptyCard);

                var addBtn = CreateCardButton("➕  새로운 공간 만들기", "거실, 안방, 서재 등 추가", true, () =>
                {
                    isAddRoomMode = true;
                    BuildUI();
                }, AccentColor);
                activeCards.Add(addBtn);
                return;
            }

            // B. Focused Room Header Card
            string roomEmoji = HomeItemDef.RoomEmoji(focusedRoom.Hint);
            string roomTitle = $"{roomEmoji}  {focusedRoom.Name}";
            var roomHeader = CreateHeaderCard(roomTitle, "공간 이름 변경 및 설정", () =>
            {
                customInputName = focusedRoom.Name;
                isRenameMode = true;
                BuildUI();
            });
            activeCards.Add(roomHeader);

            // C. Section Header: Installed Devices in this room
            var installedItems = GetItemsInRoom(focusedRoom.Hint);
            if (installedItems.Count > 0)
            {
                var secLabel = CreateSectionTitle($"현재 설치된 기기 ({installedItems.Count}개)");
                activeCards.Add(secLabel);

                for (int i = 0; i < installedItems.Count; i++)
                {
                    var item = installedItems[i];
                    string itemEmoji = HomeItemDef.CategoryRules.TryGetValue(item.Kind, out var r) ? r.Emoji : "📦";
                    string status = item.Kind == HomeItemKind.Light ? "💡 조명 (클릭하여 제어)" : $"{item.Surface} 설치됨";
                    var devCard = CreateInstalledDeviceCard($"{itemEmoji}  {item.DisplayName}", status);
                    activeCards.Add(devCard);
                }
            }

            // D. Section Header: Add Device to this room
            var addSecLabel = CreateSectionTitle("➕  이 공간에 장치 추가하기");
            activeCards.Add(addSecLabel);

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

                var addDevBtn = CreateCardButton(title, sub, !blocked, () =>
                {
                    AddDeviceToFocusedRoom(kind);
                }, blocked ? DisabledColor : BgCardColor);
                activeCards.Add(addDevBtn);
            }

            // E. Add Another Room Button
            var addOtherRoomBtn = CreateCardButton("➕  새로운 공간 추가하기", "서재, 아이방, 드레스룸 등 다른 방 만들기", true, () =>
            {
                isAddRoomMode = true;
                BuildUI();
            }, new Color(0.18f, 0.22f, 0.30f));
            activeCards.Add(addOtherRoomBtn);
        }

        private void BuildRoomChipsBar(HomeLayout layout)
        {
            var chipsContainer = new GameObject("RoomChipsBar");
            chipsContainer.transform.SetParent(contentRoot, false);
            var rect = chipsContainer.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 44f);

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

                var chipBtnGo = new GameObject($"Chip_{room.Name}");
                chipBtnGo.transform.SetParent(chipsContainer.transform, false);

                var chipRect = chipBtnGo.AddComponent<RectTransform>();
                chipRect.sizeDelta = new Vector2(92f, 38f);

                var chipImg = chipBtnGo.AddComponent<Image>();
                chipImg.color = isSelected ? AccentColor : new Color(0.18f, 0.20f, 0.26f, 0.95f);

                var chipBtn = chipBtnGo.AddComponent<Button>();
                chipBtn.targetGraphic = chipImg;
                var capturedRoom = room;
                chipBtn.onClick.AddListener(() =>
                {
                    focusedRoom = capturedRoom;
                    BuildUI();
                });

                var txtGo = new GameObject("Label");
                txtGo.transform.SetParent(chipBtnGo.transform, false);
                var txtRect = txtGo.AddComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;

                var txt = txtGo.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.text = chipText;
                txt.fontSize = 12;
                txt.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
                txt.color = isSelected ? Color.white : TextSecondary;
                txt.alignment = TextAnchor.MiddleCenter;
            }

            activeCards.Add(chipsContainer);
        }

        // ==========================================
        // 2. Add New Room View (Preset Chips + Custom Input)
        // ==========================================
        private void BuildAddRoomModeUI()
        {
            if (captionText != null) captionText.text = "새로운 공간(방) 추가";

            // Back Button
            var backBtn = CreateCardButton("← 돌아가기", "공간 및 기기 관리 목록으로", true, () =>
            {
                isAddRoomMode = false;
                BuildUI();
            }, AccentColor);
            activeCards.Add(backBtn);

            // Section: Recommended Presets
            var presetSec = CreateSectionTitle("추천 공간 프리셋 (원터치 1초 생성)");
            activeCards.Add(presetSec);

            var presets = RoomPreset.RecommendedPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var presetBtn = CreateCardButton($"{preset.Emoji}  {preset.DefaultName}", "클릭 즉시 3D 디오라마에 공간 생성", true, () =>
                {
                    CreateRoomWithPreset(preset.Hint, preset.DefaultName);
                }, BgCardColor);
                activeCards.Add(presetBtn);
            }
        }

        // ==========================================
        // 3. Rename Room Mode
        // ==========================================
        private void BuildRenameRoomModeUI()
        {
            if (captionText != null) captionText.text = $"[{focusedRoom.Name}] 이름 변경";

            var backBtn = CreateCardButton("← 취소 및 돌아가기", "이름 변경을 취소합니다", true, () =>
            {
                isRenameMode = false;
                BuildUI();
            }, AccentColor);
            activeCards.Add(backBtn);

            var renamePresets = new[] { "서재", "아이방", "드레스룸", "알파룸", "홈카페", "게임룸", "작업실", "게스트룸", "냥이방" };
            var secLabel = CreateSectionTitle("추천 이름 선택");
            activeCards.Add(secLabel);

            for (int i = 0; i < renamePresets.Length; i++)
            {
                string rName = renamePresets[i];
                var nameBtn = CreateCardButton($"✏️  '{rName}'(으)로 변경", "공간 및 기기 표시명 즉시 갱신", true, () =>
                {
                    ApplyRename(rName);
                }, BgCardColor);
                activeCards.Add(nameBtn);
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
        // UI Helpers & Component Factories
        // ==========================================
        private GameObject CreateHeaderCard(string title, string sub, UnityEngine.Events.UnityAction onClick)
        {
            var card = CreateCardButton(title, sub, true, onClick, new Color(0.18f, 0.22f, 0.32f, 1.0f));
            return card;
        }

        private GameObject CreateSectionTitle(string text)
        {
            var go = new GameObject($"Sec_{text}");
            go.transform.SetParent(contentRoot, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 28f);

            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = text;
            txt.fontSize = 13;
            txt.fontStyle = FontStyle.Bold;
            txt.color = TextSecondary;
            txt.alignment = TextAnchor.MiddleLeft;
            return go;
        }

        private GameObject CreateInstalledDeviceCard(string title, string status)
        {
            var go = new GameObject($"Installed_{title}");
            go.transform.SetParent(contentRoot, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 52f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.11f, 0.13f, 0.18f, 0.9f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 4f);
            textRect.offsetMax = new Vector2(-16f, -4f);

            var vGroup = textGo.AddComponent<VerticalLayoutGroup>();
            vGroup.childAlignment = TextAnchor.MiddleLeft;
            vGroup.childControlHeight = true;
            vGroup.childControlWidth = true;
            vGroup.spacing = 2f;

            var t1 = new GameObject("T1").AddComponent<Text>();
            t1.transform.SetParent(textGo.transform, false);
            t1.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t1.text = title;
            t1.fontSize = 15;
            t1.fontStyle = FontStyle.Bold;
            t1.color = TextPrimary;

            var t2 = new GameObject("T2").AddComponent<Text>();
            t2.transform.SetParent(textGo.transform, false);
            t2.font = t1.font;
            t2.text = status;
            t2.fontSize = 11;
            t2.color = AccentGreen;

            return go;
        }

        private GameObject CreateInfoCard(string title, string sub)
        {
            var go = new GameObject("InfoCard");
            go.transform.SetParent(contentRoot, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 60f);

            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = $"{title}\n{sub}";
            txt.fontSize = 13;
            txt.color = TextSecondary;
            txt.alignment = TextAnchor.MiddleCenter;
            return go;
        }

        private GameObject CreateCardButton(string mainText, string subText, bool interactable, UnityEngine.Events.UnityAction onClick, Color? bgColor = null)
        {
            var btnGo = new GameObject($"Btn_{mainText}");
            btnGo.transform.SetParent(contentRoot, false);

            var rect = btnGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 64f);

            Color bg = bgColor ?? (interactable ? BgCardColor : DisabledColor);
            var img = btnGo.AddComponent<Image>();
            img.color = bg;

            var btn = btnGo.AddComponent<Button>();
            btn.interactable = interactable;
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = bg;
            colors.highlightedColor = BgCardHover;
            colors.pressedColor = AccentColor * 0.75f;
            colors.selectedColor = bg;
            colors.disabledColor = DisabledColor;
            btn.colors = colors;

            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Texts");
            textGo.transform.SetParent(btnGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 6f);
            textRect.offsetMax = new Vector2(-16f, -6f);

            var vGroup = textGo.AddComponent<VerticalLayoutGroup>();
            vGroup.childAlignment = TextAnchor.MiddleLeft;
            vGroup.childControlHeight = true;
            vGroup.childControlWidth = true;
            vGroup.childForceExpandHeight = false;
            vGroup.childForceExpandWidth = true;
            vGroup.spacing = 2f;

            var mainLabelGo = new GameObject("MainLabel");
            mainLabelGo.transform.SetParent(textGo.transform, false);
            var mainTxt = mainLabelGo.AddComponent<Text>();
            mainTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            mainTxt.text = mainText;
            mainTxt.fontSize = 16;
            mainTxt.fontStyle = FontStyle.Bold;
            mainTxt.color = interactable ? TextPrimary : new Color(0.5f, 0.55f, 0.62f, 0.5f);
            mainTxt.alignment = TextAnchor.MiddleLeft;

            var subLabelGo = new GameObject("SubLabel");
            subLabelGo.transform.SetParent(textGo.transform, false);
            var subTxt = subLabelGo.AddComponent<Text>();
            subTxt.font = mainTxt.font;
            subTxt.text = subText;
            subTxt.fontSize = 11;
            subTxt.color = interactable ? (mainText.StartsWith("←") ? AccentColor : TextSecondary) : new Color(0.45f, 0.50f, 0.58f, 0.4f);
            subTxt.alignment = TextAnchor.MiddleLeft;

            return btnGo;
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
