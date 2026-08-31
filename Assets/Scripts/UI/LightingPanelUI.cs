using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class LightingPanelUI : MonoBehaviour
    {
        private static readonly Color OnColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color OffColor = new Color(0.45f, 0.45f, 0.5f);
        private static readonly Color OnBg = new Color(0.22f, 0.26f, 0.34f);
        private static readonly Color OffBg = new Color(0.12f, 0.14f, 0.18f);

        private Button[] lightButtons;
        private Text[] statusTexts;
        private Text[] nameTexts;

        public void Build()
        {
            var root = GetComponent<RectTransform>();
            var allOff = UiFactory.CreateButton("AllOff", root, new Vector2(0, 1), Vector2.one, new Vector2(0, -72), Vector2.zero, "일괄 소등", new Color(0.18f, 0.2f, 0.26f), 28);
            allOff.onClick.AddListener(() => WallpadManager.Instance.TurnOffAllLights());

            var manager = WallpadManager.Instance;
            int count = manager != null ? manager.Lights.Count : 0;
            lightButtons = new Button[count];
            statusTexts = new Text[count];
            nameTexts = new Text[count];

            int columns = 3;
            for (int i = 0; i < count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                float x0 = col / (float)columns;
                float x1 = (col + 1) / (float)columns;
                float y1 = 1f - (row * 0.28f) - 0.10f;
                float y0 = y1 - 0.25f;

                int lightId = manager.Lights[i].id;
                var button = UiFactory.CreateButton(
                    $"Light{lightId}",
                    root,
                    new Vector2(x0, y0),
                    new Vector2(x1, y1),
                    new Vector2(8, 8),
                    new Vector2(-8, -8),
                    "",
                    OffBg);
                button.onClick.AddListener(() => WallpadManager.Instance.ToggleLight(lightId));
                lightButtons[i] = button;
                nameTexts[i] = UiFactory.CreateLabel("Name", button.transform, new Vector2(0, 0.45f), Vector2.one, new Vector2(16, 0), new Vector2(-16, -10), manager.Lights[i].name, 26, Color.white, TextAnchor.MiddleLeft);
                statusTexts[i] = UiFactory.CreateLabel("Status", button.transform, Vector2.zero, new Vector2(1, 0.5f), new Vector2(16, 12), new Vector2(-16, 0), "OFF", 22, OffColor, TextAnchor.MiddleLeft);
            }

            if (manager != null)
            {
                manager.OnStateChanged += RefreshAll;
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance == null) return;
            WallpadManager.Instance.OnStateChanged -= RefreshAll;
        }

        public void RefreshAll()
        {
            if (WallpadManager.Instance == null || lightButtons == null) return;
            var lights = WallpadManager.Instance.Lights;
            for (int i = 0; i < lights.Count && i < lightButtons.Length; i++)
            {
                bool isOn = lights[i].isOn;
                if (statusTexts[i] != null)
                {
                    statusTexts[i].text = isOn ? "ON" : "OFF";
                    statusTexts[i].color = isOn ? OnColor : OffColor;
                }

                if (nameTexts[i] != null)
                {
                    nameTexts[i].text = lights[i].name;
                }

                var image = lightButtons[i].GetComponent<Image>();
                if (image != null) image.color = isOn ? OnBg : OffBg;
            }
        }
    }
}
