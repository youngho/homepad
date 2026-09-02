using System;
using Homepad.Core;
using Homepad.Home;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public enum DayPeriod
    {
        Night,
        Dawn,
        Day,
        Dusk
    }

    [DefaultExecutionOrder(80)]
    public class PadDayCycle : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Light keyLight;
        [SerializeField] private Light fillLight;
        [SerializeField] private Image headerChrome;
        [SerializeField] private Image padWash;
        [SerializeField] private bool previewOverride;
        [SerializeField] [Range(0f, 24f)] private float previewHour = 12f;

        private static readonly Look Night = new Look(
            new Color(0.055f, 0.06f, 0.085f),
            new Color(0.10f, 0.115f, 0.145f, 1f),
            new Color(0.55f, 0.62f, 0.78f), 0.38f,
            new Color(0.35f, 0.42f, 0.62f), 0.10f,
            new Color(0.05f, 0.055f, 0.07f),
            new Color(0.08f, 0.10f, 0.18f, 0.04f));

        private static readonly Look Dawn = new Look(
            new Color(0.28f, 0.34f, 0.48f),
            new Color(0.16f, 0.20f, 0.32f, 1f),
            new Color(1.00f, 0.78f, 0.62f), 0.85f,
            new Color(0.55f, 0.62f, 0.88f), 0.28f,
            new Color(0.16f, 0.18f, 0.24f),
            new Color(0.45f, 0.42f, 0.70f, 0.12f));

        private static readonly Look Day = new Look(
            new Color(0.62f, 0.72f, 0.82f),
            new Color(0.22f, 0.26f, 0.31f, 1f),
            new Color(1.00f, 0.97f, 0.92f), 1.25f,
            new Color(0.78f, 0.86f, 1.00f), 0.42f,
            new Color(0.18f, 0.20f, 0.23f),
            new Color(0.85f, 0.92f, 1.00f, 0.08f));

        private static readonly Look Dusk = new Look(
            new Color(0.32f, 0.16f, 0.14f),
            new Color(0.24f, 0.13f, 0.11f, 1f),
            new Color(1.00f, 0.52f, 0.28f), 0.95f,
            new Color(0.45f, 0.28f, 0.55f), 0.22f,
            new Color(0.14f, 0.08f, 0.07f),
            new Color(0.90f, 0.35f, 0.18f, 0.14f));

        private Keyframe[] keys;
        private int lastCalculatedDay = -1;

        public DayPeriod Period { get; private set; }
        public SolarCalculator.SolarTimes SolarTimes { get; private set; }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (LocationProvider.Instance != null)
            {
                LocationProvider.Instance.OnLocationChanged += RebuildSolarKeyframes;
            }

            RebuildSolarKeyframes();
            Apply(Evaluate(CurrentHour()));
        }

        private void OnDestroy()
        {
            if (LocationProvider.Instance != null)
            {
                LocationProvider.Instance.OnLocationChanged -= RebuildSolarKeyframes;
            }
        }

        public void RebuildSolarKeyframes()
        {
            var now = DateTime.Now;
            double lat = LocationProvider.Instance != null ? LocationProvider.Instance.Latitude : LocationProvider.DefaultLat;
            double lng = LocationProvider.Instance != null ? LocationProvider.Instance.Longitude : LocationProvider.DefaultLng;

            SolarTimes = SolarCalculator.Calculate(now, lat, lng);
            lastCalculatedDay = now.DayOfYear;

            float dawn = SolarTimes.DawnHour;
            float sunrise = SolarTimes.SunriseHour;
            float sunset = SolarTimes.SunsetHour;
            float dusk = SolarTimes.DuskHour;

            float dawnStart = Mathf.Max(0f, dawn - 0.6f);
            float dayFull = sunrise + 0.6f;
            float duskStart = Mathf.Max(dayFull, sunset - 0.7f);
            float nightFull = dusk + 0.5f;

            keys = new Keyframe[]
            {
                new Keyframe(0.00f, Night),
                new Keyframe(dawnStart, Night),
                new Keyframe(dawn, Dawn),
                new Keyframe(dayFull, Day),
                new Keyframe(duskStart, Day),
                new Keyframe(sunset, Dusk),
                new Keyframe(nightFull, Night),
                new Keyframe(24.00f, Night)
            };
        }

        private void LateUpdate()
        {
            Apply(Evaluate(CurrentHour()));
        }

        private float CurrentHour()
        {
            if (previewOverride) return Mathf.Repeat(previewHour, 24f);
            var now = DateTime.Now;
            return now.Hour + now.Minute / 60f + now.Second / 3600f;
        }

        private Look Evaluate(float hour)
        {
            if (keys == null || DateTime.Now.DayOfYear != lastCalculatedDay)
            {
                RebuildSolarKeyframes();
            }

            hour = Mathf.Repeat(hour, 24f);
            for (int i = 0; i < keys.Length - 1; i++)
            {
                var a = keys[i];
                var b = keys[i + 1];
                if (hour < a.hour || hour > b.hour) continue;
                float span = b.hour - a.hour;
                float t = span <= 0.0001f ? 0f : (hour - a.hour) / span;
                t = Smooth(t);
                return Look.Lerp(a.look, b.look, t);
            }

            return Night;
        }

        private DayPeriod Classify(float hour)
        {
            if (keys == null || keys.Length == 0) return DayPeriod.Day;
            if (hour >= SolarTimes.DawnHour && hour < SolarTimes.SunriseHour + 0.4f) return DayPeriod.Dawn;
            if (hour >= SolarTimes.SunriseHour + 0.4f && hour < SolarTimes.SunsetHour - 0.4f) return DayPeriod.Day;
            if (hour >= SolarTimes.SunsetHour - 0.4f && hour < SolarTimes.DuskHour + 0.3f) return DayPeriod.Dusk;
            return DayPeriod.Night;
        }

        private void Apply(Look look)
        {
            Period = Classify(CurrentHour());

            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = look.voidColor;
            }

            if (headerChrome != null) headerChrome.color = look.chrome;
            if (padWash != null) padWash.color = look.wash;

            if (keyLight != null)
            {
                keyLight.color = look.keyColor;
                keyLight.intensity = look.keyIntensity;
            }

            if (fillLight != null)
            {
                fillLight.color = look.fillColor;
                fillLight.intensity = look.fillIntensity;
            }

            var builder = HomeController.Instance != null
                ? HomeController.Instance.GetComponent<IsometricHomeBuilder>()
                : FindFirstObjectByType<IsometricHomeBuilder>();
            builder?.SetGroundColor(look.ground);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private readonly struct Keyframe
        {
            public readonly float hour;
            public readonly Look look;

            public Keyframe(float hour, Look look)
            {
                this.hour = hour;
                this.look = look;
            }
        }

        private readonly struct Look
        {
            public readonly Color voidColor;
            public readonly Color chrome;
            public readonly Color keyColor;
            public readonly float keyIntensity;
            public readonly Color fillColor;
            public readonly float fillIntensity;
            public readonly Color ground;
            public readonly Color wash;

            public Look(Color voidColor, Color chrome, Color keyColor, float keyIntensity,
                Color fillColor, float fillIntensity, Color ground, Color wash)
            {
                this.voidColor = voidColor;
                this.chrome = chrome;
                this.keyColor = keyColor;
                this.keyIntensity = keyIntensity;
                this.fillColor = fillColor;
                this.fillIntensity = fillIntensity;
                this.ground = ground;
                this.wash = wash;
            }

            public static Look Lerp(Look a, Look b, float t)
            {
                return new Look(
                    Color.Lerp(a.voidColor, b.voidColor, t),
                    Color.Lerp(a.chrome, b.chrome, t),
                    Color.Lerp(a.keyColor, b.keyColor, t),
                    Mathf.Lerp(a.keyIntensity, b.keyIntensity, t),
                    Color.Lerp(a.fillColor, b.fillColor, t),
                    Mathf.Lerp(a.fillIntensity, b.fillIntensity, t),
                    Color.Lerp(a.ground, b.ground, t),
                    Color.Lerp(a.wash, b.wash, t));
            }
        }
    }
}
