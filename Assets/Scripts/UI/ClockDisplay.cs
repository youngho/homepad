using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class ClockDisplay : MonoBehaviour
    {
        [SerializeField] private Text timeText;
        [SerializeField] private Text dateText;

        private float timer;

        private void Start()
        {
            UpdateDateTime();
        }

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer < 1f) return;
            timer = 0f;
            UpdateDateTime();
        }

        private void UpdateDateTime()
        {
            DateTime now = DateTime.Now;
            if (timeText != null)
            {
                timeText.text = now.ToString("HH:mm:ss");
            }

            if (dateText != null)
            {
                dateText.text = now.ToString("yyyy년 MM월 dd일 (ddd)", CultureInfo.GetCultureInfo("ko-KR"));
            }
        }
    }
}
