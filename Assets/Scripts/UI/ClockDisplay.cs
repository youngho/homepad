using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    /// <summary>
    /// 상단바 실시간 시계 및 날짜 표출 컴포넌트
    /// </summary>
    public class ClockDisplay : MonoBehaviour
    {
        [SerializeField] private Text timeText;
        [SerializeField] private Text dateText;

        private float timer = 0f;

        private void Start()
        {
            UpdateDateTime();
        }

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer >= 1.0f)
            {
                timer = 0f;
                UpdateDateTime();
            }
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
                CultureInfo koreanCulture = new CultureInfo("ko-KR");
                dateText.text = now.ToString("yyyy년 MM월 dd일 (ddd)", koreanCulture);
            }
        }
    }
}
