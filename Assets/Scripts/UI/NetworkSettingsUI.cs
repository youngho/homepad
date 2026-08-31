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

        [SerializeField] private InputField ipInput;
        [SerializeField] private InputField portInput;
        [SerializeField] private Toggle simulationToggle;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button clearLogButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Image statusDot;
        [SerializeField] private Text logText;

        private readonly StringBuilder logBuilder = new StringBuilder();
        private int logLineCount;
        private const int MaxLogLines = 50;

        private void Start()
        {
            var connector = WallpadManager.Instance != null ? WallpadManager.Instance.Connector : null;
            if (connector != null)
            {
                if (ipInput != null) ipInput.text = connector.ArduinoIp;
                if (portInput != null) portInput.text = connector.ArduinoPort.ToString();
                if (simulationToggle != null) simulationToggle.isOn = connector.UseSimulationMode;
                connector.OnConnectionStatusChanged += UpdateConnectionStatus;
                connector.OnLogMessage += AppendLog;
                UpdateConnectionStatus(connector.IsConnected);
            }

            if (applyButton != null)
            {
                applyButton.onClick.RemoveAllListeners();
                applyButton.onClick.AddListener(OnApplyClicked);
            }

            if (clearLogButton != null)
            {
                clearLogButton.onClick.RemoveAllListeners();
                clearLogButton.onClick.AddListener(ClearLogs);
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
