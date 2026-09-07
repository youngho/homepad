using UnityEngine;

namespace Homepad.Home
{
    public class DoorLockVisual : MonoBehaviour
    {
        [SerializeField] private Transform doorLeaf;
        [SerializeField] private Renderer led;

        private float targetAngle;
        private float currentAngle;

        public void SetOpen(bool open)
        {
            targetAngle = open ? -82f : 0f;
            SetLed(open);
        }

        private void Update()
        {
            if (doorLeaf == null) return;
            if (Mathf.Approximately(currentAngle, targetAngle)) return;
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, 220f * Time.deltaTime);
            doorLeaf.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
        }

        private void SetLed(bool open)
        {
            if (led == null) return;
            var mat = led.material;
            Color emission = open ? new Color(0.18f, 2.1f, 0.45f) : new Color(1.8f, 0.18f, 0.12f);
            Color baseColor = open ? new Color(0.22f, 0.72f, 0.38f) : new Color(0.72f, 0.18f, 0.16f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
        }
    }
}
