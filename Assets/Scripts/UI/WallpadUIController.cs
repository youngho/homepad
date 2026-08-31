using System;
using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public enum WallpadTab
    {
        Dashboard,
        Lighting,
        Heating,
        GasVent,
        Elevator,
        Settings
    }

    public class WallpadUIController : MonoBehaviour
    {
        private static readonly Color TabIdle = new Color(0.10f, 0.12f, 0.16f, 1f);
        private static readonly Color TabActive = new Color(0.18f, 0.45f, 0.90f, 1f);
        private static readonly Color Ok = new Color(0.20f, 0.62f, 0.38f, 1f);

        [SerializeField] private GameObject[] panels;
        [SerializeField] private Image[] tabImages;
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private Text summaryLight;
        [SerializeField] private Text summaryTemp;
        [SerializeField] private Text summaryGas;
        [SerializeField] private Text summaryVent;
        [SerializeField] private Text awayButtonText;
        [SerializeField] private Image wifiDot;
        [SerializeField] private Text wifiText;
        [SerializeField] private Button allOffButton;
        [SerializeField] private Button gasCloseButton;
        [SerializeField] private Button awayButton;
        [SerializeField] private Button elevatorButton;

        private WallpadTab currentTab = WallpadTab.Dashboard;
        private Action<bool> awayHandler;

        private void Start()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                if (tabButtons[i] == null) continue;
                tabButtons[i].onClick.RemoveAllListeners();
                tabButtons[i].onClick.AddListener(() => SwitchTab((WallpadTab)index));
            }

            Bind(allOffButton, () => WallpadManager.Instance.TurnOffAllLights());
            Bind(gasCloseButton, () => WallpadManager.Instance.CloseGasValve());
            Bind(awayButton, () => WallpadManager.Instance.ToggleAwayMode());
            Bind(elevatorButton, () => WallpadManager.Instance.CallElevator());

            Subscribe();
            SwitchTab(WallpadTab.Dashboard);
            RefreshDashboard();
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance == null) return;
            WallpadManager.Instance.OnStateChanged -= RefreshDashboard;
            if (awayHandler != null)
            {
                WallpadManager.Instance.OnAwayModeChanged -= awayHandler;
            }

            if (WallpadManager.Instance.Connector != null)
            {
                WallpadManager.Instance.Connector.OnConnectionStatusChanged -= UpdateWifi;
            }
        }

        public void SwitchTab(WallpadTab newTab)
        {
            currentTab = newTab;
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null) panels[i].SetActive(i == (int)newTab);
            }

            for (int i = 0; i < tabImages.Length; i++)
            {
                if (tabImages[i] != null) tabImages[i].color = i == (int)newTab ? TabActive : TabIdle;
            }
        }

        private void Subscribe()
        {
            if (WallpadManager.Instance == null) return;

            WallpadManager.Instance.OnStateChanged += RefreshDashboard;
            awayHandler = _ => RefreshDashboard();
            WallpadManager.Instance.OnAwayModeChanged += awayHandler;

            if (WallpadManager.Instance.Connector != null)
            {
                WallpadManager.Instance.Connector.OnConnectionStatusChanged += UpdateWifi;
                UpdateWifi(WallpadManager.Instance.Connector.IsConnected);
            }
        }

        private void RefreshDashboard()
        {
            if (WallpadManager.Instance == null) return;

            int lightsOn = 0;
            foreach (var light in WallpadManager.Instance.Lights)
            {
                if (light.isOn) lightsOn++;
            }

            if (summaryLight != null)
            {
                summaryLight.text = $"{lightsOn} / {WallpadManager.Instance.Lights.Count} 켜짐";
            }

            float sum = 0;
            var rooms = WallpadManager.Instance.HeatingRooms;
            foreach (var room in rooms) sum += room.currentTemp;
            float avg = rooms.Count > 0 ? sum / rooms.Count : 22f;
            if (summaryTemp != null) summaryTemp.text = $"{avg:F1}℃";

            if (summaryGas != null)
            {
                summaryGas.text = WallpadManager.Instance.Gas.isOpen ? "열림 (주의)" : "안전 잠금";
            }

            if (summaryVent != null)
            {
                var vent = WallpadManager.Instance.Ventilation;
                summaryVent.text = vent.isPowered ? $"가동 ({SpeedName(vent.speed)})" : "정지";
            }

            if (awayButtonText != null)
            {
                awayButtonText.text = WallpadManager.Instance.IsAwayMode ? "외출 모드 ON" : "외출 모드 OFF";
            }
        }

        private void UpdateWifi(bool isConnected)
        {
            bool sim = WallpadManager.Instance != null && WallpadManager.Instance.Connector != null && WallpadManager.Instance.Connector.UseSimulationMode;
            Color color = isConnected ? Ok : new Color(0.9f, 0.3f, 0.3f);
            if (wifiDot != null) wifiDot.color = color;
            if (wifiText != null)
            {
                wifiText.color = color;
                wifiText.text = !isConnected ? "OFFLINE" : (sim ? "시뮬레이션" : "RS485 ONLINE");
            }
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static string SpeedName(VentilationSpeed speed)
        {
            return speed switch
            {
                VentilationSpeed.Low => "미풍",
                VentilationSpeed.Medium => "약풍",
                VentilationSpeed.High => "강풍",
                _ => "정지"
            };
        }
    }
}
