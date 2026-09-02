using System.Collections.Generic;
using Homepad.Core;
using UnityEngine;

namespace Homepad.Home
{
    public class DioramaRoomRig : MonoBehaviour
    {
        [System.Serializable]
        public class RoomFixture
        {
            public RoomHint hint;
            public Light roomLight;
            public Light heaterLight;
            public HeaterGlow heaterGlow;
            public MeshRenderer lampRenderer;
            public RoomLightRig lightRig;
            public ProceduralCurtain3D curtain3D;
            public Transform ceilingAnchor;
            public Transform wallAnchor;
            public Transform floorAnchor;
        }

        public readonly List<RoomFixture> fixtures = new List<RoomFixture>();
        private readonly Dictionary<RoomHint, RoomFixture> fixtureMap = new Dictionary<RoomHint, RoomFixture>();

        public RoomFixture GetFixture(RoomHint hint)
        {
            fixtureMap.TryGetValue(hint, out var f);
            return f;
        }

        public void RegisterFixture(RoomFixture fixture)
        {
            if (fixture == null) return;
            if (fixtureMap.TryGetValue(fixture.hint, out var existing))
            {
                Merge(existing, fixture);
                return;
            }

            fixtures.Add(fixture);
            fixtureMap[fixture.hint] = fixture;
        }

        public void Clear()
        {
            fixtures.Clear();
            fixtureMap.Clear();
        }

        public void SetAllLights(bool on)
        {
            for (int i = 0; i < fixtures.Count; i++)
            {
                SetLight(fixtures[i].hint, on);
            }
        }

        public void SetLight(RoomHint hint, bool on)
        {
            if (!fixtureMap.TryGetValue(hint, out var f)) return;
            if (f.lightRig != null)
            {
                f.lightRig.SetLit(on);
                return;
            }

            if (f.roomLight != null)
            {
                f.roomLight.enabled = on;
                f.roomLight.intensity = on ? 3.2f : 0.12f;
            }

            if (f.lampRenderer == null) return;
            var mat = f.lampRenderer.material;
            if (mat == null) return;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", on ? new Color(1f, 0.94f, 0.82f) * 2.8f : Color.black);
        }

        public void SetHeating(RoomHint hint, bool on)
        {
            if (!fixtureMap.TryGetValue(hint, out var f)) return;
            if (f.heaterLight != null) f.heaterLight.enabled = on;
            if (f.heaterGlow != null) f.heaterGlow.SetOn(on);
        }

        public void SetCurtain(RoomHint hint, float open)
        {
            if (!fixtureMap.TryGetValue(hint, out var f)) return;
            if (f.curtain3D != null) f.curtain3D.SetOpen(open);
        }

        public Transform GetAnchor(HomeItemKind kind, RoomHint hint)
        {
            if (fixtureMap.TryGetValue(hint, out var f))
            {
                switch (kind)
                {
                    case HomeItemKind.Light:
                    case HomeItemKind.Vent:
                        return f.ceilingAnchor ?? transform;
                    case HomeItemKind.Heating:
                    case HomeItemKind.Gas:
                    case HomeItemKind.Elevator:
                        return f.wallAnchor ?? f.floorAnchor ?? transform;
                    case HomeItemKind.ElectricCurtain:
                        return f.wallAnchor ?? transform;
                    default:
                        return f.floorAnchor ?? transform;
                }
            }

            return transform;
        }

        private static void Merge(RoomFixture dest, RoomFixture src)
        {
            if (src.roomLight != null) dest.roomLight = src.roomLight;
            if (src.heaterLight != null) dest.heaterLight = src.heaterLight;
            if (src.lampRenderer != null) dest.lampRenderer = src.lampRenderer;
            if (src.lightRig != null) dest.lightRig = src.lightRig;
            if (src.heaterGlow != null) dest.heaterGlow = src.heaterGlow;
            if (src.curtain3D != null) dest.curtain3D = src.curtain3D;
            if (src.ceilingAnchor != null) dest.ceilingAnchor = src.ceilingAnchor;
            if (src.wallAnchor != null) dest.wallAnchor = src.wallAnchor;
            if (src.floorAnchor != null) dest.floorAnchor = src.floorAnchor;
        }
    }
}
