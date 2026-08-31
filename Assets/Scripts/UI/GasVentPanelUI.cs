using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class GasVentPanelUI : MonoBehaviour
    {
        private static readonly Color OpenColor = new Color(1f, 0.4f, 0.3f);
        private static readonly Color ClosedColor = new Color(0.3f, 0.8f, 0.4f);
        private static readonly Color Active = new Color(0.2f, 0.5f, 0.9f);
        private static readonly Color Idle = new Color(0.18f, 0.2f, 0.26f);

        private Text gasStatus;
        private Image gasIndicator;
        private Text ventText;
        private Button offButton;
        private Button lowButton;
        private Button medButton;
        private Button highButton;

        public void Build()
        {
            var root = GetComponent<RectTransform>();

            var gasCard = UiFactory.Create("Gas", root, new Vector2(0, 0.52f), Vector2.one, Vector2.zero, Vector2.zero);
            UiFactory.AddImage(gasCard, new Color(0.13f, 0.16f, 0.22f), false);
            UiFactory.CreateLabel("Title", gasCard, new Vector2(0, 0.7f), Vector2.one, new Vector2(28, 0), new Vector2(-28, -16), "가스 밸브", 30, Color.white, TextAnchor.MiddleLeft);
            gasStatus = UiFactory.CreateLabel("Status", gasCard, new Vector2(0, 0.35f), new Vector2(0.7f, 0.7f), new Vector2(28, 0), Vector2.zero, "안전 잠금 상태", 28, ClosedColor, TextAnchor.MiddleLeft);
            var indicatorRect = UiFactory.Create("Indicator", gasCard, new Vector2(0.82f, 0.45f), new Vector2(0.82f, 0.45f), Vector2.zero, Vector2.zero);
            indicatorRect.sizeDelta = new Vector2(28, 28);
            gasIndicator = UiFactory.AddImage(indicatorRect, ClosedColor, false);
            var close = UiFactory.CreateButton("Close", gasCard, new Vector2(0.04f, 0.08f), new Vector2(0.4f, 0.32f), Vector2.zero, Vector2.zero, "가스 잠금", new Color(0.78f, 0.28f, 0.24f), 26);
            close.onClick.AddListener(() => WallpadManager.Instance.CloseGasValve());

            var ventCard = UiFactory.Create("Vent", root, Vector2.zero, new Vector2(1, 0.48f), Vector2.zero, Vector2.zero);
            UiFactory.AddImage(ventCard, new Color(0.13f, 0.16f, 0.22f), false);
            UiFactory.CreateLabel("Title", ventCard, new Vector2(0, 0.7f), Vector2.one, new Vector2(28, 0), new Vector2(-28, -12), "환기", 30, Color.white, TextAnchor.MiddleLeft);
            ventText = UiFactory.CreateLabel("Speed", ventCard, new Vector2(0, 0.42f), Vector2.one, new Vector2(28, 0), new Vector2(-28, 0), "현재 풍량: 정지", 24, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft);

            offButton = MakeVentButton(ventCard, "Off", "정지", 0, VentilationSpeed.Off);
            lowButton = MakeVentButton(ventCard, "Low", "미풍", 1, VentilationSpeed.Low);
            medButton = MakeVentButton(ventCard, "Med", "약풍", 2, VentilationSpeed.Medium);
            highButton = MakeVentButton(ventCard, "High", "강풍", 3, VentilationSpeed.High);

            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnStateChanged += RefreshAll;
            }

            RefreshAll();
        }

        private Button MakeVentButton(RectTransform parent, string name, string label, int index, VentilationSpeed speed)
        {
            float x0 = 0.04f + index * 0.24f;
            var button = UiFactory.CreateButton(name, parent, new Vector2(x0, 0.08f), new Vector2(x0 + 0.22f, 0.36f), Vector2.zero, Vector2.zero, label, Idle, 24);
            button.onClick.AddListener(() => WallpadManager.Instance.SetVentilationSpeed(speed));
            return button;
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance == null) return;
            WallpadManager.Instance.OnStateChanged -= RefreshAll;
        }

        public void RefreshAll()
        {
            if (WallpadManager.Instance == null) return;
            var gas = WallpadManager.Instance.Gas;
            if (gasStatus != null)
            {
                gasStatus.text = gas.isOpen ? "열림 (주의)" : "안전 잠금 상태";
                gasStatus.color = gas.isOpen ? OpenColor : ClosedColor;
            }

            if (gasIndicator != null)
            {
                gasIndicator.color = gas.isOpen ? OpenColor : ClosedColor;
            }

            var vent = WallpadManager.Instance.Ventilation;
            if (ventText != null)
            {
                string speedName = vent.speed switch
                {
                    VentilationSpeed.Low => "미풍 (1단)",
                    VentilationSpeed.Medium => "약풍 (2단)",
                    VentilationSpeed.High => "강풍 (3단)",
                    _ => "정지"
                };
                ventText.text = $"현재 풍량: {speedName}";
            }

            SetActive(offButton, vent.speed == VentilationSpeed.Off);
            SetActive(lowButton, vent.speed == VentilationSpeed.Low);
            SetActive(medButton, vent.speed == VentilationSpeed.Medium);
            SetActive(highButton, vent.speed == VentilationSpeed.High);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null) image.color = active ? Active : Idle;
        }
    }
}
