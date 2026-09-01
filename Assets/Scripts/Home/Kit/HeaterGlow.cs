using UnityEngine;

namespace Homepad.Home
{
    public class HeaterGlow : MonoBehaviour
    {
        [SerializeField] private Renderer[] plates;

        public void SetOn(bool on)
        {
            if (plates == null) return;
            Color emission = on ? new Color(2.4f, 0.55f, 0.18f) : Color.black;
            Color baseColor = on ? new Color(0.55f, 0.22f, 0.12f) : new Color(0.42f, 0.4f, 0.38f);
            for (int i = 0; i < plates.Length; i++)
            {
                if (plates[i] == null) continue;
                var mat = plates[i].material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission);
                }
            }
        }
    }
}
