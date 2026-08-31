using System.Text;
using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    /// <summary>
    /// 아두이노 통신 설정 및 RS-485 패킷 모니터링 UI
    /// </summary>
    public class NetworkSettingsUI : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private InputField ipInputField;
        [SerializeField] private InputField portInputField;
        [SerializeField] private Toggle simulationToggle;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button clearLogButton;

        [Header("Status & Log")]
        [SerializeField] private Text connectionStatusText;
        [SerializeField] private Image connectionStatusIndicator;
        [SerializeField] private Text logText;
        [SerializeField] private ScrollRect logScrollRect;

        [Header("Colors")]
        [SerializeField] private Color connectedColor = new Color(0.2f, 0.85f, 0.4f);
        [SerializeField] private Color disconnectedColor = new Color(0.9f, 0.3f, 0.3f);

        private readonly StringBuilder logBuilder = new StringBuilder();
        private const int MAX_LOG_LINES = 50;
        private int logLineCount = 0;

        private void Start()
        {
            var connector = WallpadManager.Instance != null ? WallpadManager.Instance.Connector : null;

            if (connector != null)
            {
                if (ipInputField != null) ipInputField.text = connector.ArduinoIp;
                if (portInputField != null) portInputField.text = connector.ArduinoPort.ToString();
                if (simulationToggle != null) simulationToggle.isOn = connector.UseSimulationMode;

                connector.OnConnectionStatusChanged += UpdateConnectionStatus;
                connector.OnLogMessage += AppendLog;

                UpdateConnectionStatus(connector.IsConnected);
            }

            if (applyButton != null)
            {
                applyButton.onClick.AddListener(OnApplyClicked);
            }

            if (clearLogButton != null)
            {
                clearLogButton.onClick.AddListener(ClearLogs);
            }
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance != null && WallpadManager.Instance.Connector != null)
            {
                WallpadManager.Instance.Connector.OnConnectionStatusChanged -= UpdateConnectionStatus;
                WallpadManager.Instance.Connector.OnLogMessage -= AppendLog;
            }
        }

        private void OnApplyClicked()
        {
            if (WallpadManager.Instance == null || WallpadManager.Instance.Connector == null) return;

            string ip = ipInputField != null ? ipInputField.text.Trim() : "192.168.0.100";
            int port = 8080;
            if (portInputField != null && int.TryParse(portInputField.text.Trim(), out int parsedPort))
            {
                port = parsedPort;
            }
            bool isSim = simulationToggle != null && simulationToggle.isOn;

            WallpadManager.Instance.Connector.SetTarget(ip, port, isSim);
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
            if (connectionStatusText != null)
            {
                bool isSim = WallpadManager.Instance != null && WallpadManager.Instance.Connector.UseSimulationMode;
                connectionStatusText.text = isConnected ? (isSim ? "가상 시뮬레이션 연결됨" : "아두이노 UNO WiFi 연결됨") : "통신 끊김 (오프라인)";
                connectionStatusText.color = isConnected ? connectedColor : disconnectedColor;
            }
            if (connectionStatusIndicator != null)
            {
                connectionStatusIndicator.color = isConnected ? connectedColor : disconnectedColor;
            }
        }

        private void AppendLog(string message, bool isTx)
        {
            string timeStr = System.DateTime.Now.ToString("HH:mm:ss");
            string formatted = $"<color=#888888>[{timeStr}]</color> {(isTx ? "<color=#55AAFF>" : "<color=#AAAAAA>")}{message}</color>\n";

            logBuilder.Append(formatted);
            logLineCount++;

            if (logLineCount > MAX_LOG_LINES)
            {
                // Trim first lines
                string current = logBuilder.ToString();
                int firstNewline = current.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    logBuilder.Remove(0, firstNewline + 1);
                    logLineCount--;
                }
            }

            if (logText != null)
            {
                logText.text = logBuilder.ToString();
            }

            if (logScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                logScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void ClearLogs()
        {
            logBuilder.Clear();
            logLineCount = 0;
            if (logText != null) logText.text = string.Empty;
        }
    }
}
