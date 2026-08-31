using System.Text;
using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class NetworkSettingsUI : MonoBehaviour
    {
        private static readonly Color Connected = new Color(0.2f, 0.85f, 0.4f);
        private static readonly Color Disconnected = new Color(0.9f, 0.3f, 0.3f);

        private InputField ipInput;
        private InputField portInput;
        private Toggle simulationToggle;
        private Text statusText;
        private Image statusDot;
        private Text logText;
        private readonly StringBuilder logBuilder = new StringBuilder();
        private int logLineCount;
        private const int MaxLogLines = 50;

        public void Build()
        {
            var root = GetComponent<RectTransform>();
            var connector = WallpadManager.Instance != null ? WallpadManager.Instance.Connector : null;

            UiFactory.CreateLabel("IpLabel", root, new Vector2(0, 0.9f), new Vector2(0.18f, 1f), Vector2.zero, Vector2.zero, "IP", 24, Color.white, TextAnchor.MiddleLeft);
            ipInput = UiFactory.CreateInput("Ip", root, new Vector2(0.18f, 0.9f), new Vector2(0.55f, 1f), Vector2.zero, new Vector2(-8, 0), connector != null ? connector.ArduinoIp : "192.168.0.100");

            UiFactory.CreateLabel("PortLabel", root, new Vector2(0.56f, 0.9f), new Vector2(0.68f, 1f), Vector2.zero, Vector2.zero, "포트", 24, Color.white, TextAnchor.MiddleLeft);
            portInput = UiFactory.CreateInput("Port", root, new Vector2(0.68f, 0.9f), Vector2.one, Vector2.zero, Vector2.zero, connector != null ? connector.ArduinoPort.ToString() : "8080");

            simulationToggle = UiFactory.CreateToggle("Sim", root, new Vector2(0, 0.78f), new Vector2(0.45f, 0.88f), Vector2.zero, Vector2.zero, "시뮬레이션 모드", connector == null || connector.UseSimulationMode);

            var apply = UiFactory.CreateButton("Apply", root, new Vector2(0.48f, 0.78f), new Vector2(0.72f, 0.88f), Vector2.zero, new Vector2(-8, 0), "적용", new Color(0.18f, 0.45f, 0.9f), 24);
            apply.onClick.AddListener(OnApplyClicked);
            var clear = UiFactory.CreateButton("Clear", root, new Vector2(0.74f, 0.78f), Vector2.one, Vector2.zero, Vector2.zero, "로그 지우기", new Color(0.18f, 0.2f, 0.26f), 24);
            clear.onClick.AddListener(ClearLogs);

            var statusRect = UiFactory.Create("StatusDot", root, new Vector2(0.02f, 0.70f), new Vector2(0.02f, 0.70f), Vector2.zero, Vector2.zero);
            statusRect.sizeDelta = new Vector2(16, 16);
            statusDot = UiFactory.AddImage(statusRect, Connected, false);
            statusText = UiFactory.CreateLabel("Status", root, new Vector2(0.05f, 0.64f), new Vector2(1, 0.76f), Vector2.zero, Vector2.zero, "상태", 24, Connected, TextAnchor.MiddleLeft);

            var logRect = UiFactory.Create("Log", root, Vector2.zero, new Vector2(1, 0.62f), Vector2.zero, Vector2.zero);
            UiFactory.AddImage(logRect, new Color(0.08f, 0.09f, 0.12f), false);
            logText = UiFactory.CreateLabel("LogText", logRect, Vector2.zero, Vector2.one, new Vector2(16, 12), new Vector2(-16, -12), "", 18, new Color(0.85f, 0.9f, 0.95f), TextAnchor.UpperLeft);
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;
            logText.supportRichText = true;

            if (connector != null)
            {
                connector.OnConnectionStatusChanged += UpdateConnectionStatus;
                connector.OnLogMessage += AppendLog;
                UpdateConnectionStatus(connector.IsConnected);
            }
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance == null || WallpadManager.Instance.Connector == null) return;
            WallpadManager.Instance.Connector.OnConnectionStatusChanged -= UpdateConnectionStatus;
            WallpadManager.Instance.Connector.OnLogMessage -= AppendLog;
        }

        private void OnApplyClicked()
        {
            if (WallpadManager.Instance == null || WallpadManager.Instance.Connector == null) return;
            string ip = ipInput != null ? ipInput.text.Trim() : "192.168.0.100";
            int port = 8080;
            if (portInput != null) int.TryParse(portInput.text.Trim(), out port);
            bool sim = simulationToggle != null && simulationToggle.isOn;
            WallpadManager.Instance.Connector.SetTarget(ip, port, sim);
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
            bool sim = WallpadManager.Instance != null && WallpadManager.Instance.Connector.UseSimulationMode;
            if (statusText != null)
            {
                statusText.text = isConnected ? (sim ? "가상 시뮬레이션 연결됨" : "아두이노 UNO WiFi 연결됨") : "통신 끊김 (오프라인)";
                statusText.color = isConnected ? Connected : Disconnected;
            }

            if (statusDot != null)
            {
                statusDot.color = isConnected ? Connected : Disconnected;
            }
        }

        private void AppendLog(string message, bool isTx)
        {
            string color = isTx ? "#55AAFF" : "#AAAAAA";
            logBuilder.Append($"<color=#888888>[{System.DateTime.Now:HH:mm:ss}]</color> <color={color}>{message}</color>\n");
            logLineCount++;
            if (logLineCount > MaxLogLines)
            {
                string current = logBuilder.ToString();
                int newline = current.IndexOf('\n');
                if (newline >= 0)
                {
                    logBuilder.Remove(0, newline + 1);
                    logLineCount--;
                }
            }

            if (logText != null) logText.text = logBuilder.ToString();
        }

        private void ClearLogs()
        {
            logBuilder.Clear();
            logLineCount = 0;
            if (logText != null) logText.text = string.Empty;
        }
    }
}
