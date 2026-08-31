using System;
using System.Collections.Generic;
using System.Text;
using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class KocomHexTestUI : MonoBehaviour
    {
        [Header("UI References - Tabs")]
        [SerializeField] private Button tabAllButton;
        [SerializeField] private Button tabLightingButton;
        [SerializeField] private Button tabHeatingButton;
        [SerializeField] private Button tabVentButton;
        [SerializeField] private Button tabDoorButton;

        [Header("UI References - Preset List")]
        [SerializeField] private Transform presetContainer;
        [SerializeField] private GameObject presetItemPrefab;
        [SerializeField] private Button reloadPresetsButton;

        [Header("UI References - Custom HEX Input")]
        [SerializeField] private InputField customHexInput;
        [SerializeField] private Button fixChecksumButton;
        [SerializeField] private Button sendCustomButton;

        [Header("UI References - Log")]
        [SerializeField] private Text logText;
        [SerializeField] private ScrollRect logScrollRect;
        [SerializeField] private Button clearLogButton;

        private readonly StringBuilder logBuilder = new StringBuilder();
        private int logLineCount;
        private const int MaxLogLines = 100;
        private HexCategory currentCategory = HexCategory.All;

        private void Start()
        {
            BindUI();
            HookConnectorEvents();
            PopulatePresetList(HexCategory.All);

            if (customHexInput != null && string.IsNullOrEmpty(customHexInput.text))
            {
                customHexInput.text = "AA 55 30 BC 00 0E 00 01 00 00 FF 00 00 00 00 00 00 00 FA 0D 0D";
            }
        }

        private void OnDestroy()
        {
            UnhookConnectorEvents();
        }

        private void BindUI()
        {
            Bind(tabAllButton, () => SwitchCategory(HexCategory.All));
            Bind(tabLightingButton, () => SwitchCategory(HexCategory.Lighting));
            Bind(tabHeatingButton, () => SwitchCategory(HexCategory.Heating));
            Bind(tabVentButton, () => SwitchCategory(HexCategory.Ventilation));
            Bind(tabDoorButton, () => SwitchCategory(HexCategory.DoorLock));

            Bind(reloadPresetsButton, ReloadPresets);
            Bind(fixChecksumButton, OnFixChecksumClicked);
            Bind(sendCustomButton, OnSendCustomClicked);
            Bind(clearLogButton, ClearLog);
        }

        public void ReloadPresets()
        {
            KocomHexPresets.Reload();
            PopulatePresetList(currentCategory);
            AppendLog($"<color=#55FF55>[MD 로드 완료] kocom-hex.md 에서 {KocomHexPresets.AllPresets.Count}개 프리셋 로드됨</color>", false);
        }

        private void HookConnectorEvents()
        {
            var connector = GetConnector();
            if (connector != null)
            {
                connector.OnLogMessage += OnLogMessageReceived;
                connector.OnPacketReceived += OnPacketReceived;
            }
        }

        private void UnhookConnectorEvents()
        {
            var connector = GetConnector();
            if (connector != null)
            {
                connector.OnLogMessage -= OnLogMessageReceived;
                connector.OnPacketReceived -= OnPacketReceived;
            }
        }

        private ArduinoConnector GetConnector()
        {
            if (WallpadManager.Instance != null && WallpadManager.Instance.Connector != null)
            {
                return WallpadManager.Instance.Connector;
            }
            return FindObjectOfType<ArduinoConnector>();
        }

        public void SwitchCategory(HexCategory category)
        {
            currentCategory = category;
            PopulatePresetList(category);
        }

        private void PopulatePresetList(HexCategory category)
        {
            if (presetContainer == null) return;

            // Clear existing children
            for (int i = presetContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(presetContainer.GetChild(i).gameObject);
            }

            var presets = KocomHexPresets.GetPresetsByCategory(category);

            foreach (var preset in presets)
            {
                GameObject itemObj;
                if (presetItemPrefab != null)
                {
                    itemObj = Instantiate(presetItemPrefab, presetContainer);
                }
                else
                {
                    // Fallback to runtime UI building if prefab not assigned
                    itemObj = CreateDefaultPresetItem(presetContainer);
                }

                itemObj.name = $"Preset_{preset.id}";

                var titleText = itemObj.transform.Find("Title")?.GetComponent<Text>();
                var descText = itemObj.transform.Find("Desc")?.GetComponent<Text>();
                var hexText = itemObj.transform.Find("Hex")?.GetComponent<Text>();
                var sendBtn = itemObj.GetComponentInChildren<Button>();

                if (titleText != null) titleText.text = preset.title;
                if (descText != null) descText.text = preset.description;
                if (hexText != null) hexText.text = preset.hexString;

                if (sendBtn != null)
                {
                    var currentPreset = preset;
                    sendBtn.onClick.RemoveAllListeners();
                    sendBtn.onClick.AddListener(() => SendPreset(currentPreset));
                }
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
                AppendLog("<color=#FF5555>[오류] 잘못된 HEX 문자열입니다.</color>", false);
                return;
            }

            var connector = GetConnector();
            if (connector != null)
            {
                connector.SendPacket(bytes);
            }
            else
            {
                AppendLog($"<color=#FFAA55>[시뮬레이션/직접전송] {KocomProtocol.ToHexString(bytes)}</color>", false);
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
                AppendLog($"<color=#FFFF55>[RX 파싱불가] {hexStr}</color>", false);
            }
        }

        private void OnLogMessageReceived(string message, bool isTx)
        {
            AppendLog(message, isTx);
        }

        public void ClearLog()
        {
            logBuilder.Clear();
            logLineCount = 0;
            if (logText != null) logText.text = string.Empty;
        }

        private void AppendLog(string message, bool isTx)
        {
            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            string line = $"<color=#888888>[{time}]</color> {message}\n";

            logBuilder.Append(line);
            logLineCount++;

            if (logLineCount > MaxLogLines)
            {
                string str = logBuilder.ToString();
                int idx = str.IndexOf('\n');
                if (idx >= 0)
                {
                    logBuilder.Remove(0, idx + 1);
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

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private GameObject CreateDefaultPresetItem(Transform parent)
        {
            GameObject item = new GameObject("PresetItem", typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            var img = item.GetComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);

            var rt = item.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 70);

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(item.transform, false);
            var title = titleGo.GetComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            title.fontSize = 15;
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 0.5f);
            titleRt.anchorMax = new Vector2(0.7f, 1f);
            titleRt.offsetMin = new Vector2(12, 0);
            titleRt.offsetMax = new Vector2(-10, -8);
            title.alignment = TextAnchor.MiddleLeft;

            // Hex / Desc
            var hexGo = new GameObject("Hex", typeof(RectTransform), typeof(Text));
            hexGo.transform.SetParent(item.transform, false);
            var hex = hexGo.GetComponent<Text>();
            hex.font = title.font;
            hex.fontSize = 12;
            hex.color = new Color(0.6f, 0.8f, 1f, 1f);
            var hexRt = hexGo.GetComponent<RectTransform>();
            hexRt.anchorMin = new Vector2(0, 0f);
            hexRt.anchorMax = new Vector2(0.7f, 0.5f);
            hexRt.offsetMin = new Vector2(12, 8);
            hexRt.offsetMax = new Vector2(-10, 0);
            hex.alignment = TextAnchor.MiddleLeft;

            // Send Button
            var btnGo = new GameObject("SendButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(item.transform, false);
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.15f, 0.55f, 0.9f, 1f);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.75f, 0.15f);
            btnRt.anchorMax = new Vector2(0.98f, 0.85f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;

            var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnText = btnTextGo.GetComponent<Text>();
            btnText.font = title.font;
            btnText.fontSize = 14;
            btnText.text = "전송";
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;

            return item;
        }
    }
}
