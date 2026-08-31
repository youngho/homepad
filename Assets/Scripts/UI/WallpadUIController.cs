using System;
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

    public class WallpadUIController : MonoBehaviour
    {
        private static readonly Color Bg = new Color(0.082f, 0.098f, 0.133f, 1f);
        private static readonly Color TopBar = new Color(0.10f, 0.12f, 0.17f, 1f);
        private static readonly Color TabIdle = new Color(0.10f, 0.12f, 0.16f, 1f);
        private static readonly Color TabActive = new Color(0.18f, 0.45f, 0.90f, 1f);
        private static readonly Color Card = new Color(0.13f, 0.16f, 0.22f, 1f);
        private static readonly Color Danger = new Color(0.78f, 0.28f, 0.24f, 1f);
        private static readonly Color Ok = new Color(0.20f, 0.62f, 0.38f, 1f);

        private readonly Dictionary<WallpadTab, GameObject> panels = new Dictionary<WallpadTab, GameObject>();
        private readonly Dictionary<WallpadTab, Image> tabImages = new Dictionary<WallpadTab, Image>();
        private WallpadTab currentTab = WallpadTab.Dashboard;

        private Text summaryLight;
        private Text summaryTemp;
        private Text summaryGas;
        private Text summaryVent;
        private Text awayButtonText;
        private Image wifiDot;
        private Text wifiText;
        private Action<bool> awayHandler;

        private void Start()
        {
            Build();
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

        private void Build()
        {
            var root = GetComponent<RectTransform>();
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }

            UiFactory.AddImage(root, Bg);

            var top = UiFactory.Create("TopBar", root, new Vector2(0, 1), Vector2.one, new Vector2(0, -88), Vector2.zero);
            UiFactory.AddImage(top, TopBar, false);
            UiFactory.CreateLabel("Title", top, new Vector2(0, 0), new Vector2(0.28f, 1), new Vector2(32, 0), Vector2.zero, "홈패드", 36, Color.white, TextAnchor.MiddleLeft);

            var clockGo = UiFactory.Create("Clock", top, new Vector2(0.28f, 0), new Vector2(0.72f, 1), Vector2.zero, Vector2.zero);
            var timeText = UiFactory.CreateLabel("Time", clockGo, new Vector2(0, 0.48f), Vector2.one, Vector2.zero, Vector2.zero, "00:00:00", 34, Color.white, TextAnchor.MiddleCenter);
            var dateText = UiFactory.CreateLabel("Date", clockGo, Vector2.zero, new Vector2(1, 0.46f), Vector2.zero, Vector2.zero, "", 20, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            var clock = clockGo.gameObject.AddComponent<ClockDisplay>();
            clock.Bind(timeText, dateText);

            wifiDot = UiFactory.AddImage(
                UiFactory.Create("WifiDot", top, new Vector2(0.90f, 0.5f), new Vector2(0.90f, 0.5f), Vector2.zero, Vector2.zero),
                Ok,
                false);
            wifiDot.rectTransform.sizeDelta = new Vector2(16, 16);
            wifiText = UiFactory.CreateLabel("WifiText", top, new Vector2(0.72f, 0), new Vector2(0.98f, 1), new Vector2(8, 0), new Vector2(-16, 0), "시뮬레이션", 22, Ok, TextAnchor.MiddleRight);

            var tabs = UiFactory.Create("TabBar", root, new Vector2(0, 1), Vector2.one, new Vector2(16, -176), new Vector2(-16, -96));
            CreateTab(tabs, WallpadTab.Dashboard, "홈", 0);
            CreateTab(tabs, WallpadTab.Lighting, "조명", 1);
            CreateTab(tabs, WallpadTab.Heating, "난방", 2);
            CreateTab(tabs, WallpadTab.GasVent, "가스/환기", 3);
            CreateTab(tabs, WallpadTab.Elevator, "엘리베이터", 4);
            CreateTab(tabs, WallpadTab.Settings, "설정", 5);

            var content = UiFactory.Create("Content", root, Vector2.zero, Vector2.one, new Vector2(24, 24), new Vector2(-24, -192));
            panels[WallpadTab.Dashboard] = BuildDashboard(content).gameObject;
            panels[WallpadTab.Lighting] = CreatePanel(content, "LightingPanel", go => go.AddComponent<LightingPanelUI>().Build());
            panels[WallpadTab.Heating] = CreatePanel(content, "HeatingPanel", go => go.AddComponent<HeatingPanelUI>().Build());
            panels[WallpadTab.GasVent] = CreatePanel(content, "GasVentPanel", go => go.AddComponent<GasVentPanelUI>().Build());
            panels[WallpadTab.Elevator] = CreatePanel(content, "ElevatorPanel", go => go.AddComponent<ElevatorPanelUI>().Build());
            panels[WallpadTab.Settings] = CreatePanel(content, "SettingsPanel", go => go.AddComponent<NetworkSettingsUI>().Build());
        }

        private void CreateTab(RectTransform parent, WallpadTab tab, string label, int index)
        {
            float width = 1f / 6f;
            var button = UiFactory.CreateButton(
                tab.ToString(),
                parent,
                new Vector2(index * width, 0),
                new Vector2((index + 1) * width, 1),
                new Vector2(4, 0),
                new Vector2(-4, 0),
                label,
                TabIdle,
                24);
            tabImages[tab] = button.GetComponent<Image>();
            button.onClick.AddListener(() => SwitchTab(tab));
        }

        private GameObject CreatePanel(RectTransform parent, string name, System.Action<GameObject> setup)
        {
            var rect = UiFactory.Create(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            setup(rect.gameObject);
            return rect.gameObject;
        }

        private RectTransform BuildDashboard(RectTransform parent)
        {
            var dash = UiFactory.Create("Dashboard", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            summaryLight = CreateSummaryCard(dash, "LightCard", "조명", "0 / 0 켜짐", 0);
            summaryTemp = CreateSummaryCard(dash, "TempCard", "평균 온도", "22.0℃", 1);
            summaryGas = CreateSummaryCard(dash, "GasCard", "가스", "안전 잠금", 2);
            summaryVent = CreateSummaryCard(dash, "VentCard", "환기", "정지", 3);

            var actions = UiFactory.Create("QuickActions", dash, Vector2.zero, new Vector2(1, 0.48f), Vector2.zero, Vector2.zero);
            var allOff = UiFactory.CreateButton("AllOff", actions, new Vector2(0, 0.52f), new Vector2(0.49f, 1), Vector2.zero, Vector2.zero, "일괄 소등", Card);
            allOff.onClick.AddListener(() => WallpadManager.Instance.TurnOffAllLights());

            var gas = UiFactory.CreateButton("GasClose", actions, new Vector2(0.51f, 0.52f), Vector2.one, Vector2.zero, Vector2.zero, "가스 잠금", Danger);
            gas.onClick.AddListener(() => WallpadManager.Instance.CloseGasValve());

            var away = UiFactory.CreateButton("Away", actions, new Vector2(0, 0), new Vector2(0.49f, 0.48f), Vector2.zero, Vector2.zero, "외출 모드 OFF", Card);
            awayButtonText = away.GetComponentInChildren<Text>();
            away.onClick.AddListener(() => WallpadManager.Instance.ToggleAwayMode());

            var elevator = UiFactory.CreateButton("Elevator", actions, new Vector2(0.51f, 0), new Vector2(1, 0.48f), Vector2.zero, Vector2.zero, "엘리베이터 호출", TabActive);
            elevator.onClick.AddListener(() => WallpadManager.Instance.CallElevator());

            return dash;
        }

        private Text CreateSummaryCard(RectTransform parent, string name, string title, string value, int index)
        {
            float x0 = (index % 4) * 0.25f;
            var card = UiFactory.Create(name, parent, new Vector2(x0, 0.56f), new Vector2(x0 + 0.24f, 1f), Vector2.zero, Vector2.zero);
            UiFactory.AddImage(card, Card, false);
            var titleText = UiFactory.CreateLabel("Title", card, new Vector2(0, 0.64f), Vector2.one, new Vector2(20, 8), new Vector2(-16, -8), title, 20, new Color(1, 1, 1, 0.65f), TextAnchor.LowerLeft);
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var valueText = UiFactory.CreateLabel("Value", card, Vector2.zero, new Vector2(1, 0.58f), new Vector2(20, 14), new Vector2(-16, 0), value, 28, Color.white, TextAnchor.UpperLeft);
            valueText.verticalOverflow = VerticalWrapMode.Truncate;
            valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            return valueText;
        }

        public void SwitchTab(WallpadTab newTab)
        {
            currentTab = newTab;
            foreach (var pair in panels)
            {
                pair.Value.SetActive(pair.Key == newTab);
            }

            foreach (var pair in tabImages)
            {
                pair.Value.color = pair.Key == newTab ? TabActive : TabIdle;
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
