using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class HeatingPanelUI : MonoBehaviour
    {
        [SerializeField] private RoomSlot[] rooms;

        [System.Serializable]
        public class RoomSlot
        {
            public int roomId;
            public Text nameText;
            public Text currentTempText;
            public Text targetTempText;
            public Button downButton;
            public Button upButton;
            public Button powerButton;
            public Button awayButton;
            public Text powerText;
            public Text awayText;
        }

        public void Focus(int roomId)
        {
            if (rooms != null && rooms.Length > 0)
            {
                rooms[0].roomId = roomId;
            }

            BindClicks();
            RefreshAll();
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        private void Start()
        {
            BindClicks();
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnStateChanged += RefreshAll;
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance == null) return;
            WallpadManager.Instance.OnStateChanged -= RefreshAll;
        }

        public void RefreshAll()
        {
            if (WallpadManager.Instance == null || rooms == null) return;
            for (int i = 0; i < rooms.Length; i++)
            {
                var slot = rooms[i];
                var room = FindRoom(slot.roomId);
                if (room == null) continue;

                if (slot.nameText != null) slot.nameText.text = room.roomName;
                if (slot.currentTempText != null) slot.currentTempText.text = $"현재 {room.currentTemp:F1}℃";
                if (slot.targetTempText != null) slot.targetTempText.text = $"{room.targetTemp:F1}℃";
                if (slot.powerText != null) slot.powerText.text = room.isPowered ? "난방 켬" : "난방 끔";
                if (slot.awayText != null) slot.awayText.text = room.isAwayMode ? "외출 중" : "일반";

                if (slot.powerButton != null)
                {
                    var image = slot.powerButton.GetComponent<Image>();
                    if (image != null) image.color = room.isPowered ? new Color(0.337f, 0.588f, 0.408f) : new Color(0.10f, 0.12f, 0.16f);
                }

                if (slot.awayButton != null)
                {
                    var image = slot.awayButton.GetComponent<Image>();
                    if (image != null) image.color = room.isAwayMode ? new Color(0.455f, 0.612f, 0.773f) : new Color(0.10f, 0.12f, 0.16f);
                }
            }
        }

        private void BindClicks()
        {
            if (rooms == null) return;
            for (int i = 0; i < rooms.Length; i++)
            {
                int roomId = rooms[i].roomId;
                BindButton(rooms[i].downButton, () =>
                {
                    var room = FindRoom(roomId);
                    if (room == null) return;
                    WallpadManager.Instance.SetHeatingTargetTemp(roomId, room.targetTemp - 0.5f);
                });
                BindButton(rooms[i].upButton, () =>
                {
                    var room = FindRoom(roomId);
                    if (room == null) return;
                    WallpadManager.Instance.SetHeatingTargetTemp(roomId, room.targetTemp + 0.5f);
                });
                BindButton(rooms[i].powerButton, () => WallpadManager.Instance.ToggleHeatingPower(roomId));
                BindButton(rooms[i].awayButton, () => WallpadManager.Instance.ToggleHeatingAway(roomId));
            }
        }

        private static HeatingState FindRoom(int roomId)
        {
            if (WallpadManager.Instance == null) return null;
            var list = WallpadManager.Instance.HeatingRooms;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].roomId == roomId) return list[i];
            }

            return null;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
