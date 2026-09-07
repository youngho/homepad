#if UNITY_EDITOR
using System.Reflection;
using Homepad.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Homepad.Editor
{
    public static class WallpadSettingsHexBake
    {
        private static readonly Font Regular = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Pretendard-Regular.otf");
        private static readonly Font SemiBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Pretendard-SemiBold.otf");

        [MenuItem("Tools/Homepad/Bake Wallpad Settings HEX UI")]
        public static void Bake()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "WallpadMain")
            {
                Debug.LogError("[Homepad] WallpadMain 씬을 연 뒤 다시 실행하세요.");
                return;
            }

            var canvas = GameObject.Find("Canvas");
            var root = canvas != null ? canvas.transform.Find("WallpadRoot") : null;
            var settings = root != null ? root.Find("Settings") : null;
            var header = root != null ? root.Find("Header") : null;
            var catalogClose = root != null ? root.Find("Catalog/Close") : null;
            if (root == null || settings == null || header == null || catalogClose == null)
            {
                Debug.LogError("[Homepad] Settings/Header/Catalog Close를 찾지 못했습니다.");
                return;
            }

            EnsureLinkWidgets(root, settings, header, catalogClose);
            WireComponents(root, settings, header);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Homepad] 헤더 설정 버튼, HEX 연결창, 로그 오버레이를 저장했습니다.");
        }

        private static void EnsureLinkWidgets(Transform root, Transform settings, Transform header, Transform catalogClose)
        {
            var hexLog = root.Find("HexLog");
            var linkBar = settings.Find("LinkBar");
            var mqttBar = settings.Find("MqttBar");
            var settingsBtn = header.Find("SettingsButton");

            if (linkBar == null || mqttBar == null)
            {
                Scene hexScene = default;
                bool opened = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s.name == "KocomHexTest")
                    {
                        hexScene = s;
                        break;
                    }
                }

                if (!hexScene.IsValid() || !hexScene.isLoaded)
                {
                    hexScene = EditorSceneManager.OpenScene("Assets/Scenes/KocomHexTest.unity", OpenSceneMode.Additive);
                    opened = true;
                }

                GameObject srcHeader = null;
                GameObject srcMqtt = null;
                foreach (var go in hexScene.GetRootGameObjects())
                {
                    var h = FindDeep(go.transform, "HeaderCard");
                    if (h != null) srcHeader = h.gameObject;
                    var m = FindDeep(go.transform, "MqttBar");
                    if (m != null) srcMqtt = m.gameObject;
                }

                if (srcHeader == null || srcMqtt == null)
                {
                    if (opened) EditorSceneManager.CloseScene(hexScene, false);
                    throw new System.Exception("KocomHexTest HeaderCard/MqttBar를 찾지 못했습니다.");
                }

                if (linkBar == null)
                {
                    var copy = Object.Instantiate(srcHeader, settings, false);
                    copy.name = "LinkBar";
                    StripCloneSuffix(copy.transform);
                    DestroyChild(copy.transform, "Title");
                    DestroyChild(copy.transform, "WallpadScene");
                    SanitizeTexts(copy.transform);
                    linkBar = copy.transform;
                }

                if (mqttBar == null)
                {
                    var copy = Object.Instantiate(srcMqtt, settings, false);
                    copy.name = "MqttBar";
                    StripCloneSuffix(copy.transform);
                    SanitizeTexts(copy.transform);
                    copy.SetActive(false);
                    mqttBar = copy.transform;
                }

                if (opened) EditorSceneManager.CloseScene(hexScene, false);
                EditorSceneManager.SetActiveScene(SceneManager.GetSceneByName("WallpadMain"));
            }

            PinTop((RectTransform)linkBar, 20f, 20f, 76f, 88f);
            PinTop((RectTransform)mqttBar, 20f, 20f, 220f, 72f);

            var settingsRt = (RectTransform)settings;
            settingsRt.anchorMin = Vector2.zero;
            settingsRt.anchorMax = Vector2.one;
            settingsRt.pivot = new Vector2(0.5f, 0.5f);
            settingsRt.offsetMin = new Vector2(16f, 20f);
            settingsRt.offsetMax = new Vector2(-16f, -108f);
            settingsRt.localScale = Vector3.one;

            SetText(settings.Find("Title"), "연결 설정", SemiBold, 28);
            HideNamed(settings, "IpLabel");
            HideNamed(settings, "IpInput");
            HideNamed(settings, "PortLabel");
            HideNamed(settings, "PortInput");
            HideNamed(settings, "Apply");
            HideNamed(settings, "StatusRow");

            var showLog = settings.Find("Simulation") ?? settings.Find("ShowLog");
            if (showLog != null)
            {
                showLog.name = "ShowLog";
                SetText(showLog.Find("Label"), "로그보기", Regular, 20);
                var showLogRt = (RectTransform)showLog;
                showLogRt.anchorMin = new Vector2(0f, 1f);
                showLogRt.anchorMax = new Vector2(0f, 1f);
                showLogRt.pivot = new Vector2(0f, 1f);
                showLogRt.anchoredPosition = new Vector2(24f, -172f);
                showLogRt.sizeDelta = new Vector2(280f, 40f);
                var toggle = showLog.GetComponent<Toggle>();
                if (toggle != null) toggle.isOn = false;
            }

            var memory = settings.Find("DeviceMemory");
            if (memory == null)
            {
                var title = settings.Find("Title");
                if (title != null)
                {
                    var copy = Object.Instantiate(title.gameObject, settings, false);
                    copy.name = "DeviceMemory";
                    memory = copy.transform;
                }
            }

            if (memory != null)
            {
                var memoryRt = (RectTransform)memory;
                memoryRt.anchorMin = Vector2.zero;
                memoryRt.anchorMax = Vector2.one;
                memoryRt.pivot = new Vector2(0.5f, 1f);
                memoryRt.offsetMin = new Vector2(24f, 20f);
                memoryRt.offsetMax = new Vector2(-24f, -308f);
                memoryRt.localScale = Vector3.one;
                var txt = memory.GetComponent<Text>() ?? memory.GetComponentInChildren<Text>(true);
                if (txt != null)
                {
                    txt.font = Regular != null ? Regular : txt.font;
                    txt.fontSize = 18;
                    txt.fontStyle = FontStyle.Normal;
                    txt.resizeTextForBestFit = false;
                    txt.alignment = TextAnchor.UpperLeft;
                    txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                    txt.verticalOverflow = VerticalWrapMode.Overflow;
                    txt.supportRichText = false;
                    txt.raycastTarget = false;
                    txt.color = new Color(0.86f, 0.88f, 0.90f, 1f);
                    txt.text = "장치 상태";
                }
            }

            if (hexLog == null)
            {
                var go = new GameObject("HexLog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = root.gameObject.layer;
                go.transform.SetParent(root, false);
                hexLog = go.transform;
                var img = go.GetComponent<Image>();
                var settingsImg = settings.GetComponent<Image>();
                if (settingsImg != null)
                {
                    img.sprite = settingsImg.sprite;
                    img.type = settingsImg.type;
                    img.pixelsPerUnitMultiplier = settingsImg.pixelsPerUnitMultiplier;
                }

                img.color = new Color(0.08f, 0.10f, 0.13f, 0.78f);
                img.raycastTarget = true;
            }

            SetAnchors((RectTransform)hexLog, 0.04f, 0.02f, 0.96f, 0.16f);

            var logScroll = settings.Find("LogScroll") ?? hexLog.Find("LogScroll");
            if (logScroll != null)
            {
                logScroll.SetParent(hexLog, false);
                SetAnchors((RectTransform)logScroll, 0.02f, 0.06f, 0.82f, 0.94f);
                var logImg = logScroll.GetComponent<Image>();
                if (logImg != null) logImg.color = new Color(0.06f, 0.07f, 0.09f, 0.35f);
            }

            var clearLog = settings.Find("ClearLog") ?? hexLog.Find("ClearLog");
            if (clearLog != null)
            {
                clearLog.SetParent(hexLog, false);
                SetAnchors((RectTransform)clearLog, 0.84f, 0.12f, 0.98f, 0.88f);
                SetText(clearLog.Find("Text"), "지우기", SemiBold, 18);
            }

            var logTextTf = FindDeep(hexLog, "LogText");
            if (logTextTf != null)
            {
                var txt = logTextTf.GetComponent<Text>();
                if (txt != null)
                {
                    txt.font = Regular;
                    txt.fontSize = 16;
                    txt.fontStyle = FontStyle.Normal;
                    txt.supportRichText = true;
                    txt.alignment = TextAnchor.UpperLeft;
                    txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                    txt.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }

            hexLog.gameObject.SetActive(false);
            settings.SetAsLastSibling();
            hexLog.SetAsLastSibling();

            if (settingsBtn == null)
            {
                var copy = Object.Instantiate(catalogClose.gameObject, header, false);
                copy.name = "SettingsButton";
                settingsBtn = copy.transform;
            }

            SetAnchors((RectTransform)settingsBtn, 0.865f, 0.12f, 0.985f, 0.88f);
            SetText(settingsBtn.Find("Text"), "설정", SemiBold, 18);

            var hexBtn = header.Find("HexTestScene");
            if (hexBtn == null)
            {
                var copy = Object.Instantiate(settingsBtn.gameObject, header, false);
                copy.name = "HexTestScene";
                hexBtn = copy.transform;
            }

            SetAnchors((RectTransform)hexBtn, 0.745f, 0.12f, 0.855f, 0.88f);
            SetText(hexBtn.Find("Text"), "HEX 테스트", SemiBold, 18);

            var wifi = header.Find("Wifi");
            if (wifi != null)
            {
                var wifiRt = (RectTransform)wifi;
                wifiRt.anchorMin = new Vector2(0.62f, 0.18f);
                wifiRt.anchorMax = new Vector2(0.735f, 0.82f);
                wifiRt.offsetMin = Vector2.zero;
                wifiRt.offsetMax = Vector2.zero;
            }

            settings.gameObject.SetActive(false);
        }

        private static void WireComponents(Transform root, Transform settings, Transform header)
        {
            var hexLog = root.Find("HexLog");
            var settingsBtn = header.Find("SettingsButton");
            var hexBtn = header.Find("HexTestScene");
            var close = settings.Find("Close");

            var old = settings.GetComponent<NetworkSettingsUI>();
            if (old != null) Object.DestroyImmediate(old);

            var net = root.GetComponent<NetworkSettingsUI>();
            if (net == null) net = root.gameObject.AddComponent<NetworkSettingsUI>();
            SetField(net, "settingsRoot", settings);
            SetField(net, "logPanel", hexLog != null ? hexLog.gameObject : null);
            var memory = settings.Find("DeviceMemory");
            var memoryTxt = memory != null
                ? memory.GetComponent<Text>() ?? memory.GetComponentInChildren<Text>(true)
                : null;
            SetField(net, "memoryText", memoryTxt);

            var controller = root.GetComponent<WallpadUIController>();
            if (controller == null) return;
            SetField(controller, "settingsButton", settingsBtn != null ? settingsBtn.GetComponent<Button>() : null);
            SetField(controller, "hexTestButton", hexBtn != null ? hexBtn.GetComponent<Button>() : null);
            SetField(controller, "settingsCloseButton", close != null ? close.GetComponent<Button>() : null);
            SetField(controller, "settingsPanel", settings.gameObject);
        }

        private static void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null)
            {
                Debug.LogError("[Homepad] 필드 없음: " + target.GetType().Name + "." + name);
                return;
            }

            f.SetValue(target, value);
        }

        private static void PinTop(RectTransform rt, float left, float right, float top, float height)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
            rt.localScale = Vector3.one;
        }

        private static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static void SetText(Transform t, string value, Font font, int size)
        {
            if (t == null) return;
            var txt = t.GetComponent<Text>() ?? t.GetComponentInChildren<Text>(true);
            if (txt == null) return;
            txt.text = value;
            txt.font = font != null ? font : txt.font;
            txt.fontSize = size;
            txt.fontStyle = FontStyle.Normal;
            txt.resizeTextForBestFit = false;
        }

        private static void HideNamed(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static void StripCloneSuffix(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("(Clone)"))
                    t.name = t.name.Substring(0, t.name.Length - "(Clone)".Length).Trim();
            }
        }

        private static void SanitizeTexts(Transform root)
        {
            foreach (var txt in root.GetComponentsInChildren<Text>(true))
            {
                txt.fontStyle = FontStyle.Normal;
                txt.resizeTextForBestFit = false;
                if (txt.fontSize < 16) txt.fontSize = 16;
                if (txt.font != null && (txt.font.name.Contains("Arial") || txt.font.name.Contains("Legacy")))
                    txt.font = Regular;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var f = FindDeep(root.GetChild(i), name);
                if (f != null) return f;
            }

            return null;
        }
    }
}
#endif
