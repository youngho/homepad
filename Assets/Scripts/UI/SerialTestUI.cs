using System.Text;
using Homepad.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class SerialTestUI : MonoBehaviour
    {
        private static readonly Color Danger = new Color(0.78f, 0.28f, 0.24f, 1f);
        private static readonly Color Ok = new Color(0.20f, 0.62f, 0.38f, 1f);

        [SerializeField] private InputField portField;
        [SerializeField] private InputField baudField;
        [SerializeField] private Text statusText;
        [SerializeField] private Text logText;
        [SerializeField] private Image statusDot;
        [SerializeField] private Button wallpadButton;
        [SerializeField] private Button prevPortButton;
        [SerializeField] private Button nextPortButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button[] commandButtons;

        private readonly StringBuilder logBuilder = new StringBuilder();
        private int logLineCount;
        private string[] ports = new string[0];
        private int portIndex;
        private const int MaxLogLines = 80;

        private void Awake()
        {
            ResolveUi();
        }

        private void ResolveUi()
        {
            if (portField == null) portField = FindUi<InputField>("Port");
            if (baudField == null) baudField = FindUi<InputField>("Baud");
            if (statusText == null) statusText = FindUi<Text>("Status");
            if (logText == null) logText = FindUi<Text>("Log");
            if (statusDot == null) statusDot = FindUi<Image>("StatusDot");
            if (wallpadButton == null) wallpadButton = FindUi<Button>("WallpadScene");
            if (prevPortButton == null) prevPortButton = FindUi<Button>("PrevPort");
            if (nextPortButton == null) nextPortButton = FindUi<Button>("NextPort");
            if (refreshButton == null) refreshButton = FindUi<Button>("Refresh");
            if (connectButton == null) connectButton = FindUi<Button>("Connect");
            if (disconnectButton == null) disconnectButton = FindUi<Button>("Disconnect");

            if (commandButtons == null || commandButtons.Length == 0)
            {
                commandButtons = new Button[15];
                for (int i = 0; i < commandButtons.Length; i++)
                {
                    commandButtons[i] = FindUi<Button>("Cmd" + i);
                }
            }
        }

        private T FindUi<T>(string objectName) where T : Component
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name != objectName) continue;
                var component = transforms[i].GetComponent<T>();
                if (component != null) return component;
            }

            return null;
        }

        private void Start()
        {
            BindButtons();
            var connector = WallpadManager.Instance != null ? WallpadManager.Instance.Connector : null;
            if (connector != null)
            {
                connector.OnConnectionStatusChanged += UpdateStatus;
                connector.OnLogMessage += AppendLog;
                UpdateStatus(connector.IsConnected);
            }

            RefreshPorts(true);
            if (ports.Length > 0)
            {
                OnConnectClicked();
            }
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance == null || WallpadManager.Instance.Connector == null) return;
            WallpadManager.Instance.Connector.OnConnectionStatusChanged -= UpdateStatus;
            WallpadManager.Instance.Connector.OnLogMessage -= AppendLog;
        }

        private void BindButtons()
        {
            Bind(wallpadButton, () => SceneManager.LoadScene("WallpadMain"));
            Bind(prevPortButton, () => CyclePort(-1));
            Bind(nextPortButton, () => CyclePort(1));
            Bind(refreshButton, () => RefreshPorts(false));
            Bind(connectButton, OnConnectClicked);
            Bind(disconnectButton, () => WallpadManager.Instance.Connector.Disconnect());

            if (commandButtons == null) return;
            for (int i = 0; i < commandButtons.Length; i++)
            {
                int index = i;
                Bind(commandButtons[i], () => RunCommand(index));
            }
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void RunCommand(int index)
        {
            var manager = WallpadManager.Instance;
            if (manager == null) return;

            switch (index)
            {
                case 0: manager.ToggleLight(1); break;
                case 1: manager.ToggleLight(2); break;
                case 2: manager.ToggleLight(3); break;
                case 3: manager.ToggleLight(4); break;
                case 4: manager.ToggleLight(5); break;
                case 5: manager.ToggleLight(6); break;
                case 6: manager.TurnOffAllLights(); break;
                case 7: manager.CloseGasValve(); break;
                case 8: manager.ToggleAwayMode(); break;
                case 9: manager.SetVentilationSpeed(VentilationSpeed.Off); break;
                case 10: manager.SetVentilationSpeed(VentilationSpeed.Low); break;
                case 11: manager.SetVentilationSpeed(VentilationSpeed.High); break;
                case 12:
                    if (manager.HeatingRooms.Count > 0)
                    {
                        var room = manager.HeatingRooms[0];
                        manager.SetHeatingTargetTemp(room.roomId, room.targetTemp + 0.5f);
                    }
                    break;
                case 13:
                    if (manager.HeatingRooms.Count > 0)
                    {
                        var room = manager.HeatingRooms[0];
                        manager.SetHeatingTargetTemp(room.roomId, room.targetTemp - 0.5f);
                    }
                    break;
                case 14: manager.CallElevator(); break;
            }
        }

        private void RefreshPorts(bool preferSaved)
        {
            ports = ArduinoConnector.ListSerialPorts();
            string saved = PlayerPrefs.GetString("Homepad.SerialPort", "");
            if (ports.Length == 0)
            {
                if (preferSaved && !string.IsNullOrEmpty(saved) && portField != null)
                {
                    portField.text = saved;
                }

                AppendLog("[시스템] USB 시리얼 포트를 찾지 못했습니다. 아두이노를 다시 꽂거나 IDE 시리얼 모니터를 닫은 뒤 새로고침하세요.", false);
                return;
            }

            portIndex = 0;
            if (preferSaved)
            {
                int found = System.Array.IndexOf(ports, saved);
                if (found >= 0) portIndex = found;
            }

            if (portField != null) portField.text = ports[portIndex];
            AppendLog($"[시스템] 시리얼 포트 {ports.Length}개: {string.Join(", ", ports)}", false);
        }

        private void CyclePort(int delta)
        {
            if (ports == null || ports.Length == 0)
            {
                RefreshPorts(false);
                return;
            }

            portIndex = (portIndex + delta + ports.Length) % ports.Length;
            if (portField != null) portField.text = ports[portIndex];
        }

        private void OnConnectClicked()
        {
            if (WallpadManager.Instance == null || WallpadManager.Instance.Connector == null) return;
            string port = portField != null ? portField.text.Trim() : "";
            if (string.IsNullOrEmpty(port))
            {
                AppendLog("[오류] 시리얼 포트가 비어 있습니다. 새로고침 후 포트를 선택하세요.", false);
                return;
            }

            int baud = 115200;
            if (baudField != null) int.TryParse(baudField.text.Trim(), out baud);
            WallpadManager.Instance.Connector.SetSerialTarget(port, baud);
        }

        private void UpdateStatus(bool isConnected)
        {
            if (statusText != null)
            {
                statusText.text = isConnected ? "시리얼 연결됨" : "연결 안 됨";
                statusText.color = isConnected ? Ok : Danger;
            }

            if (statusDot != null)
            {
                statusDot.color = isConnected ? Ok : Danger;
            }
        }

        private void AppendLog(string message, bool isTx)
        {
            string color = isTx ? "#55AAFF" : "#CCCCCC";
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
    }
}
