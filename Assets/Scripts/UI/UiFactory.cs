using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public static class UiFactory
    {
        private static Font koreanFont;

        public static Font KoreanFont
        {
            get
            {
                if (koreanFont == null)
                {
                    koreanFont = Font.CreateDynamicFontFromOSFont(new[]
                    {
                        "Apple SD Gothic Neo",
                        "AppleGothic",
                        "NanumGothic",
                        "Malgun Gothic",
                        "Noto Sans CJK KR",
                        "Arial Unicode MS"
                    }, 28);

                    if (koreanFont == null)
                    {
                        koreanFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }

                return koreanFont;
            }
        }

        public static RectTransform Create(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image AddImage(RectTransform rect, Color color, bool raycast = true)
        {
            var image = rect.gameObject.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Text AddText(RectTransform rect, string content, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var text = rect.gameObject.GetComponent<Text>();
            if (text == null) text = rect.gameObject.AddComponent<Text>();
            text.font = KoreanFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string label, Color background, int fontSize = 28)
        {
            var rect = Create(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = AddImage(rect, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.18f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 0.35f);
            button.colors = colors;

            var textRect = Create("Label", rect, Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
            AddText(textRect, label, fontSize, Color.white, TextAnchor.MiddleCenter);
            return button;
        }

        public static Text CreateLabel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string content, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rect = Create(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            return AddText(rect, content, fontSize, color, align);
        }

        public static InputField CreateInput(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string value)
        {
            var rect = Create(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = AddImage(rect, new Color(0.08f, 0.09f, 0.12f, 1f));
            var field = rect.gameObject.AddComponent<InputField>();

            var textRect = Create("Text", rect, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
            var text = AddText(textRect, value, 26, Color.white, TextAnchor.MiddleLeft);
            text.supportRichText = false;

            var placeholderRect = Create("Placeholder", rect, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
            var placeholder = AddText(placeholderRect, "", 26, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);

            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = image;
            field.text = value;
            return field;
        }

        public static Toggle CreateToggle(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string label, bool isOn)
        {
            var rect = Create(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var toggle = rect.gameObject.AddComponent<Toggle>();

            var box = Create("Box", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            box.sizeDelta = new Vector2(36, 36);
            box.anchoredPosition = new Vector2(22, 0);
            var boxImage = AddImage(box, new Color(0.18f, 0.2f, 0.26f, 1f));

            var check = Create("Check", box, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            var checkImage = AddImage(check, new Color(0.25f, 0.72f, 0.45f, 1f));

            CreateLabel("Label", rect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(52, 0), Vector2.zero, label, 26, Color.white, TextAnchor.MiddleLeft);

            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = isOn;
            return toggle;
        }
    }
}
