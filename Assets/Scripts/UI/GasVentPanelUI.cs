using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    /// <summary>
    /// 가스 밸브 및 환기 제어 패널 UI
    /// </summary>
    public class GasVentPanelUI : MonoBehaviour
    {
        [Header("Gas Controls")]
        [SerializeField] private Button gasCloseButton;
        [SerializeField] private Text gasStatusText;
        [SerializeField] private Image gasStatusIndicator;

        [Header("Ventilation Controls")]
        [SerializeField] private Button ventOffButton;
        [SerializeField] private Button ventLowButton;
        [SerializeField] private Button ventMedButton;
        [SerializeField] private Button ventHighButton;
        [SerializeField] private Text ventSpeedText;

        [Header("Colors")]
        [SerializeField] private Color openColor = new Color(1f, 0.4f, 0.3f);
        [SerializeField] private Color closedColor = new Color(0.3f, 0.8f, 0.4f);
        [SerializeField] private Color activeBtnColor = new Color(0.2f, 0.5f, 0.9f);
        [SerializeField] private Color inactiveBtnColor = new Color(0.18f, 0.2f, 0.26f);

        private void Start()
        {
            if (gasCloseButton != null)
            {
                gasCloseButton.onClick.AddListener(() => WallpadManager.Instance.CloseGasValve());
            }

            if (ventOffButton != null)
                ventOffButton.onClick.AddListener(() => WallpadManager.Instance.SetVentilationSpeed(VentilationSpeed.Off));
            if (ventLowButton != null)
                ventLowButton.onClick.AddListener(() => WallpadManager.Instance.SetVentilationSpeed(VentilationSpeed.Low));
            if (ventMedButton != null)
                ventMedButton.onClick.AddListener(() => WallpadManager.Instance.SetVentilationSpeed(VentilationSpeed.Medium));
            if (ventHighButton != null)
                ventHighButton.onClick.AddListener(() => WallpadManager.Instance.SetVentilationSpeed(VentilationSpeed.High));

            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnGasChanged += OnGasChanged;
                WallpadManager.Instance.OnVentilationChanged += OnVentChanged;
                WallpadManager.Instance.OnStateChanged += RefreshAll;
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnGasChanged -= OnGasChanged;
                WallpadManager.Instance.OnVentilationChanged -= OnVentChanged;
                WallpadManager.Instance.OnStateChanged -= RefreshAll;
            }
        }

        private void OnGasChanged(GasState gas) => RefreshAll();
        private void OnVentChanged(VentilationState vent) => RefreshAll();

        public void RefreshAll()
        {
            if (WallpadManager.Instance == null) return;

            // Gas
            var gas = WallpadManager.Instance.Gas;
            if (gasStatusText != null)
            {
                gasStatusText.text = gas.isOpen ? "열림 (주의)" : "안전 잠금 상태";
                gasStatusText.color = gas.isOpen ? openColor : closedColor;
            }
            if (gasStatusIndicator != null)
            {
                gasStatusIndicator.color = gas.isOpen ? openColor : closedColor;
            }

            // Ventilation
            var vent = WallpadManager.Instance.Ventilation;
            if (ventSpeedText != null)
            {
                string speedName = vent.speed switch
                {
                    VentilationSpeed.Off => "정지",
                    VentilationSpeed.Low => "미풍 (1단)",
                    VentilationSpeed.Medium => "약풍 (2단)",
                    VentilationSpeed.High => "강풍 (3단)",
                    _ => "정지"
                };
                ventSpeedText.text = $"현재 풍량: {speedName}";
            }

            SetButtonActive(ventOffButton, vent.speed == VentilationSpeed.Off);
            SetButtonActive(ventLowButton, vent.speed == VentilationSpeed.Low);
            SetButtonActive(ventMedButton, vent.speed == VentilationSpeed.Medium);
            SetButtonActive(ventHighButton, vent.speed == VentilationSpeed.High);
        }

        private void SetButtonActive(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = active ? activeBtnColor : inactiveBtnColor;
            }
        }
    }
}
