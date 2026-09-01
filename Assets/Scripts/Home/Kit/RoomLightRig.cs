using UnityEngine;

namespace Homepad.Home
{
    public class RoomLightRig : MonoBehaviour
    {
        [SerializeField] private Light[] lamps;
        [SerializeField] private Renderer[] lampShades;
        [SerializeField] private Light fill;

        private static readonly Color OnColor = new Color(1f, 0.93f, 0.78f);
        private static readonly Color OffColor = new Color(0.35f, 0.42f, 0.55f);

        public void SetLit(bool on)
        {
            float intensity = on ? 7.5f : 0.15f;
            Color lamp = on ? OnColor : OffColor;
            if (lamps != null)
            {
                for (int i = 0; i < lamps.Length; i++)
                {
                    if (lamps[i] == null) continue;
                    lamps[i].enabled = true;
                    lamps[i].color = lamp;
                    lamps[i].intensity = intensity;
                }
            }

            if (fill != null)
            {
                fill.intensity = on ? 0.55f : 0.12f;
                fill.color = on ? new Color(1f, 0.96f, 0.88f) : new Color(0.45f, 0.55f, 0.75f);
            }

            if (lampShades == null) return;
            Color emission = on ? new Color(2.2f, 1.9f, 1.2f) : new Color(0.05f, 0.05f, 0.06f);
            for (int i = 0; i < lampShades.Length; i++)
            {
                if (lampShades[i] == null) continue;
                var mat = lampShades[i].material;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission);
                }
            }
        }
    }
}
