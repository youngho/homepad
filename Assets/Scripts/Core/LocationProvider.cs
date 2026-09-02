using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Homepad.Core
{
    public class LocationProvider : MonoBehaviour
    {
        public static LocationProvider Instance { get; private set; }

        public const double DefaultLat = 37.5665; // Seoul
        public const double DefaultLng = 126.9780;

        [SerializeField] private double latitude = DefaultLat;
        [SerializeField] private double longitude = DefaultLng;
        [SerializeField] private string cityName = "서울";

        public double Latitude => latitude;
        public double Longitude => longitude;
        public string CityName => cityName;

        public event Action OnLocationChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LoadSavedLocation();
                StartCoroutine(TryFetchGeoLocation());
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void SetLocation(double lat, double lng, string name = "")
        {
            latitude = lat;
            longitude = lng;
            if (!string.IsNullOrEmpty(name)) cityName = name;

            PlayerPrefs.SetFloat("Geo_Lat", (float)lat);
            PlayerPrefs.SetFloat("Geo_Lng", (float)lng);
            PlayerPrefs.SetString("Geo_City", cityName);
            PlayerPrefs.Save();

            OnLocationChanged?.Invoke();
        }

        private void LoadSavedLocation()
        {
            if (PlayerPrefs.HasKey("Geo_Lat"))
            {
                latitude = PlayerPrefs.GetFloat("Geo_Lat", (float)DefaultLat);
                longitude = PlayerPrefs.GetFloat("Geo_Lng", (float)DefaultLng);
                cityName = PlayerPrefs.GetString("Geo_City", "서울");
            }
        }

        private IEnumerator TryFetchGeoLocation()
        {
            // Optional background IP Geolocation query (non-blocking, fallback gracefully to default)
            using var req = UnityWebRequest.Get("https://ipapi.co/json/");
            req.timeout = 4;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = req.downloadHandler.text;
                    var data = JsonUtility.FromJson<IpGeoResponse>(json);
                    if (data != null && data.latitude != 0 && data.longitude != 0)
                    {
                        latitude = data.latitude;
                        longitude = data.longitude;
                        cityName = !string.IsNullOrEmpty(data.city) ? data.city : cityName;
                        OnLocationChanged?.Invoke();
                    }
                }
                catch
                {
                    // Ignore and keep current
                }
            }
        }

        [Serializable]
        private class IpGeoResponse
        {
            public double latitude;
            public double longitude;
            public string city;
        }
    }
}
