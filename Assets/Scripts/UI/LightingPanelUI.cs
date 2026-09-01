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

        [SerializeField] private Button allOffButton;
        [SerializeField] private LightSlot[] lights;

        [System.Serializable]
        public class LightSlot
        {
            public int lightId;
            public Button button;
            public Text nameText;
            public Text statusText;
        }

        public void Focus(int lightId)
        {
            if (lights != null && lights.Length > 0)
            {
                lights[0].lightId = lightId;
            }

            BindClicks();
            RefreshAll();
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        private void Start()
        {
            BindClicks();
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnStateChanged += RefreshAll;
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
            if (WallpadManager.Instance == null || lights == null) return;
            var managerLights = WallpadManager.Instance.Lights;
            for (int i = 0; i < lights.Length; i++)
            {
                var slot = lights[i];
                var state = FindLight(managerLights, slot.lightId);
                if (state == null) continue;

                bool isOn = state.isOn;
                if (slot.statusText != null)
                {
                    slot.statusText.text = isOn ? "ON" : "OFF";
                    slot.statusText.color = isOn ? OnColor : OffColor;
                }

                if (slot.nameText != null)
                {
                    slot.nameText.text = state.name;
                }

                if (slot.button != null)
                {
                    var image = slot.button.GetComponent<Image>();
                    if (image != null) image.color = isOn ? OnBg : OffBg;
                }
            }
        }

        private void BindClicks()
        {
            if (allOffButton != null)
            {
                allOffButton.onClick.RemoveAllListeners();
                allOffButton.onClick.AddListener(() => WallpadManager.Instance.TurnOffAllLights());
            }

            if (lights == null) return;
            for (int i = 0; i < lights.Length; i++)
            {
                int lightId = lights[i].lightId;
                if (lights[i].button == null) continue;
                lights[i].button.onClick.RemoveAllListeners();
                lights[i].button.onClick.AddListener(() => WallpadManager.Instance.ToggleLight(lightId));
            }
        }

        private static LightState FindLight(System.Collections.Generic.IReadOnlyList<LightState> managerLights, int lightId)
        {
            if (managerLights == null) return null;
            for (int i = 0; i < managerLights.Count; i++)
            {
                if (managerLights[i].id == lightId) return managerLights[i];
            }

            return null;
        }
    }
}
