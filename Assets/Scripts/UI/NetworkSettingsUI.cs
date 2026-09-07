using System;
using System.Collections;
using System.Text;
using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class NetworkSettingsUI : MonoBehaviour
    {
        private static readonly Color MutedGreen = new Color(0.337f, 0.588f, 0.408f);
        private static readonly Color MutedRed = new Color(0.753f, 0.337f, 0.337f);
        private static readonly Color TabInactiveTextColor = new Color(0.62f, 0.65f, 0.71f);

        [Header("Serial Connection")]
        [SerializeField] private InputField portField;
        [SerializeField] private InputField baudField;
        [SerializeField] private Text baudLabel;
        [SerializeField] private Text statusText;
        [SerializeField] private Image statusDot;
        [SerializeField] private Button prevPortButton;
        [SerializeField] private Button nextPortButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Toggle arduinoToggle;
        [SerializeField] private Toggle ew11Toggle;
        [SerializeField] private Toggle serialToggle;
        [SerializeField] private Toggle tcpToggle;
        [SerializeField] private Toggle mqttToggle;
        [SerializeField] private InputField mqttUserField;
        [SerializeField] private InputField mqttPassField;
        [SerializeField] private InputField mqttTxField;
        [SerializeField] private InputField mqttRxField;
        [SerializeField] private GameObject mqttBar;

        [Header("Log Overlay")]
        [SerializeField] private Transform settingsRoot;
        [SerializeField] private Toggle showLogToggle;
        [SerializeField] private GameObject logPanel;
        [SerializeField] private Text logText;
        [SerializeField] private ScrollRect logScrollRect;
        [SerializeField] private Button clearLogButton;

        private readonly StringBuilder logBuilder = new StringBuilder();
        private int logLineCount;
        private const int MaxLogLines = 80;
        private string[] ports = new string[0];
        private int portIndex;
        private string[] lastSeenPorts = new string[0];
        [SerializeField] private ArduinoConnector connector;
        private KocomLinkDevice linkDevice = KocomLinkDevice.Arduino;
        private ArduinoLinkMode linkProtocol = ArduinoLinkMode.Serial;
        private ArduinoLinkMode arduinoProtocol = ArduinoLinkMode.Serial;
        private ArduinoLinkMode ew11Protocol = ArduinoLinkMode.Tcp;
        private bool linkUiApplying;
        private bool autoConnectAttempted;
        private Coroutine logScrollRoutine;

        private void Awake()
        {
            AutoResolveUiReferences();
        }

        private void Start()
        {
            EnsureReady();
            LoadLinkPrefs();
            if (linkProtocol == ArduinoLinkMode.Serial)
            {
                RefreshPorts(true);
            }
            else
            {
                ports = ArduinoConnector.ListSerialPorts() ?? new string[0];
            }

            lastSeenPorts = ports ?? new string[0];
            ApplyLinkUi();
            ApplyLogVisibility(PlayerPrefs.GetInt("Homepad.ShowHexLog", 0) == 1, false);
            StartCoroutine(WatchUsbRoutine());
        }

        private void OnDestroy()
        {
            if (connector == null) return;
            connector.OnConnectionStatusChanged -= UpdateStatus;
            connector.OnLogMessage -= AppendLog;
        }

        private void AutoResolveUiReferences()
        {
            if (settingsRoot == null)
            {
                settingsRoot = FindChildEvenIfInactive(transform, "Settings");
            }

            if (logPanel == null)
            {
                var logRt = FindChildEvenIfInactive(transform, "HexLog");
                if (logRt != null) logPanel = logRt.gameObject;
            }

            if (portField == null) portField = FindUi<InputField>("Port");
            if (baudField == null) baudField = FindUi<InputField>("Baud");
            if (baudLabel == null) baudLabel = FindUi<Text>("BaudLabel");
            if (statusText == null) statusText = FindUi<Text>("Status");
            if (statusDot == null) statusDot = FindUi<Image>("StatusDot");
            if (prevPortButton == null) prevPortButton = FindUi<Button>("PrevPort");
            if (nextPortButton == null) nextPortButton = FindUi<Button>("NextPort");
            if (refreshButton == null) refreshButton = FindUi<Button>("Refresh");
            if (connectButton == null) connectButton = FindUi<Button>("Connect");
            if (disconnectButton == null) disconnectButton = FindUi<Button>("Disconnect");
            if (arduinoToggle == null) arduinoToggle = FindUi<Toggle>("DeviceArduino");
            if (ew11Toggle == null) ew11Toggle = FindUi<Toggle>("DeviceEw11");
            if (serialToggle == null) serialToggle = FindUi<Toggle>("ProtoSerial");
            if (tcpToggle == null) tcpToggle = FindUi<Toggle>("ProtoTcp");
            if (mqttToggle == null) mqttToggle = FindUi<Toggle>("ProtoMqtt");
            if (mqttUserField == null) mqttUserField = FindUi<InputField>("MqttUser");
            if (mqttPassField == null) mqttPassField = FindUi<InputField>("MqttPass");
            if (mqttTxField == null) mqttTxField = FindUi<InputField>("MqttTx");
            if (mqttRxField == null) mqttRxField = FindUi<InputField>("MqttRx");
            if (mqttBar == null)
            {
                var mqttRt = FindUi<RectTransform>("MqttBar");
                if (mqttRt != null) mqttBar = mqttRt.gameObject;
            }

            if (showLogToggle == null) showLogToggle = FindUi<Toggle>("ShowLog");
            if (logText == null) logText = FindIn<Text>(logPanel != null ? logPanel.transform : null, "LogText");
            if (logScrollRect == null) logScrollRect = FindIn<ScrollRect>(logPanel != null ? logPanel.transform : null, "LogScroll");
            if (clearLogButton == null) clearLogButton = FindIn<Button>(logPanel != null ? logPanel.transform : null, "ClearLog");
        }

        private T FindUi<T>(string objectName) where T : Component
        {
            var found = FindIn<T>(settingsRoot, objectName);
            if (found != null) return found;
            if (logPanel != null)
            {
                found = FindIn<T>(logPanel.transform, objectName);
                if (found != null) return found;
            }

            return FindIn<T>(transform, objectName);
        }

        private static Transform FindChildEvenIfInactive(Transform root, string objectName)
        {
            if (root == null) return null;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName) return transforms[i];
            }

            return null;
        }

        private static T FindIn<T>(Transform root, string objectName) where T : Component // scoped lookup
        {
            if (root == null) return null;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name != objectName) continue;
                var comp = transforms[i].GetComponent<T>();
                if (comp != null) return comp;
            }

            return null;
        }

        public void EnsureReady()
        {
            AutoResolveUiReferences();
            BindEvents();
            HookConnector();
        }

        private void BindEvents()
        {
            Bind(prevPortButton, () => CyclePort(-1));
            Bind(nextPortButton, () => CyclePort(1));
            Bind(refreshButton, () => RefreshPorts(false));
            Bind(connectButton, OnConnectClicked);
            Bind(disconnectButton, () => GetConnector()?.Disconnect());
            BindToggle(arduinoToggle, on => { if (on) SelectDevice(KocomLinkDevice.Arduino); });
            BindToggle(ew11Toggle, on => { if (on) SelectDevice(KocomLinkDevice.Ew11); });
            BindToggle(serialToggle, on => { if (on) SelectProtocol(ArduinoLinkMode.Serial); });
            BindToggle(tcpToggle, on => { if (on) SelectProtocol(ArduinoLinkMode.Tcp); });
            BindToggle(mqttToggle, on => { if (on) SelectProtocol(ArduinoLinkMode.Mqtt); });
            Bind(clearLogButton, ClearLogs);
            BindToggle(showLogToggle, on => ApplyLogVisibility(on, true));
        }

        private void HookConnector()
        {
            var serial = GetConnector();
            if (serial == null) return;

            serial.OnConnectionStatusChanged -= UpdateStatus;
            serial.OnLogMessage -= AppendLog;
            serial.OnConnectionStatusChanged += UpdateStatus;
            serial.OnLogMessage += AppendLog;
            UpdateStatus(serial.IsConnected);
        }

        private ArduinoConnector GetConnector()
        {
            if (connector) return connector;

            if (WallpadManager.Instance != null)
            {
                connector = WallpadManager.Instance.EnsureConnector();
                if (connector) return connector;
            }

            connector = FindFirstObjectByType<ArduinoConnector>(FindObjectsInactive.Include);
            if (connector) return connector;

            var host = WallpadManager.Instance != null
                ? WallpadManager.Instance.gameObject
                : gameObject;
            connector = host.GetComponent<ArduinoConnector>();
            if (!connector) connector = host.AddComponent<ArduinoConnector>();
            return connector;
        }

        private IEnumerator WatchUsbRoutine()
        {
            var wait = new WaitForSeconds(1f);
            while (true)
            {
                yield return wait;

                string[] now;
                try
                {
                    now = ArduinoConnector.ListSerialPorts();
                }
                catch
                {
                    continue;
                }

                if (now == null) now = new string[0];

                for (int i = 0; i < now.Length; i++)
                {
                    if (IndexOfPort(lastSeenPorts, now[i]) < 0)
                    {
                        AppendLog($"<color=#5CAE7C>[USB 감지]</color> {now[i]}", false);
                        if (linkProtocol == ArduinoLinkMode.Serial && portField != null)
                        {
                            portField.text = now[i];
                            portIndex = i;
                        }

                        TryAutoConnect();
                    }
                }

                for (int i = 0; i < lastSeenPorts.Length; i++)
                {
                    if (IndexOfPort(now, lastSeenPorts[i]) < 0)
                    {
                        AppendLog($"<color=#CF5C5C>[USB 해제]</color> {lastSeenPorts[i]}", false);
                        var serial = connector;
                        if (serial != null && serial.IsConnected && serial.SerialPortName == lastSeenPorts[i])
                        {
                            serial.Disconnect();
                            autoConnectAttempted = false;
                            AppendLog("[시리얼] USB가 빠져 연결을 끊었습니다.", false);
                        }
                    }
                }

                ports = now;
                lastSeenPorts = now;
            }
        }

        private static int IndexOfPort(string[] list, string port)
        {
            if (list == null || string.IsNullOrEmpty(port)) return -1;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == port) return i;
            }

            return -1;
        }

        private void TryAutoConnect()
        {
            if (autoConnectAttempted) return;
            if (linkProtocol != ArduinoLinkMode.Serial) return;
            if (linkDevice != KocomLinkDevice.Arduino) return;
            if (portField == null || string.IsNullOrEmpty(portField.text.Trim())) return;
            autoConnectAttempted = true;
            StartCoroutine(AutoConnectAfterDelay());
        }

        private IEnumerator AutoConnectAfterDelay()
        {
            AppendLog("[시스템] USB 포트를 찾았습니다. 보드가 준비될 때까지 잠시 기다립니다.", false);
            yield return new WaitForSeconds(1.6f);
            OnConnectClicked();
        }

        private void RefreshPorts(bool preferSaved)
        {
            ports = ArduinoConnector.ListSerialPorts();
            string saved = PlayerPrefs.GetString("Homepad.SerialPort", "");
            if (ports.Length == 0)
            {
                if (preferSaved && linkProtocol == ArduinoLinkMode.Serial && !string.IsNullOrEmpty(saved) && portField != null)
                {
                    portField.text = saved;
                }

                if (linkProtocol == ArduinoLinkMode.Serial)
                {
                    AppendLog("<color=#CF5C5C>[시스템] USB 시리얼 포트를 찾지 못했습니다. 아두이노를 다시 꽂거나 IDE 시리얼 모니터를 닫은 뒤 새로고침하세요.</color>", false);
                }

                return;
            }

            portIndex = 0;
            if (preferSaved)
            {
                int found = Array.IndexOf(ports, saved);
                if (found >= 0) portIndex = found;
            }

            if (linkProtocol == ArduinoLinkMode.Serial && portField != null)
            {
                portField.text = ports[portIndex];
            }

            AppendLog($"<color=#9EA6B5>[시스템] 시리얼 포트 {ports.Length}개: {string.Join(", ", ports)}</color>", false);
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
            var serial = GetConnector();
            if (serial == null)
            {
                AppendLog("<color=#CF5C5C>[오류] ArduinoConnector를 찾을 수 없습니다.</color>", false);
                return;
            }

            serial.SetLinkLabel(DeviceLabel());
            SaveLinkPrefs();

            if (linkProtocol == ArduinoLinkMode.Serial)
            {
                if (linkDevice != KocomLinkDevice.Arduino)
                {
                    AppendLog("<color=#E5B550>[안내] EW-11은 USB 시리얼이 없습니다. TCP 또는 MQTT를 선택하세요.</color>", false);
                    return;
                }

                string port = portField != null ? portField.text.Trim() : "";
                if (string.IsNullOrEmpty(port))
                {
                    AppendLog("<color=#CF5C5C>[오류] 시리얼 포트가 비어 있습니다. 새로고침 후 포트를 선택하세요.</color>", false);
                    return;
                }

                int baud = 115200;
                if (baudField != null && int.TryParse(baudField.text.Trim(), out int parsed) && parsed > 0)
                {
                    baud = parsed;
                }

                serial.SetSerialTarget(port, baud);
                return;
            }

            string host = portField != null ? portField.text.Trim() : "";
            if (string.IsNullOrEmpty(host))
            {
                AppendLog("<color=#CF5C5C>[오류] 호스트 주소가 비어 있습니다.</color>", false);
                return;
            }

            int netPort = linkProtocol == ArduinoLinkMode.Mqtt ? 1883 : DefaultTcpPort();
            if (baudField != null && int.TryParse(baudField.text.Trim(), out int parsedPort) && parsedPort > 0)
            {
                netPort = parsedPort;
            }

            if (linkProtocol == ArduinoLinkMode.Mqtt)
            {
                string user = mqttUserField != null ? mqttUserField.text.Trim() : "";
                string pass = mqttPassField != null ? mqttPassField.text : "";
                string tx = mqttTxField != null ? mqttTxField.text.Trim() : "kocom/tx";
                string rx = mqttRxField != null ? mqttRxField.text.Trim() : "kocom/rx";
                if (string.IsNullOrEmpty(tx)) tx = "kocom/tx";
                if (string.IsNullOrEmpty(rx)) rx = "kocom/rx";
                serial.SetMqttTarget(host, netPort, user, pass, tx, rx);
                return;
            }

            serial.SetTcpTarget(host, netPort);
        }

        private void SelectDevice(KocomLinkDevice device)
        {
            if (linkUiApplying || linkDevice == device) return;

            RememberProtocolForDevice();
            bool switchedToEw11 = device == KocomLinkDevice.Ew11;
            linkDevice = device;
            linkProtocol = ProtocolForDevice(device);

            if (linkProtocol == ArduinoLinkMode.Serial)
            {
                EnsureSerialPortsListed();
            }

            ApplyLinkUi();
            SaveLinkPrefs();
            if (switchedToEw11)
            {
                AppendLog("<color=#9EA6B5>[안내] EW-11은 TCP(기본 포트 8899) 또는 MQTT로 붙습니다. 장치 IP를 입력한 뒤 연결하세요.</color>", false);
            }
        }

        private void SelectProtocol(ArduinoLinkMode protocol)
        {
            if (linkUiApplying || linkProtocol == protocol) return;
            if (linkDevice == KocomLinkDevice.Ew11 && protocol == ArduinoLinkMode.Serial)
            {
                ApplyLinkUi();
                return;
            }

            linkProtocol = protocol;
            RememberProtocolForDevice();
            if (linkProtocol == ArduinoLinkMode.Serial)
            {
                EnsureSerialPortsListed();
            }

            ApplyLinkUi();
            SaveLinkPrefs();
        }

        private void RememberProtocolForDevice()
        {
            if (linkDevice == KocomLinkDevice.Arduino)
            {
                arduinoProtocol = linkProtocol;
            }
            else
            {
                ew11Protocol = linkProtocol == ArduinoLinkMode.Serial
                    ? ArduinoLinkMode.Tcp
                    : linkProtocol;
            }
        }

        private ArduinoLinkMode ProtocolForDevice(KocomLinkDevice device)
        {
            return device == KocomLinkDevice.Arduino
                ? ClampArduinoProtocol(arduinoProtocol)
                : ClampEw11Protocol(ew11Protocol);
        }

        private string DeviceLabel()
        {
            return linkDevice == KocomLinkDevice.Ew11 ? "EW-11" : "아두이노";
        }

        private int DefaultTcpPort()
        {
            return linkDevice == KocomLinkDevice.Ew11 ? 8899 : 8080;
        }

        private void UpdateStatus(bool isConnected)
        {
            if (statusText != null)
            {
                if (!isConnected)
                {
                    statusText.text = "연결 안 됨";
                }
                else if (linkProtocol == ArduinoLinkMode.Mqtt)
                {
                    statusText.text = DeviceLabel() + " MQTT 연결됨";
                }
                else if (linkProtocol == ArduinoLinkMode.Tcp)
                {
                    statusText.text = DeviceLabel() + " TCP 연결됨";
                }
                else
                {
                    statusText.text = "시리얼 연결됨";
                }

                statusText.color = isConnected ? MutedGreen : MutedRed;
            }

            if (statusDot != null)
            {
                statusDot.color = isConnected ? MutedGreen : MutedRed;
            }
        }

        private void LoadLinkPrefs()
        {
            linkDevice = (KocomLinkDevice)PlayerPrefs.GetInt("Homepad.LinkDevice", (int)KocomLinkDevice.Arduino);
            if (linkDevice != KocomLinkDevice.Ew11) linkDevice = KocomLinkDevice.Arduino;

            int legacy = PlayerPrefs.GetInt("Homepad.LinkProtocol", (int)ArduinoLinkMode.Serial);
            arduinoProtocol = PlayerPrefs.HasKey("Homepad.ArduinoProtocol")
                ? ClampArduinoProtocol((ArduinoLinkMode)PlayerPrefs.GetInt("Homepad.ArduinoProtocol"))
                : ArduinoLinkMode.Serial;
            ew11Protocol = PlayerPrefs.HasKey("Homepad.Ew11Protocol")
                ? ClampEw11Protocol((ArduinoLinkMode)PlayerPrefs.GetInt("Homepad.Ew11Protocol"))
                : (linkDevice == KocomLinkDevice.Ew11 ? ClampEw11Protocol((ArduinoLinkMode)legacy) : ArduinoLinkMode.Tcp);
            linkProtocol = ProtocolForDevice(linkDevice);

            if (mqttUserField != null) mqttUserField.text = PlayerPrefs.GetString("Homepad.MqttUser", "");
            if (mqttPassField != null) mqttPassField.text = PlayerPrefs.GetString("Homepad.MqttPass", "");
            if (mqttTxField != null)
            {
                string tx = PlayerPrefs.GetString("Homepad.MqttTx", "kocom/tx");
                mqttTxField.text = string.IsNullOrEmpty(tx) ? "kocom/tx" : tx;
            }

            if (mqttRxField != null)
            {
                string rx = PlayerPrefs.GetString("Homepad.MqttRx", "kocom/rx");
                mqttRxField.text = string.IsNullOrEmpty(rx) ? "kocom/rx" : rx;
            }
        }

        private void SaveLinkPrefs()
        {
            RememberProtocolForDevice();
            PlayerPrefs.SetInt("Homepad.LinkDevice", (int)linkDevice);
            PlayerPrefs.SetInt("Homepad.LinkProtocol", (int)linkProtocol);
            PlayerPrefs.SetInt("Homepad.ArduinoProtocol", (int)arduinoProtocol);
            PlayerPrefs.SetInt("Homepad.Ew11Protocol", (int)ew11Protocol);
            if (linkProtocol == ArduinoLinkMode.Serial && portField != null)
            {
                string serialPort = portField.text.Trim();
                if (!string.IsNullOrEmpty(serialPort) && !LooksLikeNetworkHost(serialPort))
                {
                    PlayerPrefs.SetString("Homepad.SerialPort", serialPort);
                }
            }

            if (linkProtocol == ArduinoLinkMode.Tcp && portField != null)
            {
                PlayerPrefs.SetString("Homepad.TcpHost", portField.text.Trim());
                if (baudField != null && int.TryParse(baudField.text.Trim(), out int p))
                {
                    PlayerPrefs.SetInt("Homepad.TcpPort", p);
                }
            }

            if (linkProtocol == ArduinoLinkMode.Mqtt && portField != null)
            {
                PlayerPrefs.SetString("Homepad.MqttHost", portField.text.Trim());
                if (baudField != null && int.TryParse(baudField.text.Trim(), out int p))
                {
                    PlayerPrefs.SetInt("Homepad.MqttPort", p);
                }
            }

            if (mqttUserField != null) PlayerPrefs.SetString("Homepad.MqttUser", mqttUserField.text.Trim());
            if (mqttPassField != null) PlayerPrefs.SetString("Homepad.MqttPass", mqttPassField.text);
            if (mqttTxField != null) PlayerPrefs.SetString("Homepad.MqttTx", mqttTxField.text.Trim());
            if (mqttRxField != null) PlayerPrefs.SetString("Homepad.MqttRx", mqttRxField.text.Trim());
            PlayerPrefs.Save();
        }

        private void ApplyLinkUi()
        {
            bool arduino = linkDevice == KocomLinkDevice.Arduino;
            bool serial = linkProtocol == ArduinoLinkMode.Serial;
            bool mqtt = linkProtocol == ArduinoLinkMode.Mqtt;

            linkUiApplying = true;
            SetToggleOn(arduinoToggle, arduino);
            SetToggleOn(ew11Toggle, !arduino);
            SetToggleOn(serialToggle, serial);
            SetToggleOn(tcpToggle, linkProtocol == ArduinoLinkMode.Tcp);
            SetToggleOn(mqttToggle, mqtt);
            if (serialToggle != null)
            {
                serialToggle.interactable = arduino;
                serialToggle.gameObject.SetActive(arduino);
            }

            StyleToggle(arduinoToggle, arduino);
            StyleToggle(ew11Toggle, !arduino);
            StyleToggle(serialToggle, serial);
            StyleToggle(tcpToggle, linkProtocol == ArduinoLinkMode.Tcp);
            StyleToggle(mqttToggle, mqtt);
            linkUiApplying = false;

            SetActive(prevPortButton, serial);
            SetActive(nextPortButton, serial);
            SetActive(refreshButton, serial);
            if (baudLabel != null)
            {
                baudLabel.gameObject.SetActive(serial);
                baudLabel.fontStyle = FontStyle.Normal;
                baudLabel.text = "Baud: 115200";
            }

            if (baudField != null) baudField.gameObject.SetActive(!serial);
            if (mqttBar != null) mqttBar.SetActive(mqtt);

            if (portField != null && portField.placeholder is Text portPh)
            {
                portPh.fontStyle = FontStyle.Normal;
                portPh.fontSize = Mathf.Max(16, portPh.fontSize);
                portPh.text = serial ? "시리얼 포트" : (mqtt ? "MQTT 브로커 IP" : "장치 IP");
            }

            if (baudField != null && baudField.placeholder is Text baudPh)
            {
                baudPh.fontStyle = FontStyle.Normal;
                baudPh.fontSize = Mathf.Max(16, baudPh.fontSize);
                baudPh.text = mqtt ? "1883" : DefaultTcpPort().ToString();
            }

            if (serial)
            {
                FillSerialPortField();
            }
            else
            {
                FillNetworkFields(mqtt);
            }

            var protocolGroup = serialToggle != null
                ? serialToggle.transform.parent as RectTransform
                : (tcpToggle != null ? tcpToggle.transform.parent as RectTransform : null);
            if (protocolGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(protocolGroup);
            }
        }

        private void FillSerialPortField()
        {
            if (portField == null) return;

            string saved = PlayerPrefs.GetString("Homepad.SerialPort", "");
            if (ports != null && ports.Length > 0)
            {
                int found = IndexOfPort(ports, saved);
                portIndex = found >= 0 ? found : Mathf.Clamp(portIndex, 0, ports.Length - 1);
                portField.text = ports[portIndex];
            }
            else if (!string.IsNullOrEmpty(saved) && !LooksLikeNetworkHost(saved))
            {
                portField.text = saved;
            }
            else
            {
                portField.text = string.Empty;
            }

            if (baudField != null) baudField.text = "115200";
        }

        private void FillNetworkFields(bool mqtt)
        {
            if (portField == null) return;
            if (mqtt)
            {
                string host = PlayerPrefs.GetString("Homepad.MqttHost", "");
                int port = PlayerPrefs.GetInt("Homepad.MqttPort", 1883);
                if (string.IsNullOrEmpty(host) || LooksLikeSerialPort(host))
                {
                    host = PlayerPrefs.GetString("Homepad.TcpHost", "192.168.0.85");
                }

                if (string.IsNullOrEmpty(host) || LooksLikeSerialPort(host)) host = "192.168.0.85";
                portField.text = host;
                if (baudField != null) baudField.text = port > 0 ? port.ToString() : "1883";
            }
            else
            {
                string host = PlayerPrefs.GetString("Homepad.TcpHost", "192.168.0.85");
                int port = PlayerPrefs.GetInt("Homepad.TcpPort", DefaultTcpPort());
                if (LooksLikeSerialPort(host)) host = "192.168.0.85";
                if (port == 115200 || port <= 0) port = DefaultTcpPort();
                portField.text = host;
                if (baudField != null) baudField.text = port.ToString();
            }
        }

        private void EnsureSerialPortsListed()
        {
            ports = ArduinoConnector.ListSerialPorts() ?? new string[0];
            lastSeenPorts = ports;
        }

        private static ArduinoLinkMode ClampArduinoProtocol(ArduinoLinkMode protocol)
        {
            return protocol == ArduinoLinkMode.Tcp || protocol == ArduinoLinkMode.Mqtt
                ? protocol
                : ArduinoLinkMode.Serial;
        }

        private static ArduinoLinkMode ClampEw11Protocol(ArduinoLinkMode protocol)
        {
            return protocol == ArduinoLinkMode.Mqtt ? ArduinoLinkMode.Mqtt : ArduinoLinkMode.Tcp;
        }

        private static bool LooksLikeNetworkHost(string value)
        {
            if (string.IsNullOrEmpty(value) || LooksLikeSerialPort(value)) return false;
            return value.IndexOf('.') >= 0;
        }

        private static bool LooksLikeSerialPort(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.StartsWith("/") || value.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || value.Contains("/dev/");
        }

        private static void SetActive(Component target, bool on)
        {
            if (target != null) target.gameObject.SetActive(on);
        }

        private static void SetToggleOn(Toggle toggle, bool on)
        {
            if (toggle == null) return;
            toggle.SetIsOnWithoutNotify(on);
        }

        private static void StyleToggle(Toggle toggle, bool on)
        {
            if (toggle == null) return;
            var txt = toggle.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.fontStyle = FontStyle.Normal;
                txt.fontSize = Mathf.Max(16, txt.fontSize);
                txt.color = on ? Color.white : TabInactiveTextColor;
            }
        }

        private void ApplyLogVisibility(bool on, bool save)
        {
            if (logPanel != null)
            {
                logPanel.SetActive(on);
                if (on) logPanel.transform.SetAsLastSibling();
            }
            if (showLogToggle != null) showLogToggle.SetIsOnWithoutNotify(on);
            if (on) PinLogToBottomNextFrame();
            if (save)
            {
                PlayerPrefs.SetInt("Homepad.ShowHexLog", on ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void AppendLog(string message, bool isTx)
        {
            string color = isTx ? "#55AAFF" : "#AAAAAA";
            if (logBuilder.Length > 0) logBuilder.Append('\n');
            logBuilder.Append($"<color=#888888>[{DateTime.Now:HH:mm:ss}]</color> <color={color}>{message}</color>");
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

            if (logText != null)
            {
                logText.text = logBuilder.ToString();
            }

            PinLogToBottom();
            PinLogToBottomNextFrame();
        }

        private void PinLogToBottomNextFrame()
        {
            if (!isActiveAndEnabled)
            {
                PinLogToBottom();
                return;
            }

            if (logScrollRoutine != null) StopCoroutine(logScrollRoutine);
            logScrollRoutine = StartCoroutine(PinLogToBottomRoutine());
        }

        private IEnumerator PinLogToBottomRoutine()
        {
            yield return null;
            PinLogToBottom();
            yield return new WaitForEndOfFrame();
            PinLogToBottom();
            logScrollRoutine = null;
        }

        private void PinLogToBottom()
        {
            if (logScrollRect == null) return;

            var content = logScrollRect.content != null
                ? logScrollRect.content
                : logText != null ? logText.rectTransform.parent as RectTransform : null;
            if (content == null) return;

            Canvas.ForceUpdateCanvases();
            if (logText != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(logText.rectTransform);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();

            var view = logScrollRect.viewport != null
                ? logScrollRect.viewport
                : logScrollRect.GetComponent<RectTransform>();
            float max = 0f;
            if (view != null)
            {
                max = Mathf.Max(0f, content.rect.height - view.rect.height);
            }

            content.anchoredPosition = new Vector2(content.anchoredPosition.x, max);
        }

        private void ClearLogs()
        {
            logBuilder.Clear();
            logLineCount = 0;
            if (logText != null) logText.text = string.Empty;
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void BindToggle(Toggle toggle, UnityEngine.Events.UnityAction<bool> action)
        {
            if (toggle == null) return;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(action);
        }
    }
}
