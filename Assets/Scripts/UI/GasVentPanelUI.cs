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

        [SerializeField] private Text gasStatus;
        [SerializeField] private Image gasIndicator;
        [SerializeField] private Button gasCloseButton;
        [SerializeField] private Text ventText;
        [SerializeField] private Button offButton;
        [SerializeField] private Button lowButton;
        [SerializeField] private Button medButton;
        [SerializeField] private Button highButton;

        public void Bind(Text gasStatusText, Image indicator, Button closeButton, Text ventilationText, Button off, Button low, Button med, Button high)
        {
            gasStatus = gasStatusText;
            gasIndicator = indicator;
            gasCloseButton = closeButton;
            ventText = ventilationText;
            offButton = off;
            lowButton = low;
            medButton = med;
            highButton = high;
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        private void Start()
        {
            Bind(gasCloseButton, () => WallpadManager.Instance.CloseGasValve());
            Bind(offButton, () => WallpadManager.Instance.SetVentilationSpeed(VentilationSpeed.Off));
            Bind(lowButton, () => WallpadManager.Instance.SetVentilationSpeed(VentilationSpeed.Low));
            Bind(medButton, () => WallpadManager.Instance.SetVentilationSpeed(VentilationSpeed.Medium));
            Bind(highButton, () => WallpadManager.Instance.SetVentilationSpeed(VentilationSpeed.High));

            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnStateChanged += RefreshAll;
            }

            RefreshAll();
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

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null) image.color = active ? Active : Idle;
        }
    }
}
