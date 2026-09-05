using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Homepad.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class KocomHexTestUI : MonoBehaviour
    {
        private static readonly Color PresetRowA = new Color(0.165f, 0.176f, 0.204f, 1f);
        private static readonly Color PresetRowB = new Color(0.141f, 0.153f, 0.176f, 1f);
        private static readonly Color TabActiveColor = new Color(0.255f, 0.278f, 0.318f, 1f);
        private static readonly Color TabInactiveColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color TabInactiveTextColor = new Color(0.62f, 0.65f, 0.71f, 1f);
        private static readonly Color SoftSendBlue = new Color(0.455f, 0.612f, 0.773f, 1f);
        private static readonly Color MutedGreen = new Color(0.337f, 0.588f, 0.408f, 1f);
        private static readonly Color MutedRed = new Color(0.753f, 0.337f, 0.337f, 1f);
        private static readonly Color HexCodeCyan = new Color(0.43f, 0.65f, 0.84f, 1f);
        private static readonly Color LogSelectionColor = new Color(0.32f, 0.52f, 0.82f, 0.4f);
        private static readonly Color HexTableLine = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color HexTableFill = new Color(0.12f, 0.13f, 0.15f, 1f);
        private static readonly Color HexTableHex = new Color(0.90f, 0.90f, 0.90f, 1f);
        private static readonly Color HexTableLabel = new Color(0.66f, 0.66f, 0.66f, 1f);

        [Header("Serial Connection")]
        [SerializeField] private InputField portField;
        [SerializeField] private InputField baudField;
        [SerializeField] private Text baudLabel;
        [SerializeField] private Text statusText;
        [SerializeField] private Image statusDot;
        [SerializeField] private Button wallpadButton;
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

        [Header("Tabs")]
        [SerializeField] private Button tabAllButton;
        [SerializeField] private Button tabLightingButton;
        [SerializeField] private Button tabHeatingButton;
        [SerializeField] private Button tabVentButton;
        [SerializeField] private Button tabDoorButton;
        [SerializeField] private Button reloadPresetsButton;

        [Header("Custom HEX Input")]
        [SerializeField] private InputField customHexInput;
        [SerializeField] private Button fixChecksumButton;
        [SerializeField] private Button sendCustomButton;

        [Header("Presets List")]
        [SerializeField] private ScrollRect presetScrollRect;
        [SerializeField] private Transform presetContainer;
        [SerializeField] private GameObject presetItemPrefab;

        [Header("Log Panel")]
        [SerializeField] private Text logText;
        [SerializeField] private ScrollRect logScrollRect;
        [SerializeField] private Button clearLogButton;

        [Header("Font & Appearance")]
        [SerializeField] private Font customFont;
        [SerializeField] private Font customFontBold;
        [SerializeField] private Sprite roundedSprite;

        private readonly StringBuilder logBuilder = new StringBuilder();
        private int logLineCount;
        private string[] ports = new string[0];
        private int portIndex;
        private const int MaxLogLines = 80;
        private HexCategory currentCategory = HexCategory.All;
        private Font uiFont;
        private Font uiFontBold;
        private ArduinoConnector connector;
        private string[] lastSeenPorts = new string[0];
        private bool autoConnectAttempted;
        private bool logDragActive;
        private Vector2 logDragStart;
        private RectTransform logHighlightRoot;
        private readonly List<Image> logHighlightPool = new List<Image>();
        private int logSelFrom = -1;
        private int logSelTo = -1;
        private float logSelXFrom;
        private float logSelXTo;
        private readonly List<UILineInfo> logLineInfos = new List<UILineInfo>();
        private readonly List<GameObject> logEntries = new List<GameObject>();
        private readonly List<string> logPlainEntries = new List<string>();
        private bool logFollowTail = true;
        private KocomLinkDevice linkDevice = KocomLinkDevice.Arduino;
        private ArduinoLinkMode linkProtocol = ArduinoLinkMode.Serial;
        private ArduinoLinkMode arduinoProtocol = ArduinoLinkMode.Serial;
        private ArduinoLinkMode ew11Protocol = ArduinoLinkMode.Tcp;
        private GameObject mqttBar;
        private RectTransform leftPanelRt;
        private RectTransform rightPanelRt;
        private float panelsMaxY = 0.86f;
        private bool linkUiApplying;

        private void Awake()
        {
            AutoResolveUiReferences();
            EnsureLinkUi();
            BindEvents();
            HookConnector();
            EnsureLogLayout();
            UiInputBootstrap.GiveMouseToUi();
        }

        private void Start()
        {
            uiFont = customFont != null
                ? customFont
                : (statusText != null && statusText.font != null)
                    ? statusText.font
#if UNITY_EDITOR
                    : UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Pretendard-Regular.otf");
#else
                    : null;
#endif
            uiFontBold = customFontBold != null
                ? customFontBold
#if UNITY_EDITOR
                : UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Pretendard-SemiBold.otf");
#else
                : uiFont;
#endif

            if (customHexInput != null && string.IsNullOrEmpty(customHexInput.text))
            {
                customHexInput.text = "AA 55 30 BC 00 0E 00 01 00 00 FF 00 00 00 00 00 00 00 FA 0D 0D";
            }

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
            PopulatePresetList(HexCategory.All);
            SwitchCategory(HexCategory.All, tabAllButton);
            ApplyLinkUi();
            UiInputBootstrap.GiveMouseToUi();

            if (ports.Length > 0)
            {
                TryAutoConnect();
            }

            StartCoroutine(WatchUsbRoutine());
        }


        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                logDragActive = IsPointerOverLog(pos);
                if (logDragActive)
                {
                    logDragStart = pos;
                    UpdateLogSelection(pos, pos, false);
                }
                else
                {
                    ClearLogSelection();
                }
            }

            if (logDragActive && mouse.leftButton.isPressed)
            {
                UpdateLogSelection(logDragStart, pos, false);
            }

            if (logDragActive && mouse.leftButton.wasReleasedThisFrame)
            {
                logDragActive = false;
                UpdateLogSelection(logDragStart, pos, true);
            }

            CopyLogIfShortcutPressed();
            HandleLogMouseScroll(pos, mouse);
        }

        private void HookConnector()
        {
            var serial = GetConnector();
            if (serial == null) return;

            serial.OnConnectionStatusChanged -= UpdateStatus;
            serial.OnLogMessage -= AppendLog;
            serial.OnPacketReceived -= OnPacketReceived;
            serial.OnConnectionStatusChanged += UpdateStatus;
            serial.OnLogMessage += AppendLog;
            serial.OnPacketReceived += OnPacketReceived;
            UpdateStatus(serial.IsConnected);
        }

        private void OnDestroy()
        {
            if (connector == null) return;
            connector.OnConnectionStatusChanged -= UpdateStatus;
            connector.OnLogMessage -= AppendLog;
            connector.OnPacketReceived -= OnPacketReceived;
        }

        private ArduinoConnector GetConnector()
        {
            if (connector != null) return connector;

            if (WallpadManager.Instance != null && WallpadManager.Instance.Connector != null)
            {
                connector = WallpadManager.Instance.Connector;
                return connector;
            }

            connector = FindFirstObjectByType<ArduinoConnector>();
            if (connector != null) return connector;

            var go = new GameObject("ArduinoSerial");
            connector = go.AddComponent<ArduinoConnector>();
            AppendLog("[시스템] 씬에 ArduinoConnector가 없어 시리얼 브리지를 생성했습니다.", false);
            return connector;
        }

        private void AutoResolveUiReferences()
        {
            if (portField == null) portField = FindUi<InputField>("Port");
            if (baudField == null) baudField = FindUi<InputField>("Baud");
            if (baudLabel == null) baudLabel = FindUi<Text>("BaudLabel");
            if (statusText == null) statusText = FindUi<Text>("Status");
            if (statusDot == null) statusDot = FindUi<Image>("StatusDot");
            if (wallpadButton == null) wallpadButton = FindUi<Button>("WallpadScene");
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

            if (tabAllButton == null) tabAllButton = FindUi<Button>("TabAll");
            if (tabLightingButton == null) tabLightingButton = FindUi<Button>("TabLighting");
            if (tabHeatingButton == null) tabHeatingButton = FindUi<Button>("TabHeating");
            if (tabVentButton == null) tabVentButton = FindUi<Button>("TabVent");
            if (tabDoorButton == null) tabDoorButton = FindUi<Button>("TabDoor");
            if (reloadPresetsButton == null) reloadPresetsButton = FindUi<Button>("TabReload");

            if (customHexInput == null) customHexInput = FindUi<InputField>("CustomHexInput");
            if (fixChecksumButton == null) fixChecksumButton = FindUi<Button>("FixChecksum");
            if (sendCustomButton == null) sendCustomButton = FindUi<Button>("SendCustom");

            if (presetScrollRect == null) presetScrollRect = FindUi<ScrollRect>("PresetScrollView");
            if (presetContainer == null && presetScrollRect != null && presetScrollRect.content != null)
            {
                presetContainer = presetScrollRect.content;
            }

            if (logText == null) logText = FindUi<Text>("Log");
            if (logScrollRect == null) logScrollRect = FindUi<ScrollRect>("LogScrollView");
            if (clearLogButton == null) clearLogButton = FindUi<Button>("ClearLog");

            if (connectButton == null || refreshButton == null || logText == null)
            {
                Debug.LogError("[KocomHexTestUI] UI 참조를 찾지 못했습니다. Connect/Refresh/Log를 확인하세요.");
            }
        }

        private void EnsureLogLayout()
        {
            if (logText == null) return;

            logText.raycastTarget = false;
            logText.supportRichText = true;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;
            logText.alignment = TextAnchor.UpperLeft;
            logText.enabled = false;

            var logLe = logText.GetComponent<LayoutElement>();
            if (logLe == null) logLe = logText.gameObject.AddComponent<LayoutElement>();
            logLe.ignoreLayout = true;
            logLe.minHeight = 0f;
            logLe.preferredHeight = 0f;

            var content = LogScrollContent();
            if (content != null)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.offsetMin = new Vector2(0f, content.offsetMin.y);
                content.offsetMax = new Vector2(0f, 0f);

                var vg = content.GetComponent<VerticalLayoutGroup>();
                if (vg == null) vg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vg.padding = new RectOffset(12, 12, 8, 8);
                vg.spacing = 10f;
                vg.childAlignment = TextAnchor.UpperLeft;
                vg.childControlWidth = true;
                vg.childControlHeight = true;
                vg.childForceExpandWidth = true;
                vg.childForceExpandHeight = false;

                var fitter = content.GetComponent<ContentSizeFitter>();
                if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.enabled = true;
            }

            if (logScrollRect != null)
            {
                logScrollRect.movementType = ScrollRect.MovementType.Clamped;
                logScrollRect.scrollSensitivity = 30f;
                if (logScrollRect.viewport == null)
                {
                    logScrollRect.viewport = logScrollRect.GetComponent<RectTransform>();
                }
            }

            EnsureLogHighlightRoot();
            if (logHighlightRoot != null)
            {
                var hlLe = logHighlightRoot.GetComponent<LayoutElement>();
                if (hlLe == null) hlLe = logHighlightRoot.gameObject.AddComponent<LayoutElement>();
                hlLe.ignoreLayout = true;
            }
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

        private T FindUi<T>(string objectName) where T : Component
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                {
                    var comp = transforms[i].GetComponent<T>();
                    if (comp != null) return comp;
                }
            }
            return null;
        }

        private void BindEvents()
        {
            // Serial Controls
            Bind(wallpadButton, () => SceneManager.LoadScene("WallpadMain"));
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

            // Tabs
            Bind(tabAllButton, () => SwitchCategory(HexCategory.All, tabAllButton));
            Bind(tabLightingButton, () => SwitchCategory(HexCategory.Lighting, tabLightingButton));
            Bind(tabHeatingButton, () => SwitchCategory(HexCategory.Heating, tabHeatingButton));
            Bind(tabVentButton, () => SwitchCategory(HexCategory.Ventilation, tabVentButton));
            Bind(tabDoorButton, () => SwitchCategory(HexCategory.DoorLock, tabDoorButton));
            Bind(reloadPresetsButton, ReloadPresetsFromMarkdown);

            // Custom Hex
            Bind(fixChecksumButton, OnFixChecksumClicked);
            Bind(sendCustomButton, OnSendCustomClicked);

            // Log
            Bind(clearLogButton, ClearLog);
        }

        public void SwitchCategory(HexCategory category, Button clickedTab = null)
        {
            currentCategory = category;
            UpdateTabColors(clickedTab ?? GetTabButton(category));
            PopulatePresetList(category);
        }

        private Button GetTabButton(HexCategory category)
        {
            return category switch
            {
                HexCategory.All => tabAllButton,
                HexCategory.Lighting => tabLightingButton,
                HexCategory.Heating => tabHeatingButton,
                HexCategory.Ventilation => tabVentButton,
                HexCategory.DoorLock => tabDoorButton,
                _ => tabAllButton
            };
        }

        private void UpdateTabColors(Button activeTab)
        {
            Button[] tabs = { tabAllButton, tabLightingButton, tabHeatingButton, tabVentButton, tabDoorButton, reloadPresetsButton };
            foreach (var tab in tabs)
            {
                if (tab == null) continue;
                var img = tab.GetComponent<Image>();
                var txt = tab.GetComponentInChildren<Text>();
                bool isActive = tab == activeTab;
                if (img != null)
                {
                    img.color = isActive ? TabActiveColor : TabInactiveColor;
                }
                if (txt != null)
                {
                    txt.color = isActive ? Color.white : TabInactiveTextColor;
                }
            }
        }

        public void ReloadPresetsFromMarkdown()
        {
            KocomHexPresets.Reload();
            PopulatePresetList(currentCategory);
            string path = KocomMarkdownParser.FindMarkdownPath() ?? KocomMarkdownParser.UserMarkdownPath;
            AppendLog($"<color=#5CAE7C>[MD 로드 완료] {path} 에서 {KocomHexPresets.AllPresets.Count}개 프리셋 로드됨</color>", false);
        }

        private void PopulatePresetList(HexCategory category)
        {
            if (presetContainer == null) return;

            var vg = presetContainer.GetComponent<VerticalLayoutGroup>();
            if (vg != null)
            {
                vg.padding = new RectOffset(8, 8, 8, 8);
                vg.childControlWidth = true;
                vg.childControlHeight = true;
                vg.childForceExpandWidth = true;
                vg.childForceExpandHeight = false;
                vg.spacing = 6f;
            }

            for (int i = presetContainer.childCount - 1; i >= 0; i--)
            {
                InspectorSafeDestroy.GameObject(presetContainer.GetChild(i).gameObject);
            }

            var presets = KocomHexPresets.GetPresetsByCategory(category);
            int rowIndex = 0;

            foreach (var preset in presets)
            {
                GameObject rowObj;
                if (presetItemPrefab != null)
                {
                    rowObj = Instantiate(presetItemPrefab, presetContainer);
                    rowObj.hideFlags = HideFlags.DontSave;
                }
                else
                {
                    rowObj = CreatePresetRowObject(presetContainer, preset, rowIndex);
                }

                rowIndex++;
                rowObj.name = $"Preset_{preset.id}";

                var titleText = rowObj.transform.Find("Title")?.GetComponent<Text>();
                var descText = rowObj.transform.Find("Desc")?.GetComponent<Text>();
                var hexText = rowObj.transform.Find("Hex")?.GetComponent<Text>();
                var sendBtn = rowObj.GetComponentInChildren<Button>();

                if (titleText != null) titleText.text = preset.title;
                if (descText != null) descText.text = preset.description;
                if (hexText != null) hexText.text = preset.hexString;

                if (sendBtn != null)
                {
                    var p = preset;
                    Bind(sendBtn, () => SendPreset(p));
                }
            }

            if (presetScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                presetScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        public void SendPreset(HexPreset preset)
        {
            if (preset == null) return;
            AppendLog($"<b>{preset.title}</b>", true, KocomHexPresets.HexStringToBytes(preset.hexString));
            SendRawHex(preset.hexString);
        }

        public void SendRawHex(string hexString)
        {
            byte[] bytes = KocomHexPresets.HexStringToBytes(hexString);
            if (bytes == null || bytes.Length == 0)
            {
                AppendLog("<color=#CF5C5C>[오류] 유효하지 않은 HEX 문자열입니다.</color>", false);
                return;
            }

            var connector = GetConnector();
            if (connector != null)
            {
                connector.SendPacket(bytes);
            }
            else
            {
                AppendLog("<color=#5A98D4>[시뮬레이션 전송]</color>", false, bytes);
            }
        }

        private void OnFixChecksumClicked()
        {
            if (customHexInput == null) return;
            string corrected = KocomHexPresets.RecalculateChecksum(customHexInput.text);
            customHexInput.text = corrected;
            AppendLog($"<color=#9EA6B5>[체크섬 계산 완료] {corrected}</color>", false);
        }

        private void OnSendCustomClicked()
        {
            if (customHexInput == null) return;
            string hex = customHexInput.text.Trim();
            if (string.IsNullOrEmpty(hex)) return;

            AppendLog("<b>[커스텀 직접전송]</b>", true, KocomHexPresets.HexStringToBytes(hex));
            SendRawHex(hex);
        }

        private void OnPacketReceived(byte[] packet)
        {
            if (packet == null || packet.Length < KocomProtocol.PacketSize) return;

            if (KocomProtocol.TryParse(packet, out var frame))
            {
                string decoded = KocomProtocol.DecodeFrame(frame);
                AppendLog($"<color=#5CAE7C>[RX 수신] {decoded}</color>", false, packet);
            }
            else
            {
                AppendLog("<color=#E5B550>[RX 알수없는 패킷]</color>", false, packet);
            }
        }

        public void ClearLog()
        {
            logBuilder.Clear();
            logLineCount = 0;
            for (int i = 0; i < logEntries.Count; i++)
            {
                if (logEntries[i] != null) InspectorSafeDestroy.GameObject(logEntries[i]);
            }

            logEntries.Clear();
            logPlainEntries.Clear();
            if (logText != null) logText.text = string.Empty;
            logFollowTail = true;
            ClearLogSelection();
        }

        private void AppendLog(string message, bool isTx)
        {
            AppendLog(message, isTx, null);
        }

        private void AppendLog(string message, bool isTx, byte[] packet)
        {
            string color = isTx ? "#FFFFFF" : "#CCCCCC";
            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            string line = $"<color=#7E8794>[{time}]</color> <color={color}>{message}</color>";

            string plain = StripRichText(line);
            if (packet != null && packet.Length >= KocomProtocol.PacketSize)
            {
                plain += "\n" + KocomProtocol.FormatHexTable(packet);
            }

            logPlainEntries.Add(plain);
            logLineCount++;
            CreateLogEntry(line, packet);

            while (logEntries.Count > MaxLogLines)
            {
                var oldest = logEntries[0];
                logEntries.RemoveAt(0);
                if (logPlainEntries.Count > 0) logPlainEntries.RemoveAt(0);
                if (oldest != null)
                {
                    oldest.transform.SetParent(null, false);
                    InspectorSafeDestroy.GameObject(oldest);
                }

                logLineCount--;
            }

            RebuildLogBuilder();
            if (logFollowTail) PinLogToBottom();
            ClearLogSelection();
        }

        private void RebuildLogBuilder()
        {
            logBuilder.Clear();
            for (int i = 0; i < logPlainEntries.Count; i++)
            {
                logBuilder.Append(logPlainEntries[i]).Append('\n');
            }
        }

        private void CreateLogEntry(string headerRich, byte[] packet)
        {
            var content = LogScrollContent();
            if (content == null) return;

            var entry = new GameObject("LogEntry", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            entry.hideFlags = HideFlags.DontSave;
            entry.transform.SetParent(content, false);
            logEntries.Add(entry);

            var vg = entry.GetComponent<VerticalLayoutGroup>();
            vg.padding = new RectOffset(0, 0, 0, 0);
            vg.spacing = 6f;
            vg.childAlignment = TextAnchor.UpperLeft;
            vg.childControlWidth = true;
            vg.childControlHeight = true;
            vg.childForceExpandWidth = true;
            vg.childForceExpandHeight = false;

            var le = entry.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var header = CreateUiText(entry.transform, "Header", HeaderFont(), 18, new Color(0.8f, 0.8f, 0.8f, 1f));
            header.supportRichText = true;
            header.alignment = TextAnchor.UpperLeft;
            header.horizontalOverflow = HorizontalWrapMode.Wrap;
            header.verticalOverflow = VerticalWrapMode.Overflow;
            header.text = headerRich;

            var headerLe = header.gameObject.AddComponent<LayoutElement>();
            headerLe.minHeight = 24f;
            headerLe.flexibleWidth = 1f;
            var headerFit = header.gameObject.AddComponent<ContentSizeFitter>();
            headerFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            headerFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (packet != null && packet.Length >= KocomProtocol.PacketSize)
            {
                BuildHexTable(entry.transform, packet);
            }
        }

        private Font HeaderFont()
        {
            if (uiFont != null) return uiFont;
            if (logText != null && logText.font != null) return logText.font;
            return uiFontBold;
        }

        private Font TableHexFont()
        {
            if (uiFontBold != null) return uiFontBold;
            return HeaderFont();
        }

        private void BuildHexTable(Transform parent, byte[] packet)
        {
            var fields = KocomProtocol.GetHexTableFields(packet);
            if (fields == null || fields.Length == 0) return;

            var table = new GameObject("HexTable", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            table.hideFlags = HideFlags.DontSave;
            table.transform.SetParent(parent, false);

            var tableImg = table.GetComponent<Image>();
            tableImg.color = HexTableLine;
            tableImg.raycastTarget = false;

            var hlg = table.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(1, 1, 1, 1);
            hlg.spacing = 1f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var tableLe = table.GetComponent<LayoutElement>();
            tableLe.minHeight = 58f;
            tableLe.preferredHeight = 58f;
            tableLe.flexibleWidth = 1f;

            for (int i = 0; i < fields.Length; i++)
            {
                BuildHexTableCell(table.transform, fields[i]);
            }
        }

        private void BuildHexTableCell(Transform parent, KocomProtocol.HexTableField field)
        {
            var cell = new GameObject(field.Label, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            cell.hideFlags = HideFlags.DontSave;
            cell.transform.SetParent(parent, false);

            var img = cell.GetComponent<Image>();
            img.color = HexTableFill;
            img.raycastTarget = false;

            var vlg = cell.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.spacing = 0f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var le = cell.GetComponent<LayoutElement>();
            if (field.Expand)
            {
                le.minWidth = 150f;
                le.flexibleWidth = 1f;
            }
            else
            {
                float min = field.Caption.Length >= 8 ? 96f : 52f;
                le.minWidth = min;
                le.preferredWidth = min;
                le.flexibleWidth = 0.12f;
            }

            var hex = CreateUiText(cell.transform, "Hex", TableHexFont(), 16, HexTableHex);
            hex.alignment = TextAnchor.MiddleCenter;
            hex.horizontalOverflow = HorizontalWrapMode.Overflow;
            hex.text = field.Hex;
            var hexLe = hex.gameObject.AddComponent<LayoutElement>();
            hexLe.minHeight = 22f;
            hexLe.preferredHeight = 22f;

            var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rule.hideFlags = HideFlags.DontSave;
            rule.transform.SetParent(cell.transform, false);
            var ruleImg = rule.GetComponent<Image>();
            ruleImg.color = HexTableLine;
            ruleImg.raycastTarget = false;
            var ruleLe = rule.GetComponent<LayoutElement>();
            ruleLe.minHeight = 1f;
            ruleLe.preferredHeight = 1f;
            ruleLe.flexibleWidth = 1f;

            var cap = CreateUiText(cell.transform, "Caption", HeaderFont(), 16, HexTableLabel);
            cap.alignment = TextAnchor.MiddleCenter;
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            cap.text = field.Caption;
            var capLe = cap.gameObject.AddComponent<LayoutElement>();
            capLe.minHeight = 22f;
            capLe.preferredHeight = 22f;
        }

        private static Text CreateUiText(Transform parent, string name, Font font, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = Mathf.Max(16, fontSize);
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void HandleLogMouseScroll(Vector2 screenPos, Mouse mouse)
        {
            if (!IsPointerOverLog(screenPos)) return;
            float dy = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(dy) < 0.01f) return;

            var content = LogScrollContent();
            if (content == null) return;

            float y = content.anchoredPosition.y - dy * 30f;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, ClampLogScroll(y));
            logFollowTail = IsLogNearBottom();
        }

        private void PinLogToBottom()
        {
            var content = LogScrollContent();
            if (content == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, ClampLogScroll(float.MaxValue));
        }

        private bool IsLogNearBottom()
        {
            var content = LogScrollContent();
            if (content == null) return true;
            return content.anchoredPosition.y >= LogScrollMax() - 24f;
        }

        private RectTransform LogScrollContent()
        {
            if (logScrollRect == null) return null;
            return logScrollRect.content != null ? logScrollRect.content : logText != null ? logText.transform.parent as RectTransform : null;
        }

        private float LogScrollMax()
        {
            var content = LogScrollContent();
            var view = logScrollRect != null
                ? (logScrollRect.viewport != null ? logScrollRect.viewport : logScrollRect.GetComponent<RectTransform>())
                : null;
            if (content == null || view == null) return 0f;
            return Mathf.Max(0f, content.rect.height - view.rect.height);
        }

        private float ClampLogScroll(float y)
        {
            return Mathf.Clamp(y, 0f, LogScrollMax());
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
                AppendLog("<color=#CF5C5C>[오류] ArduinoConnector를 만들 수 없습니다.</color>", false);
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
                AppendLog("<color=#9EA6B5>[안내] EW-11은 TCP(기본 포트 8899) 또는 MQTT로 붙습니다. 장치 IP를 입력한 뒤 연결하세요. MQTT면 EW-11 구독 토픽=송신, 발행 토픽=수신입니다.</color>", false);
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
            if (device == KocomLinkDevice.Arduino)
            {
                return ClampArduinoProtocol(arduinoProtocol);
            }

            return ClampEw11Protocol(ew11Protocol);
        }

        private string DeviceLabel()
        {
            return linkDevice == KocomLinkDevice.Ew11 ? "EW-11" : "아두이노";
        }

        private string ProtocolLabel()
        {
            return linkProtocol switch
            {
                ArduinoLinkMode.Tcp => "TCP",
                ArduinoLinkMode.Mqtt => "MQTT",
                _ => "시리얼"
            };
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

        private void EnsureLinkUi()
        {
            if (arduinoToggle == null) arduinoToggle = FindUi<Toggle>("DeviceArduino");
            if (ew11Toggle == null) ew11Toggle = FindUi<Toggle>("DeviceEw11");
            if (serialToggle == null) serialToggle = FindUi<Toggle>("ProtoSerial");
            if (tcpToggle == null) tcpToggle = FindUi<Toggle>("ProtoTcp");
            if (mqttToggle == null) mqttToggle = FindUi<Toggle>("ProtoMqtt");
            if (mqttUserField == null) mqttUserField = FindUi<InputField>("MqttUser");
            if (mqttPassField == null) mqttPassField = FindUi<InputField>("MqttPass");
            if (mqttTxField == null) mqttTxField = FindUi<InputField>("MqttTx");
            if (mqttRxField == null) mqttRxField = FindUi<InputField>("MqttRx");

            leftPanelRt = FindUi<RectTransform>("LeftPanel");
            rightPanelRt = FindUi<RectTransform>("RightPanel");
            if (leftPanelRt != null) panelsMaxY = leftPanelRt.anchorMax.y;

            var mqttRt = FindUi<RectTransform>("MqttBar");
            if (mqttRt != null) mqttBar = mqttRt.gameObject;
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
            SetPortFieldWidth(serial);

            if (mqttBar != null) mqttBar.SetActive(mqtt);
            float maxY = mqtt ? 0.78f : panelsMaxY;
            SetPanelMaxY(leftPanelRt, maxY);
            SetPanelMaxY(rightPanelRt, maxY);

            if (portField != null && portField.placeholder is Text portPh)
            {
                portPh.fontStyle = FontStyle.Normal;
                portPh.text = serial ? "시리얼 포트" : (mqtt ? "MQTT 브로커 IP" : "장치 IP");
            }

            if (baudField != null && baudField.placeholder is Text baudPh)
            {
                baudPh.fontStyle = FontStyle.Normal;
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
                    host = PlayerPrefs.GetString("Homepad.TcpHost", "192.168.0.100");
                }

                if (string.IsNullOrEmpty(host) || LooksLikeSerialPort(host)) host = "192.168.0.100";
                portField.text = host;
                if (baudField != null) baudField.text = port > 0 ? port.ToString() : "1883";
            }
            else
            {
                string host = PlayerPrefs.GetString("Homepad.TcpHost", "192.168.0.100");
                int port = PlayerPrefs.GetInt("Homepad.TcpPort", DefaultTcpPort());
                if (LooksLikeSerialPort(host)) host = "192.168.0.100";
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

        private void SetPortFieldWidth(bool serial)
        {
            var rt = portField != null ? portField.GetComponent<RectTransform>() : null;
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.438f, 0.2f);
            rt.anchorMax = new Vector2(serial ? 0.522f : 0.636f, 0.8f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void SetPanelMaxY(RectTransform panel, float maxY)
        {
            if (panel == null) return;
            var max = panel.anchorMax;
            max.y = maxY;
            panel.anchorMax = max;
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

        private static void BindToggle(Toggle toggle, UnityEngine.Events.UnityAction<bool> action)
        {
            if (toggle == null) return;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(action);
        }

        private void CopyLogIfShortcutPressed()
        {
            if (IsTypingInInputField()) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            bool copyChord = kb.cKey.wasPressedThisFrame && (
                kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed ||
                kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
            if (!copyChord) return;

            if (logSelFrom >= 0)
            {
                CopyCurrentLogSelection();
                return;
            }

            CopyPlainText(StripRichText(logBuilder.ToString()));
        }

        private bool IsTypingInInputField()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var field = selected.GetComponent<InputField>();
            return field != null && field.isFocused && !field.readOnly;
        }

        private Camera LogCanvasCamera()
        {
            Canvas canvas = logText != null
                ? logText.canvas
                : (logScrollRect != null ? logScrollRect.GetComponentInParent<Canvas>() : null);
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera;
        }

        private bool IsPointerOverLog(Vector2 screenPos)
        {
            if (logScrollRect == null) return false;
            var area = logScrollRect.GetComponent<RectTransform>();
            return area != null && RectTransformUtility.RectangleContainsScreenPoint(area, screenPos, LogCanvasCamera());
        }

        private void UpdateLogSelection(Vector2 screenStart, Vector2 screenEnd, bool copy)
        {
            if (logText == null || !RefreshLogLineInfos())
            {
                ClearLogSelection();
                return;
            }

            int a = LogLineIndexAt(screenStart);
            int b = LogLineIndexAt(screenEnd);
            if (a < 0 && b < 0)
            {
                ClearLogSelection();
                return;
            }

            if (a < 0) a = b;
            if (b < 0) b = a;

            logSelFrom = a;
            logSelTo = b;
            logSelXFrom = LogXFromLeft(screenStart);
            logSelXTo = LogXFromLeft(screenEnd);
            RebuildLogHighlights((screenStart - screenEnd).sqrMagnitude < 64f);
            if (copy) CopyCurrentLogSelection();
        }

        private void CopyCurrentLogSelection()
        {
            if (logText == null || !RefreshLogLineInfos() || logSelFrom < 0) return;

            int from = Mathf.Clamp(Mathf.Min(logSelFrom, logSelTo), 0, logLineInfos.Count - 1);
            int to = Mathf.Clamp(Mathf.Max(logSelFrom, logSelTo), 0, logLineInfos.Count - 1);
            string raw = logText.text ?? string.Empty;
            int start = Mathf.Clamp(logLineInfos[from].startCharIdx, 0, raw.Length);
            int end = to + 1 < logLineInfos.Count
                ? Mathf.Clamp(logLineInfos[to + 1].startCharIdx, start, raw.Length)
                : raw.Length;
            if (end < start) end = raw.Length;

            string selected = StripRichText(raw.Substring(start, end - start)).Trim();
            if (selected.Length == 0) selected = StripRichText(raw).TrimEnd();
            CopyPlainText(selected);
        }

        private bool RefreshLogLineInfos()
        {
            logLineInfos.Clear();
            if (logText == null || string.IsNullOrEmpty(logText.text)) return false;

            var gen = logText.cachedTextGenerator;
            if (gen == null) return false;
            if (gen.lineCount == 0)
            {
                Vector2 size = logText.rectTransform.rect.size;
                if (size.x < 8f) size.x = 8f;
                gen.Populate(logText.text, logText.GetGenerationSettings(size));
            }

            int n = gen.lineCount;
            for (int i = 0; i < n; i++)
            {
                logLineInfos.Add(gen.lines[i]);
            }

            return logLineInfos.Count > 0;
        }

        private float LogPixelsPerUnit()
        {
            return logText != null ? Mathf.Max(logText.pixelsPerUnit, 0.01f) : 1f;
        }

        private int LogLineIndexAt(Vector2 screenPos)
        {
            if (logText == null || logLineInfos.Count == 0) return -1;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(logText.rectTransform, screenPos, LogCanvasCamera(), out var local))
            {
                return -1;
            }

            float ppu = LogPixelsPerUnit();
            for (int i = 0; i < logLineInfos.Count; i++)
            {
                float top = logLineInfos[i].topY / ppu;
                float bot = top - logLineInfos[i].height / ppu;
                if (local.y <= top + 0.5f && local.y >= bot - 0.5f) return i;
            }

            if (local.y > logLineInfos[0].topY / ppu) return 0;
            return logLineInfos.Count - 1;
        }

        private float LogXFromLeft(Vector2 screenPos)
        {
            if (logText == null) return 0f;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(logText.rectTransform, screenPos, LogCanvasCamera(), out var local))
            {
                return 0f;
            }

            float width = Mathf.Max(logText.rectTransform.rect.width, 1f);
            return Mathf.Clamp(local.x + width * 0.5f, 0f, width);
        }

        private void EnsureLogHighlightRoot()
        {
            if (logText == null || logHighlightRoot != null) return;

            var go = new GameObject("LogHighlight", typeof(RectTransform));
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(logText.transform.parent, false);
            go.transform.SetAsFirstSibling();
            logHighlightRoot = go.GetComponent<RectTransform>();
            SyncLogHighlightRoot();
        }

        private void SyncLogHighlightRoot()
        {
            if (logHighlightRoot == null || logText == null) return;
            var src = logText.rectTransform;
            var rt = logHighlightRoot;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.anchoredPosition = src.anchoredPosition;
            rt.sizeDelta = src.sizeDelta;
            rt.offsetMin = src.offsetMin;
            rt.offsetMax = src.offsetMax;
            int logIndex = logText.transform.GetSiblingIndex();
            if (logHighlightRoot.GetSiblingIndex() > logIndex)
            {
                logHighlightRoot.SetSiblingIndex(logIndex);
            }
        }

        private Image GetLogHighlight(int index)
        {
            EnsureLogHighlightRoot();
            while (logHighlightPool.Count <= index)
            {
                logHighlightPool.Add(CreateLogHighlight());
            }

            if (logHighlightPool[index] == null)
            {
                logHighlightPool[index] = CreateLogHighlight();
            }

            return logHighlightPool[index];
        }

        private Image CreateLogHighlight()
        {
            var go = new GameObject("Sel", typeof(RectTransform), typeof(Image));
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(logHighlightRoot, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = LogSelectionColor;
            img.sprite = roundedSprite != null
                ? roundedSprite
                : Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            if (roundedSprite != null)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 2.4f;
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return img;
        }

        private void RebuildLogHighlights(bool wholeLines)
        {
            if (logText == null || logSelFrom < 0 || !RefreshLogLineInfos())
            {
                ClearLogSelectionVisual();
                return;
            }

            SyncLogHighlightRoot();

            int from = Mathf.Clamp(Mathf.Min(logSelFrom, logSelTo), 0, logLineInfos.Count - 1);
            int to = Mathf.Clamp(Mathf.Max(logSelFrom, logSelTo), 0, logLineInfos.Count - 1);
            float width = Mathf.Max(logText.rectTransform.rect.width, 1f);
            float ppu = LogPixelsPerUnit();
            float xA = logSelFrom <= logSelTo ? logSelXFrom : logSelXTo;
            float xB = logSelFrom <= logSelTo ? logSelXTo : logSelXFrom;

            int used = 0;
            for (int i = from; i <= to; i++)
            {
                float xMin = 0f;
                float xMax = width;
                if (!wholeLines)
                {
                    if (from == to)
                    {
                        xMin = Mathf.Min(xA, xB);
                        xMax = Mathf.Max(xA, xB);
                        if (xMax - xMin < 10f)
                        {
                            xMin = 0f;
                            xMax = width;
                        }
                    }
                    else if (i == from)
                    {
                        xMin = xA;
                    }
                    else if (i == to)
                    {
                        xMax = xB;
                    }
                }

                var img = GetLogHighlight(used++);
                if (img == null) continue;
                img.gameObject.SetActive(true);
                var rt = img.rectTransform;
                float top = logLineInfos[i].topY / ppu;
                float lineH = Mathf.Max(logLineInfos[i].height / ppu, 8f);
                rt.anchoredPosition = new Vector2(xMin, top);
                rt.sizeDelta = new Vector2(Mathf.Max(8f, xMax - xMin), lineH);
            }

            for (int i = used; i < logHighlightPool.Count; i++)
            {
                logHighlightPool[i].gameObject.SetActive(false);
            }
        }

        private void ClearLogSelection()
        {
            logSelFrom = -1;
            logSelTo = -1;
            ClearLogSelectionVisual();
        }

        private void ClearLogSelectionVisual()
        {
            for (int i = 0; i < logHighlightPool.Count; i++)
            {
                if (logHighlightPool[i] != null) logHighlightPool[i].gameObject.SetActive(false);
            }
        }

        private static void CopyPlainText(string text)
        {
            GUIUtility.systemCopyBuffer = text ?? string.Empty;
        }

        private static string StripRichText(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            bool inTag = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (c == '>' && inTag)
                {
                    inTag = false;
                    continue;
                }

                if (!inTag) sb.Append(c);
            }

            return sb.ToString();
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private GameObject CreatePresetRowObject(Transform parent, HexPreset preset, int rowIndex)
        {
            GameObject row = new GameObject("PresetRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.hideFlags = HideFlags.DontSave;
            row.transform.SetParent(parent, false);
            var rowImg = row.GetComponent<Image>();
            if (roundedSprite != null)
            {
                rowImg.sprite = roundedSprite;
                rowImg.type = Image.Type.Sliced;
                rowImg.pixelsPerUnitMultiplier = 1.6f;
            }
            rowImg.color = (rowIndex % 2 == 0) ? PresetRowA : PresetRowB;

            var layout = row.GetComponent<LayoutElement>();
            layout.minHeight = 84f;
            layout.preferredHeight = 84f;
            layout.flexibleWidth = 1f;

            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.sizeDelta = new Vector2(0f, 84f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(row.transform, false);
            var title = titleGo.GetComponent<Text>();
            title.font = uiFontBold;
            title.fontSize = 22;
            title.fontStyle = FontStyle.Normal;
            title.text = preset.title;
            title.color = Color.white;
            title.alignment = TextAnchor.MiddleLeft;
            title.raycastTarget = false;

            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.46f);
            titleRt.anchorMax = new Vector2(0.80f, 1f);
            titleRt.offsetMin = new Vector2(18, 0);
            titleRt.offsetMax = new Vector2(-8, -2);

            var hexGo = new GameObject("Hex", typeof(RectTransform), typeof(Text));
            hexGo.transform.SetParent(row.transform, false);
            var hexText = hexGo.GetComponent<Text>();
            hexText.font = uiFont;
            hexText.fontSize = 16;
            hexText.text = preset.hexString;
            hexText.color = HexCodeCyan;
            hexText.alignment = TextAnchor.MiddleLeft;
            hexText.horizontalOverflow = HorizontalWrapMode.Overflow;
            hexText.raycastTarget = false;

            var hexRt = hexGo.GetComponent<RectTransform>();
            hexRt.anchorMin = new Vector2(0f, 0f);
            hexRt.anchorMax = new Vector2(0.80f, 0.50f);
            hexRt.offsetMin = new Vector2(18, 4);
            hexRt.offsetMax = new Vector2(-8, 0);

            GameObject sendBtnGo = new GameObject("SendButton", typeof(RectTransform), typeof(Image), typeof(Button));
            sendBtnGo.transform.SetParent(row.transform, false);
            var btnImg = sendBtnGo.GetComponent<Image>();
            if (roundedSprite != null)
            {
                btnImg.sprite = roundedSprite;
                btnImg.type = Image.Type.Sliced;
                btnImg.pixelsPerUnitMultiplier = 1.8f;
            }
            btnImg.color = SoftSendBlue;

            var sendRt = sendBtnGo.GetComponent<RectTransform>();
            sendRt.anchorMin = new Vector2(0.82f, 0.18f);
            sendRt.anchorMax = new Vector2(0.975f, 0.82f);
            sendRt.offsetMin = Vector2.zero;
            sendRt.offsetMax = Vector2.zero;

            var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            btnTextGo.transform.SetParent(sendBtnGo.transform, false);
            var btnText = btnTextGo.GetComponent<Text>();
            btnText.font = uiFontBold;
            btnText.fontSize = 18;
            btnText.fontStyle = FontStyle.Normal;
            btnText.text = "전송";
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.raycastTarget = false;

            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;

            return row;
        }
    }
}
