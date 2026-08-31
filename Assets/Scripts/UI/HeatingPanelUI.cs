using System.Collections.Generic;
using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    /// <summary>
    /// 난방 제어 패널 UI
    /// </summary>
    public class HeatingPanelUI : MonoBehaviour
    {
        [System.Serializable]
        public class RoomHeatingUI
        {
            public Text roomNameText;
            public Text currentTempText;
            public Text targetTempText;
            public Button tempUpButton;
            public Button tempDownButton;
            public Button powerButton;
            public Text powerText;
            public Button awayButton;
            public Text awayText;
            public Image backgroundCard;
        }

        [SerializeField] private List<RoomHeatingUI> roomUIs = new List<RoomHeatingUI>();

        private void Start()
        {
            for (int i = 0; i < roomUIs.Count; i++)
            {
                int roomId = i + 1;
                var ui = roomUIs[i];

                if (ui.tempUpButton != null)
                {
                    ui.tempUpButton.onClick.AddListener(() =>
                    {
                        var room = WallpadManager.Instance.HeatingRooms[roomId - 1];
                        WallpadManager.Instance.SetHeatingTargetTemp(roomId, room.targetTemp + 0.5f);
                    });
                }

                if (ui.tempDownButton != null)
                {
                    ui.tempDownButton.onClick.AddListener(() =>
                    {
                        var room = WallpadManager.Instance.HeatingRooms[roomId - 1];
                        WallpadManager.Instance.SetHeatingTargetTemp(roomId, room.targetTemp - 0.5f);
                    });
                }

                if (ui.powerButton != null)
                {
                    ui.powerButton.onClick.AddListener(() => WallpadManager.Instance.ToggleHeatingPower(roomId));
                }

                if (ui.awayButton != null)
                {
                    ui.awayButton.onClick.AddListener(() => WallpadManager.Instance.ToggleHeatingAway(roomId));
                }
            }

            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnHeatingChanged += OnHeatingChanged;
                WallpadManager.Instance.OnStateChanged += RefreshAll;
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnHeatingChanged -= OnHeatingChanged;
                WallpadManager.Instance.OnStateChanged -= RefreshAll;
            }
        }

        private void OnHeatingChanged(HeatingState room)
        {
            RefreshAll();
        }

        public void RefreshAll()
        {
            if (WallpadManager.Instance == null) return;
            var rooms = WallpadManager.Instance.HeatingRooms;

            for (int i = 0; i < rooms.Count && i < roomUIs.Count; i++)
            {
                var room = rooms[i];
                var ui = roomUIs[i];

                if (ui.roomNameText != null) ui.roomNameText.text = room.roomName;
                if (ui.currentTempText != null) ui.currentTempText.text = $"{room.currentTemp:F1}℃";
                if (ui.targetTempText != null) ui.targetTempText.text = $"{room.targetTemp:F1}℃";

                if (ui.powerText != null)
                {
                    ui.powerText.text = room.isPowered ? "난방 켬" : "난방 끔";
                    ui.powerText.color = room.isPowered ? new Color(1f, 0.45f, 0.3f) : new Color(0.5f, 0.5f, 0.55f);
                }

                if (ui.awayText != null)
                {
                    ui.awayText.text = room.isAwayMode ? "외출 중" : "일반";
                    ui.awayText.color = room.isAwayMode ? new Color(0.3f, 0.75f, 1f) : new Color(0.5f, 0.5f, 0.55f);
                }
            }
        }
    }
}
