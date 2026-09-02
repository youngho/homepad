using Homepad.Core;
using UnityEngine;

namespace Homepad.Home
{
    public class ApartmentLightRig : MonoBehaviour
    {
        [Header("Room Lights")]
        public Light livingLight;
        public Light masterLight;
        public Light bedroomLight;
        public Light bedroom2Light;
        public Light kitchenLight;
        public Light hallwayLight;

        [Header("Heater Warm Glow Lights")]
        public Light livingHeaterLight;
        public Light masterHeaterLight;
        public Light bedroomHeaterLight;

        [Header("Light Strip Emission Renderers")]
        public MeshRenderer luzRipadoRenderer;
        public MeshRenderer luzParedeRenderer;

        [Header("Curtain")]
        public Transform curtainTransform;
        public CurtainCloth curtainCloth;

        [Header("Device Anchors")]
        public Transform livingAnchor;
        public Transform masterAnchor;
        public Transform bedroomAnchor;
        public Transform bedroom2Anchor;
        public Transform kitchenAnchor;
        public Transform entranceAnchor;
        public Transform curtainAnchor;

        private static readonly Color WarmEmissionOn = new Color(1.0f, 0.92f, 0.78f) * 3.2f;
        private static readonly Color HeaterColor = new Color(1.0f, 0.38f, 0.12f);

        private bool heatersReady;

        private void Awake()
        {
            ApplyBaseline();
        }

        private void Start()
        {
            EnsureReferences();
        }

        private void ApplyBaseline()
        {
            EnsureReferences();
            SetAllRoomLights(false);
            SetHeating(RoomHint.Living, false);
            SetHeating(RoomHint.Master, false);
            SetHeating(RoomHint.Bedroom, false);
        }

        public void EnsureReferences()
        {
            if (curtainTransform == null)
            {
                foreach (var child in GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.IndexOf("cortina", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || child.name.Contains("Object_7"))
                    {
                        curtainTransform = child;
                        break;
                    }
                }
            }

            if (curtainTransform != null && curtainCloth == null)
            {
                curtainCloth = curtainTransform.GetComponent<CurtainCloth>()
                               ?? curtainTransform.gameObject.AddComponent<CurtainCloth>();
            }

            if (curtainCloth != null)
            {
                curtainCloth.Initialize();
            }

            if (luzRipadoRenderer == null || luzParedeRenderer == null)
            {
                foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.gameObject.name;
                    if (luzRipadoRenderer == null && (n.Contains("Object_11") || n.IndexOf("ripado", System.StringComparison.OrdinalIgnoreCase) >= 0))
                        luzRipadoRenderer = r;
                    if (luzParedeRenderer == null && (n.Contains("Object_13") || n.IndexOf("parede", System.StringComparison.OrdinalIgnoreCase) >= 0))
                        luzParedeRenderer = r;
                }
            }

            EnsureHeaterLights();

            if (hallwayLight != null)
            {
                hallwayLight.intensity = 0.12f;
                hallwayLight.range = 8f;
                hallwayLight.shadows = LightShadows.None;
                hallwayLight.enabled = true;
            }
        }

        public void SetLight(RoomHint hint, bool on)
        {
            EnsureReferences();

            switch (hint)
            {
                case RoomHint.Living:
                    SetPoint(livingLight, on, 3.4f, 8f);
                    SetEmission(luzRipadoRenderer, on);
                    break;
                case RoomHint.Master:
                    SetPoint(masterLight, on, 2.4f, 6.5f);
                    SetEmission(luzParedeRenderer, on);
                    break;
                case RoomHint.Bedroom:
                    SetPoint(bedroomLight, on, 2.2f, 6.5f);
                    break;
                case RoomHint.Bedroom2:
                    SetPoint(bedroom2Light, on, 2.2f, 6.5f);
                    break;
                case RoomHint.Kitchen:
                    SetPoint(kitchenLight, on, 2.4f, 6.5f);
                    break;
            }
        }

        public void SetHeating(RoomHint hint, bool on)
        {
            EnsureHeaterLights();
            switch (hint)
            {
                case RoomHint.Living:
                    if (livingHeaterLight != null) livingHeaterLight.enabled = on;
                    break;
                case RoomHint.Master:
                    if (masterHeaterLight != null) masterHeaterLight.enabled = on;
                    break;
                case RoomHint.Bedroom:
                    if (bedroomHeaterLight != null) bedroomHeaterLight.enabled = on;
                    break;
            }
        }

        public void SetCurtain(float openPercent)
        {
            EnsureReferences();
            if (curtainCloth != null)
            {
                curtainCloth.SetOpen(openPercent);
            }
        }

        public Transform GetAnchor(HomeItemKind kind, RoomHint hint)
        {
            if (kind == HomeItemKind.ElectricCurtain && curtainAnchor != null) return curtainAnchor;
            if (kind == HomeItemKind.ElectricCurtain && curtainTransform != null) return curtainTransform;

            return hint switch
            {
                RoomHint.Living => livingAnchor ?? (livingLight != null ? livingLight.transform : transform),
                RoomHint.Master => masterAnchor ?? (masterLight != null ? masterLight.transform : transform),
                RoomHint.Bedroom => bedroomAnchor ?? (bedroomLight != null ? bedroomLight.transform : transform),
                RoomHint.Bedroom2 => bedroom2Anchor ?? (bedroom2Light != null ? bedroom2Light.transform : transform),
                RoomHint.Kitchen => kitchenAnchor ?? (kitchenLight != null ? kitchenLight.transform : transform),
                RoomHint.Entrance => entranceAnchor ?? transform,
                _ => livingAnchor ?? transform
            };
        }

        private void EnsureHeaterLights()
        {
            if (heatersReady
                && livingHeaterLight != null
                && masterHeaterLight != null
                && bedroomHeaterLight != null)
            {
                return;
            }

            Transform heaters = transform.Find("Heaters");
            if (heaters == null)
            {
                var go = new GameObject("Heaters");
                go.transform.SetParent(transform, false);
                heaters = go.transform;
            }

            livingHeaterLight = EnsureHeaterLight(livingHeaterLight, heaters, "HeatGlow_Living", livingLight, new Vector3(-1.1f, 0.42f, 3.2f));
            masterHeaterLight = EnsureHeaterLight(masterHeaterLight, heaters, "HeatGlow_Master", masterLight, new Vector3(2.2f, 0.42f, 2.2f));
            bedroomHeaterLight = EnsureHeaterLight(bedroomHeaterLight, heaters, "HeatGlow_Bedroom", bedroomLight, new Vector3(2.4f, 0.42f, -0.6f));
            heatersReady = true;
        }

        private static Light EnsureHeaterLight(Light existing, Transform parent, string name, Light roomLight, Vector3 fallback)
        {
            Light light = existing;
            if (light == null)
            {
                var t = parent.Find(name);
                if (t != null) light = t.GetComponent<Light>();
            }

            if (light == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                light = go.AddComponent<Light>();
            }

            Vector3 pos = fallback;
            if (roomLight != null)
            {
                pos = roomLight.transform.localPosition;
                pos.y = 0.42f;
            }

            light.transform.localPosition = pos;
            light.type = LightType.Point;
            light.color = HeaterColor;
            light.intensity = 2.6f;
            light.range = 4.8f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static void SetPoint(Light light, bool on, float intensity, float range)
        {
            if (light == null) return;
            light.intensity = intensity;
            light.range = range;
            light.enabled = on;
        }

        private static void SetEmission(MeshRenderer renderer, bool on)
        {
            if (renderer == null) return;
            var mat = renderer.material;
            if (mat == null) return;

            if (on)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", WarmEmissionOn);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
        }

        private void SetAllRoomLights(bool on)
        {
            SetPoint(livingLight, on, 3.4f, 8f);
            SetPoint(masterLight, on, 2.4f, 6.5f);
            SetPoint(bedroomLight, on, 2.2f, 6.5f);
            SetPoint(bedroom2Light, on, 2.2f, 6.5f);
            SetPoint(kitchenLight, on, 2.4f, 6.5f);
            SetEmission(luzRipadoRenderer, on);
            SetEmission(luzParedeRenderer, on);
        }
    }
}
