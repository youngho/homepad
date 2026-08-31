using System;
using System.Collections.Generic;
using System.Text;
using Homepad.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class KocomHexTestUI : MonoBehaviour
    {
        private static readonly Color DangerColor = new Color(0.78f, 0.28f, 0.24f, 1f);
        private static readonly Color OkColor = new Color(0.20f, 0.62f, 0.38f, 1f);
        private static readonly Color TabActiveColor = new Color(0.15f, 0.55f, 0.95f, 1f);
        private static readonly Color TabInactiveColor = new Color(0.18f, 0.22f, 0.28f, 1f);

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

        private readonly StringBuilder logBuilder = new StringBuilder();
        private int logLineCount;
        private string[] ports = new string[0];
        private int portIndex;
        private const int MaxLogLines = 120;
        private HexCategory currentCategory = HexCategory.All;
        private Font uiFont;

        private void Awake()
        {
            AutoResolveUiReferences();
        }

        private void Start()
        {
            uiFont = customFont != null
                ? customFont
                : (statusText != null && statusText.font != null)
                    ? statusText.font
                    : (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));

            BindEvents();

            var connector = GetConnector();
            if (connector != null)
            {
                connector.OnConnectionStatusChanged += UpdateStatus;
                connector.OnLogMessage += AppendLog;
                connector.OnPacketReceived += OnPacketReceived;
                UpdateStatus(connector.IsConnected);
            }

            if (customHexInput != null && string.IsNullOrEmpty(customHexInput.text))
            {
                customHexInput.text = "AA 55 30 BC 00 0E 00 01 00 00 FF 00 00 00 00 00 00 00 FA 0D 0D";
            }

            RefreshPorts(true);
            PopulatePresetList(HexCategory.All);

            if (ports.Length > 0)
            {
                OnConnectClicked();
            }
        }

        private void OnDestroy()
        {
            var connector = GetConnector();
            if (connector == null) return;
            connector.OnConnectionStatusChanged -= UpdateStatus;
            connector.OnLogMessage -= AppendLog;
            connector.OnPacketReceived -= OnPacketReceived;
        }

        private ArduinoConnector GetConnector()
        {
            if (WallpadManager.Instance != null && WallpadManager.Instance.Connector != null)
            {
                return WallpadManager.Instance.Connector;
            }
            return FindObjectOfType<ArduinoConnector>();
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
            Button[] tabs = { tabAllButton, tabLightingButton, tabHeatingButton, tabVentButton, tabDoorButton };
            foreach (var tab in tabs)
            {
                if (tab == null) continue;
                var img = tab.GetComponent<Image>();
                if (img != null)
                {
                    img.color = (tab == activeTab) ? TabActiveColor : TabInactiveColor;
                }
            }
        }

        public void ReloadPresetsFromMarkdown()
        {
            KocomHexPresets.Reload();
            PopulatePresetList(currentCategory);
            AppendLog($"<color=#55FF55>[MD 로드 완료] kocom-hex.md 에서 {KocomHexPresets.AllPresets.Count}개 프리셋 로드됨</color>", false);
        }

        private void PopulatePresetList(HexCategory category)
        {
            if (presetContainer == null) return;

            for (int i = presetContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(presetContainer.GetChild(i).gameObject);
            }

            var presets = KocomHexPresets.GetPresetsByCategory(category);

            foreach (var preset in presets)
            {
                GameObject rowObj;
                if (presetItemPrefab != null)
                {
                    rowObj = Instantiate(presetItemPrefab, presetContainer);
                }
                else
                {
                    rowObj = CreatePresetRowObject(presetContainer, preset);
                }

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
                    sendBtn.onClick.RemoveAllListeners();
                    sendBtn.onClick.AddListener(() => SendPreset(p));
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
            AppendLog($"<color=#55AAFF>[TX 요청]</color> <b>{preset.title}</b>", false);
            SendRawHex(preset.hexString);
        }

        public void SendRawHex(string hexString)
        {
            byte[] bytes = KocomHexPresets.HexStringToBytes(hexString);
            if (bytes == null || bytes.Length == 0)
            {
                AppendLog("<color=#FF5555>[오류] 유효하지 않은 HEX 문자열입니다.</color>", false);
                return;
            }

            var connector = GetConnector();
            if (connector != null)
            {
                connector.SendPacket(bytes);
            }
            else
            {
                AppendLog($"<color=#FFAA55>[시뮬레이션 전송] {KocomProtocol.ToHexString(bytes)}</color>", false);
            }
        }

        private void OnFixChecksumClicked()
        {
            if (customHexInput == null) return;
            string corrected = KocomHexPresets.RecalculateChecksum(customHexInput.text);
            customHexInput.text = corrected;
            AppendLog($"<color=#AAAAAA>[체크섬 계산 완료] {corrected}</color>", false);
        }

        private void OnSendCustomClicked()
        {
            if (customHexInput == null) return;
            string hex = customHexInput.text.Trim();
            if (string.IsNullOrEmpty(hex)) return;

            AppendLog($"<color=#55AAFF>[TX 커스텀]</color> {hex}", false);
            SendRawHex(hex);
        }

        private void OnPacketReceived(byte[] packet)
        {
            if (packet == null || packet.Length < KocomProtocol.PacketSize) return;

            string hexStr = KocomProtocol.ToHexString(packet);
            if (KocomProtocol.TryParse(packet, out var frame))
            {
                string decoded = KocomProtocol.DecodeFrame(frame);
                AppendLog($"<color=#55FF55>[RX 수신] {decoded}</color>\n<color=#888888>HEX: {hexStr}</color>", false);
            }
            else
            {
                AppendLog($"<color=#FFFF55>[RX 알수없는 패킷] {hexStr}</color>", false);
            }
        }

        public void ClearLog()
        {
            logBuilder.Clear();
            logLineCount = 0;
            if (logText != null) logText.text = string.Empty;
        }

        private void AppendLog(string message, bool isTx)
        {
            string color = isTx ? "#55AAFF" : "#CCCCCC";
            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            string line = $"<color=#888888>[{time}]</color> <color={color}>{message}</color>\n";

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

            if (logText != null) logText.text = logBuilder.ToString();

            if (logScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                logScrollRect.verticalNormalizedPosition = 0f;
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
                int found = Array.IndexOf(ports, saved);
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
            var connector = GetConnector();
            if (connector == null) return;
            string port = portField != null ? portField.text.Trim() : "";
            if (string.IsNullOrEmpty(port))
            {
                AppendLog("[오류] 시리얼 포트가 비어 있습니다. 새로고침 후 포트를 선택하세요.", false);
                return;
            }

            int baud = 115200;
            if (baudField != null) int.TryParse(baudField.text.Trim(), out baud);
            connector.SetSerialTarget(port, baud);
        }

        private void UpdateStatus(bool isConnected)
        {
            if (statusText != null)
            {
                statusText.text = isConnected ? "시리얼 연결됨" : "연결 안 됨";
                statusText.color = isConnected ? OkColor : DangerColor;
            }

            if (statusDot != null)
            {
                statusDot.color = isConnected ? OkColor : DangerColor;
            }
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private GameObject CreatePresetRowObject(Transform parent, HexPreset preset)
        {
            GameObject row = new GameObject("PresetRow", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            var rowImg = row.GetComponent<Image>();
            rowImg.color = new Color(0.14f, 0.17f, 0.23f, 0.95f);

            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0, 68);

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(row.transform, false);
            var title = titleGo.GetComponent<Text>();
            title.font = uiFont;
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.text = preset.title;
            title.color = Color.white;
            title.alignment = TextAnchor.MiddleLeft;

            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.45f);
            titleRt.anchorMax = new Vector2(0.78f, 1f);
            titleRt.offsetMin = new Vector2(14, 0);
            titleRt.offsetMax = new Vector2(-5, -4);

            // Hex Text
            var hexGo = new GameObject("Hex", typeof(RectTransform), typeof(Text));
            hexGo.transform.SetParent(row.transform, false);
            var hexText = hexGo.GetComponent<Text>();
            hexText.font = uiFont;
            hexText.fontSize = 15;
            hexText.text = preset.hexString;
            hexText.color = new Color(0.55f, 0.78f, 1f, 1f);
            hexText.alignment = TextAnchor.MiddleLeft;

            var hexRt = hexGo.GetComponent<RectTransform>();
            hexRt.anchorMin = new Vector2(0f, 0f);
            hexRt.anchorMax = new Vector2(0.78f, 0.45f);
            hexRt.offsetMin = new Vector2(14, 4);
            hexRt.offsetMax = new Vector2(-5, 0);

            // Send Button
            GameObject sendBtnGo = new GameObject("SendButton", typeof(RectTransform), typeof(Image), typeof(Button));
            sendBtnGo.transform.SetParent(row.transform, false);
            var btnImg = sendBtnGo.GetComponent<Image>();
            btnImg.color = new Color(0.18f, 0.52f, 0.88f, 1f);

            var sendRt = sendBtnGo.GetComponent<RectTransform>();
            sendRt.anchorMin = new Vector2(0.80f, 0.12f);
            sendRt.anchorMax = new Vector2(0.98f, 0.88f);
            sendRt.offsetMin = Vector2.zero;
            sendRt.offsetMax = Vector2.zero;

            var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            btnTextGo.transform.SetParent(sendBtnGo.transform, false);
            var btnText = btnTextGo.GetComponent<Text>();
            btnText.font = uiFont;
            btnText.fontSize = 18;
            btnText.fontStyle = FontStyle.Bold;
            btnText.text = "전송";
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;

            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;

            return row;
        }
    }
}
