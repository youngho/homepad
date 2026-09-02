using System.Collections.Generic;
using Homepad.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class ItemCatalogUI : MonoBehaviour
    {
        [SerializeField] private CatalogDrawerUI drawer;
        [SerializeField] private Text captionText;

        private RectTransform contentRoot;
        private HomeItemKind? selectedKind = null;
        private readonly List<GameObject> activeButtons = new List<GameObject>();

        // Modern Palette
        private static readonly Color BgCardColor = new Color(0.14f, 0.16f, 0.22f, 0.95f);
        private static readonly Color BgCardHover = new Color(0.20f, 0.24f, 0.32f, 1.0f);
        private static readonly Color AccentColor = new Color(0.25f, 0.65f, 1.0f, 1.0f);
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
            selectedKind = null;
            BuildUI();
        }

        private void Start()
        {
            Subscribe(true);
            BuildUI();
        }

        private void OnDisable()
        {
            var home = HomeController.Instance;
            if (home != null) home.LayoutChanged -= Refresh;
        }

        private void Subscribe(bool on)
        {
            var home = HomeController.Instance;
            if (on && home == null) home = HomeController.EnsureExists();
            if (home == null) return;
            home.LayoutChanged -= Refresh;
            if (on) home.LayoutChanged += Refresh;
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

        public void Refresh()
        {
            BuildUI();
        }

        private void ClearButtons()
        {
            for (int i = 0; i < activeButtons.Count; i++)
            {
                if (activeButtons[i] != null) Destroy(activeButtons[i]);
            }
            activeButtons.Clear();

            // Clear legacy baked buttons if present
            if (contentRoot != null)
            {
                for (int i = contentRoot.childCount - 1; i >= 0; i--)
                {
                    var child = contentRoot.GetChild(i);
                    Destroy(child.gameObject);
                }
            }
        }

        private void BuildUI()
        {
            FindContentRoot();
            if (contentRoot == null) return;

            ClearButtons();

            if (!selectedKind.HasValue)
            {
                // Step 1: Device Category Selection
                if (captionText != null) captionText.text = "장치 추가 (기기 선택)";
                BuildCategoryCards();
            }
            else
            {
                // Step 2: Target Room Selection
                var rule = HomeItemDef.CategoryRules[selectedKind.Value];
                if (captionText != null) captionText.text = $"[{rule.CategoryName}] 설치할 방 선택";
                BuildRoomSelection(selectedKind.Value, rule);
            }
        }

        private void BuildCategoryCards()
        {
            var categories = new[]
            {
                (HomeItemKind.Light, "💡  조명", "천장 앵커링 / 무드 조명"),
                (HomeItemKind.Heating, "🔥  난방", "벽면 조절기 / 온돌 난방"),
                (HomeItemKind.ElectricCurtain, "🪟  전동커튼", "창문 마운트 / 3D 주름 원단"),
                (HomeItemKind.AirConditioner, "❄️  에어컨", "천장형 시스템 에어컨"),
                (HomeItemKind.Vent, "🌀  환기", "천장 환기 청정 시스템"),
                (HomeItemKind.Gas, "🛡️  가스 밸브", "주방 안전 자동 차단기"),
                (HomeItemKind.Elevator, "🛗  엘리베이터", "현관 사전 호출 제어")
            };

            for (int i = 0; i < categories.Length; i++)
            {
                var item = categories[i];
                var card = CreateCardButton(item.Item2, item.Item3, true, () =>
                {
                    selectedKind = item.Item1;
                    BuildUI();
                });
                activeButtons.Add(card);
            }
        }

        private void BuildRoomSelection(HomeItemKind kind, DeviceCategoryRule rule)
        {
            // Back Button
            var backBtn = CreateCardButton("← 다른 장치 선택", "기기 목록으로 돌아가기", true, () =>
            {
                selectedKind = null;
                BuildUI();
            });
            activeButtons.Add(backBtn);

            var home = HomeController.Instance;
            var layout = home != null ? home.Layout : null;

            for (int i = 0; i < rule.DefaultAllowedRooms.Length; i++)
            {
                var room = rule.DefaultAllowedRooms[i];
                var def = HomeItemDef.Create(kind, room);
                bool blocked = layout != null && layout.IsCatalogBlocked(def);

                string title = $"{HomeItemDef.RoomName(room)} {rule.CategoryName}";
                string sub = blocked ? "✓ 이미 설치됨" : $"설치 표면: {GetSurfaceDesc(def.Surface)}";

                var roomBtn = CreateCardButton(title, sub, !blocked, () =>
                {
                    AddDeviceAndNotify(kind, room);
                });
                activeButtons.Add(roomBtn);
            }
        }

        private void AddDeviceAndNotify(HomeItemKind kind, RoomHint room)
        {
            var home = HomeController.EnsureExists();
            if (home == null) return;

            var def = HomeItemDef.Create(kind, room);
            bool success = home.PlaceFromCatalog(def);
            if (success)
            {
                home.Save();
                home.FrameCamera();
                // Refresh list
                BuildUI();
            }
        }

        private GameObject CreateCardButton(string mainText, string subText, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            var btnGo = new GameObject($"Btn_{mainText}");
            btnGo.transform.SetParent(contentRoot, false);

            var rect = btnGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 68f);

            var img = btnGo.AddComponent<Image>();
            img.color = interactable ? BgCardColor : DisabledColor;

            var btn = btnGo.AddComponent<Button>();
            btn.interactable = interactable;
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = interactable ? BgCardColor : DisabledColor;
            colors.highlightedColor = BgCardHover;
            colors.pressedColor = AccentColor * 0.75f;
            colors.selectedColor = BgCardColor;
            colors.disabledColor = DisabledColor;
            btn.colors = colors;

            btn.onClick.AddListener(onClick);

            // Container for text
            var textGo = new GameObject("Texts");
            textGo.transform.SetParent(btnGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 6f);
            textRect.offsetMax = new Vector2(-18f, -6f);

            var vGroup = textGo.AddComponent<VerticalLayoutGroup>();
            vGroup.childAlignment = TextAnchor.MiddleLeft;
            vGroup.childControlHeight = true;
            vGroup.childControlWidth = true;
            vGroup.childForceExpandHeight = false;
            vGroup.childForceExpandWidth = true;
            vGroup.spacing = 2f;

            // Main Label
            var mainLabelGo = new GameObject("MainLabel");
            mainLabelGo.transform.SetParent(textGo.transform, false);
            var mainTxt = mainLabelGo.AddComponent<Text>();
            mainTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            mainTxt.text = mainText;
            mainTxt.fontSize = 17;
            mainTxt.fontStyle = FontStyle.Bold;
            mainTxt.color = interactable ? TextPrimary : new Color(0.5f, 0.55f, 0.62f, 0.5f);
            mainTxt.alignment = TextAnchor.MiddleLeft;

            // Sub Label
            var subLabelGo = new GameObject("SubLabel");
            subLabelGo.transform.SetParent(textGo.transform, false);
            var subTxt = subLabelGo.AddComponent<Text>();
            subTxt.font = mainTxt.font;
            subTxt.text = subText;
            subTxt.fontSize = 12;
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
