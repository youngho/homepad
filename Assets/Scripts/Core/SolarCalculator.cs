using System;
using UnityEngine;

namespace Homepad.Core
{
    public static class SolarCalculator
    {
        public readonly struct SolarTimes
        {
            public readonly float DawnHour;
            public readonly float SunriseHour;
            public readonly float NoonHour;
            public readonly float SunsetHour;
            public readonly float DuskHour;

            public SolarTimes(float dawn, float sunrise, float noon, float sunset, float dusk)
            {
                DawnHour = dawn;
                SunriseHour = sunrise;
                NoonHour = noon;
                SunsetHour = sunset;
                DuskHour = dusk;
            }

            public override string ToString()
            {
                return $"Dawn: {FormatHour(DawnHour)}, Sunrise: {FormatHour(SunriseHour)}, Noon: {FormatHour(NoonHour)}, Sunset: {FormatHour(SunsetHour)}, Dusk: {FormatHour(DuskHour)}";
            }

            private static string FormatHour(float h)
            {
                int hour = (int)h;
                int min = (int)((h - hour) * 60f);
                return $"{hour:D2}:{min:D2}";
            }
        }

        // NOAA Solar Zenith Angles (Degrees)
        private const double OfficialZenith = 90.833; // 90°50' for official sunrise/sunset (atmospheric refraction)
        private const double CivilZenith = 96.0;      // 6° below horizon for civil twilight (dawn/dusk)

        public static SolarTimes Calculate(DateTime date, double latitude, double longitude)
        {
            // Timezone offset in hours
            double timezoneOffset = TimeZoneInfo.Local.GetUtcOffset(date).TotalHours;

            int dayOfYear = date.DayOfYear;

            double sunrise = CalcSolarTime(dayOfYear, latitude, longitude, timezoneOffset, OfficialZenith, isSunrise: true);
            double sunset = CalcSolarTime(dayOfYear, latitude, longitude, timezoneOffset, OfficialZenith, isSunrise: false);
            double dawn = CalcSolarTime(dayOfYear, latitude, longitude, timezoneOffset, CivilZenith, isSunrise: true);
            double dusk = CalcSolarTime(dayOfYear, latitude, longitude, timezoneOffset, CivilZenith, isSunrise: false);

            double noon = (sunrise + sunset) * 0.5;

            return new SolarTimes((float)dawn, (float)sunrise, (float)noon, (float)sunset, (float)dusk);
        }

        private static double CalcSolarTime(int dayOfYear, double lat, double lng, double tz, double zenith, bool isSunrise)
        {
            // 1. Convert longitude to hour value and estimate approximate time
            double lngHour = lng / 15.0;
            double t = isSunrise
                ? dayOfYear + ((6.0 - lngHour) / 24.0)
                : dayOfYear + ((18.0 - lngHour) / 24.0);

            // 2. Calculate Sun's mean anomaly
            double M = (0.9856 * t) - 3.289;

            // 3. Calculate Sun's true longitude
            double L = M + (1.916 * Math.Sin(ToRad(M))) + (0.020 * Math.Sin(ToRad(2 * M))) + 282.634;
            L = NormalizeDeg(L);

            // 4. Calculate Sun's right ascension (RA)
            double RA = ToDeg(Math.Atan(0.91764 * Math.Tan(ToRad(L))));
            RA = NormalizeDeg(RA);

            // Right ascension value needs to be in the same quadrant as L
            double lQuadrant = Math.Floor(L / 90.0) * 90.0;
            double raQuadrant = Math.Floor(RA / 90.0) * 90.0;
            RA = RA + (lQuadrant - raQuadrant);
            RA = RA / 15.0; // convert to hours

            // 5. Calculate Sun's declination
            double sinDec = 0.39782 * Math.Sin(ToRad(L));
            double cosDec = Math.Cos(Math.Asin(sinDec));

            // 6. Calculate Sun's local hour angle
            double cosH = (Math.Cos(ToRad(zenith)) - (sinDec * Math.Sin(ToRad(lat)))) / (cosDec * Math.Cos(ToRad(lat)));

            // Clamp in case of polar day/night
            if (cosH > 1.0) cosH = 1.0;
            if (cosH < -1.0) cosH = -1.0;

            // 7. Calculate H and convert into hours
            double H = isSunrise
                ? 360.0 - ToDeg(Math.Acos(cosH))
                : ToDeg(Math.Acos(cosH));
            H = H / 15.0;

            // 8. Calculate local mean time of rising/setting
            double T = H + RA - (0.06571 * t) - 6.622;

            // 9. Adjust back to UTC, then to local timezone
            double UT = T - lngHour;
            double localT = UT + tz;

            // Normalize between 0 and 24 hours
            localT = (localT % 24.0 + 24.0) % 24.0;
            return localT;
        }

        private static double ToRad(double deg) => deg * (Math.PI / 180.0);
        private static double ToDeg(double rad) => rad * (180.0 / Math.PI);
        private static double NormalizeDeg(double deg) => (deg % 360.0 + 360.0) % 360.0;
    }
}
