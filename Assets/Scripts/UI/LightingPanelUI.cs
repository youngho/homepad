using System.Collections.Generic;
using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    /// <summary>
    /// 조명 제어 패널 UI
    /// </summary>
    public class LightingPanelUI : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] private Button allOffButton;
        [SerializeField] private Transform cardsContainer;
        [SerializeField] private GameObject lightCardPrefab;

        [Header("Card UI Elements (Direct Mode)")]
        [SerializeField] private List<Button> lightButtons = new List<Button>();
        [SerializeField] private List<Text> lightStatusTexts = new List<Text>();
        [SerializeField] private List<Image> lightIcons = new List<Image>();

        [Header("Colors")]
        [SerializeField] private Color onColor = new Color(1.0f, 0.85f, 0.3f, 1.0f);
        [SerializeField] private Color offColor = new Color(0.4f, 0.4f, 0.45f, 1.0f);
        [SerializeField] private Color onBgColor = new Color(0.2f, 0.25f, 0.35f, 1.0f);
        [SerializeField] private Color offBgColor = new Color(0.12f, 0.14f, 0.18f, 1.0f);

        private void Start()
        {
            if (allOffButton != null)
            {
                allOffButton.onClick.AddListener(() => WallpadManager.Instance.TurnOffAllLights());
            }

            // Bind buttons
            for (int i = 0; i < lightButtons.Count; i++)
            {
                int lightId = i + 1;
                if (lightButtons[i] != null)
                {
                    lightButtons[i].onClick.AddListener(() => WallpadManager.Instance.ToggleLight(lightId));
                }
            }

            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnLightChanged += OnLightChanged;
                WallpadManager.Instance.OnStateChanged += RefreshAll;
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnLightChanged -= OnLightChanged;
                WallpadManager.Instance.OnStateChanged -= RefreshAll;
            }
        }

        private void OnLightChanged(LightState light)
        {
            RefreshAll();
        }

        public void RefreshAll()
        {
            if (WallpadManager.Instance == null) return;
            var lights = WallpadManager.Instance.Lights;

            for (int i = 0; i < lights.Count && i < lightButtons.Count; i++)
            {
                var light = lights[i];
                bool isOn = light.isOn;

                if (i < lightStatusTexts.Count && lightStatusTexts[i] != null)
                {
                    lightStatusTexts[i].text = isOn ? "ON" : "OFF";
                    lightStatusTexts[i].color = isOn ? onColor : offColor;
                }

                if (i < lightIcons.Count && lightIcons[i] != null)
                {
                    lightIcons[i].color = isOn ? onColor : offColor;
                }

                var img = lightButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = isOn ? onBgColor : offBgColor;
                }
            }
        }
    }
}
