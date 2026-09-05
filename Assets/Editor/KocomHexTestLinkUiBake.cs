#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.Editor
{
    public static class KocomHexTestLinkUiBake
    {
        private static readonly Color TrackColor = new Color(0.10f, 0.11f, 0.13f, 1f);
        private static readonly Color ActiveColor = new Color(0.255f, 0.278f, 0.318f, 1f);
        private static readonly Color InactiveText = new Color(0.62f, 0.65f, 0.71f, 1f);

        [MenuItem("Tools/Kocom/Bake Hex Test Link UI")]
        public static void Bake()
        {
            var header = GameObject.Find("HeaderCard");
            var root = GameObject.Find("KocomHexTestRoot");
            if (header == null || root == null)
            {
                Debug.LogError("[Kocom] HeaderCard 또는 KocomHexTestRoot를 찾지 못했습니다. KocomHexTest 씬을 여세요.");
                return;
            }

            var connect = header.transform.Find("Connect")?.gameObject;
            var port = header.transform.Find("Port")?.gameObject;
            if (connect == null || port == null)
            {
                Debug.LogError("[Kocom] Connect 또는 Port 원본이 없습니다.");
                return;
            }

            SetAnchors(header.transform.Find("Title") as RectTransform, 0.012f, 0.074f, 0.18f, 0.82f);
            var titleText = header.transform.Find("Title")?.GetComponent<Text>();
            if (titleText != null)
            {
                titleText.fontSize = 20;
                titleText.fontStyle = FontStyle.Normal;
                titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                titleText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            BakeSegmented(
                header.transform,
                connect,
                "DeviceMode",
                new Vector2(0.082f, 0.2f),
                new Vector2(0.195f, 0.8f),
                ("DeviceArduino", "아두이노", true),
                ("DeviceEw11", "EW-11", false));
            BakeSegmented(
                header.transform,
                connect,
                "ProtocolMode",
                new Vector2(0.203f, 0.2f),
                new Vector2(0.348f, 0.8f),
                ("ProtoSerial", "시리얼", true),
                ("ProtoTcp", "TCP", false),
                ("ProtoMqtt", "MQTT", false));

            SetAnchors(header.transform.Find("StatusDot") as RectTransform, 0.356f, 0.368f, 0.38f, 0.62f);
            SetAnchors(header.transform.Find("Status") as RectTransform, 0.372f, 0.430f, 0.18f, 0.82f);
            SetAnchors(header.transform.Find("Port") as RectTransform, 0.438f, 0.522f, 0.2f, 0.8f);
            SetAnchors(header.transform.Find("PortLabel") as RectTransform, 0.438f, 0.468f, 0.1f, 0.9f);
            SetAnchors(header.transform.Find("PrevPort") as RectTransform, 0.528f, 0.550f, 0.2f, 0.8f);
            SetAnchors(header.transform.Find("NextPort") as RectTransform, 0.552f, 0.574f, 0.2f, 0.8f);
            SetAnchors(header.transform.Find("Refresh") as RectTransform, 0.580f, 0.636f, 0.2f, 0.8f);
            SetAnchors(header.transform.Find("BaudLabel") as RectTransform, 0.644f, 0.704f, 0.18f, 0.82f);
            SetAnchors(header.transform.Find("Baud") as RectTransform, 0.644f, 0.704f, 0.18f, 0.82f);
            SetAnchors(header.transform.Find("Connect") as RectTransform, 0.712f, 0.768f, 0.2f, 0.8f);
            SetAnchors(header.transform.Find("Disconnect") as RectTransform, 0.774f, 0.830f, 0.2f, 0.8f);
            SetAnchors(header.transform.Find("WallpadScene") as RectTransform, 0.838f, 0.988f, 0.2f, 0.8f);
            BakeMqttBar(root.transform, header, port);

            EditorSceneManager.MarkSceneDirty(header.scene);
            Debug.Log("[Kocom] HEX 테스트 헤더를 세그먼트 토글로 씬에 반영했습니다. 에디터에서 씬을 저장하세요.");
        }

        private static void SetAnchors(RectTransform rt, float xMin, float xMax, float yMin, float yMax)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void BakeSegmented(
            Transform header,
            GameObject template,
            string groupName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            params (string name, string label, bool on)[] options)
        {
            Transform existing = header.Find(groupName);
            GameObject group = existing != null ? existing.gameObject : Object.Instantiate(template, header);
            group.name = groupName;
            var rt = group.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var btn = group.GetComponent<Button>();
            if (btn != null) Object.DestroyImmediate(btn);

            var img = group.GetComponent<Image>();
            if (img != null) img.color = TrackColor;

            var layout = group.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = group.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var groupToggle = group.GetComponent<ToggleGroup>();
            if (groupToggle == null) groupToggle = group.AddComponent<ToggleGroup>();
            groupToggle.allowSwitchOff = false;

            for (int i = group.transform.childCount - 1; i >= 0; i--)
            {
                var child = group.transform.GetChild(i);
                bool keep = false;
                foreach (var opt in options)
                {
                    if (child.name == opt.name)
                    {
                        keep = true;
                        break;
                    }
                }

                if (!keep) Object.DestroyImmediate(child.gameObject);
            }

            foreach (var opt in options)
            {
                BakeToggle(group.transform, template, groupToggle, opt.name, opt.label, opt.on);
            }
        }

        private static void BakeToggle(
            Transform parent,
            GameObject template,
            ToggleGroup group,
            string objectName,
            string label,
            bool on)
        {
            Transform existing = parent.Find(objectName);
            GameObject go = existing != null ? existing.gameObject : Object.Instantiate(template, parent);
            go.name = objectName;
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            var btn = go.GetComponent<Button>();
            if (btn != null) Object.DestroyImmediate(btn);

            var bg = go.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(1f, 1f, 1f, 0f);
                bg.raycastTarget = true;
            }

            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
            le.minWidth = 40f;

            Transform highlightTf = go.transform.Find("Highlight");
            GameObject highlightGo = highlightTf != null ? highlightTf.gameObject : new GameObject("Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            highlightGo.transform.SetParent(go.transform, false);
            highlightGo.transform.SetAsFirstSibling();
            var hRt = highlightGo.GetComponent<RectTransform>();
            hRt.anchorMin = Vector2.zero;
            hRt.anchorMax = Vector2.one;
            hRt.offsetMin = Vector2.zero;
            hRt.offsetMax = Vector2.zero;
            var hImg = highlightGo.GetComponent<Image>();
            if (bg != null)
            {
                hImg.sprite = bg.sprite;
                hImg.type = bg.type;
                hImg.pixelsPerUnitMultiplier = bg.pixelsPerUnitMultiplier;
            }

            hImg.color = ActiveColor;
            hImg.raycastTarget = false;

            var txt = go.GetComponentInChildren<Text>();
            if (txt == null)
            {
                var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textGo.transform.SetParent(go.transform, false);
                txt = textGo.GetComponent<Text>();
            }

            var tRt = txt.rectTransform;
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            txt.fontStyle = FontStyle.Normal;
            txt.fontSize = 16;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.text = label;
            txt.color = on ? Color.white : InactiveText;

            var toggle = go.GetComponent<Toggle>();
            if (toggle == null) toggle = go.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = bg;
            toggle.graphic = hImg;
            toggle.group = group;
            toggle.isOn = on;
            toggle.onValueChanged.RemoveAllListeners();
        }

        private static void BakeMqttBar(Transform root, GameObject header, GameObject portTemplate)
        {
            Transform existing = root.Find("MqttBar");
            GameObject bar = existing != null ? existing.gameObject : new GameObject("MqttBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(root, false);

            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.02f, 0.795f);
            rt.anchorMax = new Vector2(0.98f, 0.875f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var headerImg = header.GetComponent<Image>();
            var img = bar.GetComponent<Image>();
            if (headerImg != null)
            {
                img.sprite = headerImg.sprite;
                img.type = headerImg.type;
                img.color = headerImg.color;
                img.pixelsPerUnitMultiplier = headerImg.pixelsPerUnitMultiplier;
            }

            BakeInput(bar.transform, portTemplate, "MqttUser", "MQTT 사용자", new Vector2(0.012f, 0.16f), new Vector2(0.20f, 0.84f), false);
            BakeInput(bar.transform, portTemplate, "MqttPass", "비밀번호", new Vector2(0.212f, 0.16f), new Vector2(0.40f, 0.84f), true);
            BakeInput(bar.transform, portTemplate, "MqttTx", "송신 토픽 kocom/tx", new Vector2(0.412f, 0.16f), new Vector2(0.70f, 0.84f), false);
            BakeInput(bar.transform, portTemplate, "MqttRx", "수신 토픽 kocom/rx", new Vector2(0.712f, 0.16f), new Vector2(0.988f, 0.84f), false);

            bar.SetActive(false);
        }

        private static void BakeInput(Transform parent, GameObject template, string objectName, string placeholder, Vector2 anchorMin, Vector2 anchorMax, bool password)
        {
            Transform existing = parent.Find(objectName);
            GameObject go = existing != null ? existing.gameObject : Object.Instantiate(template, parent);
            go.name = objectName;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var field = go.GetComponent<InputField>();
            if (field == null) return;
            field.text = objectName == "MqttTx" ? "kocom/tx" : objectName == "MqttRx" ? "kocom/rx" : string.Empty;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            if (field.placeholder is Text ph)
            {
                ph.fontStyle = FontStyle.Normal;
                ph.fontSize = Mathf.Max(16, ph.fontSize);
                ph.text = placeholder;
            }
        }
    }
}
#endif
