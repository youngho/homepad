using System.Collections.Generic;
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

    /// <summary>
    /// 스마트 월패드 메인 UI 화면 및 탭 내비게이션 컨트롤러
    /// </summary>
    public class WallpadUIController : MonoBehaviour
    {
        [System.Serializable]
        public class TabMapping
        {
            public WallpadTab tab;
            public Button tabButton;
            public GameObject panelObject;
            public Image tabHighlight;
        }

        [Header("Tabs")]
        [SerializeField] private List<TabMapping> tabMappings = new List<TabMapping>();

        [Header("Dashboard Quick Actions")]
        [SerializeField] private Button quickAllOffButton;
        [SerializeField] private Button quickGasCloseButton;
        [SerializeField] private Button quickAwayButton;
        [SerializeField] private Text quickAwayButtonText;
        [SerializeField] private Button quickElevatorButton;

        [Header("Dashboard Status Summaries")]
        [SerializeField] private Text summaryLightCountText;
        [SerializeField] private Text summaryAvgTempText;
        [SerializeField] private Text summaryGasText;
        [SerializeField] private Text summaryVentText;

        [Header("Top Bar Quick Indicators")]
        [SerializeField] private Image topWifiStatusImg;
        [SerializeField] private Text topWifiStatusText;

        [Header("Colors")]
        [SerializeField] private Color tabActiveColor = new Color(0.18f, 0.45f, 0.9f);
        [SerializeField] private Color tabInactiveColor = new Color(0.1f, 0.12f, 0.16f);

        private WallpadTab currentTab = WallpadTab.Dashboard;

        private void Start()
        {
            // Bind tab buttons
            foreach (var mapping in tabMappings)
            {
                var tab = mapping.tab;
                if (mapping.tabButton != null)
                {
                    mapping.tabButton.onClick.AddListener(() => SwitchTab(tab));
                }
            }

            // Bind Quick Action Buttons
            if (quickAllOffButton != null)
                quickAllOffButton.onClick.AddListener(() => WallpadManager.Instance.TurnOffAllLights());

            if (quickGasCloseButton != null)
                quickGasCloseButton.onClick.AddListener(() => WallpadManager.Instance.CloseGasValve());

            if (quickAwayButton != null)
                quickAwayButton.onClick.AddListener(() => WallpadManager.Instance.ToggleAwayMode());

            if (quickElevatorButton != null)
                quickElevatorButton.onClick.AddListener(() => WallpadManager.Instance.CallElevator(12));

            // Subscribe to state changes
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnStateChanged += RefreshDashboardSummaries;
                WallpadManager.Instance.OnAwayModeChanged += (away) => RefreshDashboardSummaries();
                if (WallpadManager.Instance.Connector != null)
                {
                    WallpadManager.Instance.Connector.OnConnectionStatusChanged += UpdateTopWifiIndicator;
                    UpdateTopWifiIndicator(WallpadManager.Instance.Connector.IsConnected);
                }
            }

            SwitchTab(WallpadTab.Dashboard);
            RefreshDashboardSummaries();
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnStateChanged -= RefreshDashboardSummaries;
                if (WallpadManager.Instance.Connector != null)
                {
                    WallpadManager.Instance.Connector.OnConnectionStatusChanged -= UpdateTopWifiIndicator;
                }
            }
        }

        public void SwitchTab(WallpadTab newTab)
        {
            currentTab = newTab;

            foreach (var mapping in tabMappings)
            {
                bool isActive = mapping.tab == newTab;
                if (mapping.panelObject != null)
                {
                    mapping.panelObject.SetActive(isActive);
                }
                if (mapping.tabHighlight != null)
                {
                    mapping.tabHighlight.color = isActive ? tabActiveColor : tabInactiveColor;
                }
            }
        }

        private void RefreshDashboardSummaries()
        {
            if (WallpadManager.Instance == null) return;

            // Lights on count
            int lightsOn = 0;
            foreach (var l in WallpadManager.Instance.Lights)
            {
                if (l.isOn) lightsOn++;
            }
            if (summaryLightCountText != null)
            {
                summaryLightCountText.text = $"{lightsOn} / {WallpadManager.Instance.Lights.Count} 켜짐";
            }

            // Average temp
            float sumTemp = 0;
            var rooms = WallpadManager.Instance.HeatingRooms;
            foreach (var r in rooms) sumTemp += r.currentTemp;
            float avgTemp = rooms.Count > 0 ? sumTemp / rooms.Count : 22f;
            if (summaryAvgTempText != null)
            {
                summaryAvgTempText.text = $"평균 {avgTemp:F1}℃";
            }

            // Gas
            if (summaryGasText != null)
            {
                summaryGasText.text = WallpadManager.Instance.Gas.isOpen ? "가스 열림 (주의)" : "가스 안전 잠금";
            }

            // Vent
            if (summaryVentText != null)
            {
                var v = WallpadManager.Instance.Ventilation;
                summaryVentText.text = v.isPowered ? $"환기 가동 ({v.speed})" : "환기 정지";
            }

            // Away Mode button text
            if (quickAwayButtonText != null)
            {
                quickAwayButtonText.text = WallpadManager.Instance.IsAwayMode ? "외출 모드 ON" : "외출 모드 OFF";
            }
        }

        private void UpdateTopWifiIndicator(bool isConnected)
        {
            if (topWifiStatusImg != null)
            {
                topWifiStatusImg.color = isConnected ? new Color(0.2f, 0.85f, 0.4f) : new Color(0.9f, 0.3f, 0.3f);
            }
            if (topWifiStatusText != null)
            {
                topWifiStatusText.text = isConnected ? "WiFi RS485 ONLINE" : "OFFLINE";
                topWifiStatusText.color = isConnected ? new Color(0.2f, 0.85f, 0.4f) : new Color(0.9f, 0.3f, 0.3f);
            }
        }
    }
}
