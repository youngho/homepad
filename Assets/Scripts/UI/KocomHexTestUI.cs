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

        [Header("Serial Connection")]
        [SerializeField] private InputField portField;
        [SerializeField] private InputField baudField;
        [SerializeField] private Text statusText;
        [SerializeField] private Image statusDot;
        [SerializeField] private Button wallpadButton;
        [SerializeField] private Button prevPortButton;
        [SerializeField] private Button nextPortButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;

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
        private const int MaxLogLines = 120;
        private HexCategory currentCategory = HexCategory.All;
        private Font uiFont;
        private Font uiFontBold;
        private ArduinoConnector connector;
        private string[] lastSeenPorts = new string[0];
        private readonly List<RaycastResult> raycastHits = new List<RaycastResult>();
        private int handledClickFrame = -1;
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

        private void Awake()
        {
            AutoResolveUiReferences();
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
                    : (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));
            uiFontBold = customFontBold != null ? customFontBold : uiFont;

            if (customHexInput != null && string.IsNullOrEmpty(customHexInput.text))
            {
                customHexInput.text = "AA 55 30 BC 00 0E 00 01 00 00 FF 00 00 00 00 00 00 00 FA 0D 0D";
            }

            RefreshPorts(true);
            lastSeenPorts = ports ?? new string[0];
            PopulatePresetList(HexCategory.All);
            SwitchCategory(HexCategory.All, tabAllButton);
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
                HandleUiPointerClick(pos);
                logDragActive = handledClickFrame != Time.frameCount && IsPointerOverLog(pos);
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
            if (statusText == null) statusText = FindUi<Text>("Status");
            if (statusDot == null) statusDot = FindUi<Image>("StatusDot");
            if (wallpadButton == null) wallpadButton = FindUi<Button>("WallpadScene");
            if (prevPortButton == null) prevPortButton = FindUi<Button>("PrevPort");
            if (nextPortButton == null) nextPortButton = FindUi<Button>("NextPort");
            if (refreshButton == null) refreshButton = FindUi<Button>("Refresh");
            if (connectButton == null) connectButton = FindUi<Button>("Connect");
            if (disconnectButton == null) disconnectButton = FindUi<Button>("Disconnect");

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

            var rt = logText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(12f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-12f, -8f);

            if (logScrollRect != null && logScrollRect.viewport == null)
            {
                logScrollRect.viewport = logScrollRect.GetComponent<RectTransform>();
            }

            EnsureLogHighlightRoot();
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
                        if (portField != null)
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

        private void HandleUiPointerClick(Vector2 screenPos)
        {
            if (handledClickFrame == Time.frameCount) return;
            var es = EventSystem.current;
            if (es == null) return;

            var eventData = new PointerEventData(es) { position = screenPos };
            raycastHits.Clear();
            es.RaycastAll(eventData, raycastHits);

            Button button = null;
            for (int i = 0; i < raycastHits.Count; i++)
            {
                button = raycastHits[i].gameObject.GetComponentInParent<Button>();
                if (button != null && button.interactable) break;
                button = null;
            }

            if (button == null) return;
            button.onClick.Invoke();
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
            AppendLog($"<color=#5CAE7C>[MD 로드 완료] kocom-hex.md 에서 {KocomHexPresets.AllPresets.Count}개 프리셋 로드됨</color>", false);
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
                Destroy(presetContainer.GetChild(i).gameObject);
            }

            var presets = KocomHexPresets.GetPresetsByCategory(category);
            int rowIndex = 0;

            foreach (var preset in presets)
            {
                GameObject rowObj;
                if (presetItemPrefab != null)
                {
                    rowObj = Instantiate(presetItemPrefab, presetContainer);
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
            AppendLog($"<b>{preset.title}</b>\n              <color=#6DA6D6>{preset.hexString}</color>", true);
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
                AppendLog($"<color=#5A98D4>[시뮬레이션 전송] {KocomProtocol.ToHexString(bytes)}</color>", false);
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

            AppendLog($"<b>[커스텀 직접전송]</b>\n              <color=#6DA6D6>{hex}</color>", true);
            SendRawHex(hex);
        }

        private void OnPacketReceived(byte[] packet)
        {
            if (packet == null || packet.Length < KocomProtocol.PacketSize) return;

            string hexStr = KocomProtocol.ToHexString(packet);
            if (KocomProtocol.TryParse(packet, out var frame))
            {
                string decoded = KocomProtocol.DecodeFrame(frame);
                AppendLog($"<color=#5CAE7C>[RX 수신] {decoded}</color>\n              <color=#7E8794>HEX: {hexStr}</color>", false);
            }
            else
            {
                AppendLog($"<color=#E5B550>[RX 알수없는 패킷] {hexStr}</color>", false);
            }
        }

        public void ClearLog()
        {
            logBuilder.Clear();
            logLineCount = 0;
            if (logText != null) logText.text = string.Empty;
            ClearLogSelection();
        }

        private void AppendLog(string message, bool isTx)
        {
            string color = isTx ? "#FFFFFF" : "#CCCCCC";
            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            string line = $"<color=#7E8794>[{time}]</color> <color={color}>{message}</color>\n";

            logBuilder.Append(line);
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
                logText.verticalOverflow = VerticalWrapMode.Overflow;
                logText.text = logBuilder.ToString();
                float height = Mathf.Max(logText.preferredHeight + 24f, 80f);
                logText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                var content = logText.transform.parent as RectTransform;
                if (content != null)
                {
                    content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                }
            }

            if (logScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                logScrollRect.verticalNormalizedPosition = 0f;
            }

            ClearLogSelection();
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

                AppendLog("<color=#CF5C5C>[시스템] USB 시리얼 포트를 찾지 못했습니다. 아두이노를 다시 꽂거나 IDE 시리얼 모니터를 닫은 뒤 새로고침하세요.</color>", false);
                return;
            }

            portIndex = 0;
            if (preferSaved)
            {
                int found = Array.IndexOf(ports, saved);
                if (found >= 0) portIndex = found;
            }

            if (portField != null) portField.text = ports[portIndex];
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
        }

        private void UpdateStatus(bool isConnected)
        {
            if (statusText != null)
            {
                statusText.text = isConnected ? "시리얼 연결됨" : "연결 안 됨";
                statusText.color = isConnected ? MutedGreen : MutedRed;
            }

            if (statusDot != null)
            {
                statusDot.color = isConnected ? MutedGreen : MutedRed;
            }
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
                var go = new GameObject("Sel", typeof(RectTransform), typeof(Image));
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
                logHighlightPool.Add(img);
            }

            return logHighlightPool[index];
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
            button.onClick.AddListener(() =>
            {
                if (handledClickFrame == Time.frameCount) return;
                handledClickFrame = Time.frameCount;
                action();
            });
        }

        private GameObject CreatePresetRowObject(Transform parent, HexPreset preset, int rowIndex)
        {
            GameObject row = new GameObject("PresetRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
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
