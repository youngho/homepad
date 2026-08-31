using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class HeatingPanelUI : MonoBehaviour
    {
        private Text[] names;
        private Text[] currentTemps;
        private Text[] targetTemps;
        private Text[] powerTexts;
        private Text[] awayTexts;

        public void Build()
        {
            var root = GetComponent<RectTransform>();
            var rooms = WallpadManager.Instance != null ? WallpadManager.Instance.HeatingRooms : null;
            int count = rooms != null ? rooms.Count : 0;
            names = new Text[count];
            currentTemps = new Text[count];
            targetTemps = new Text[count];
            powerTexts = new Text[count];
            awayTexts = new Text[count];

            for (int i = 0; i < count; i++)
            {
                int col = i % 2;
                int row = i / 2;
                float x0 = col * 0.5f;
                float y1 = 1f - row * 0.5f;
                float y0 = y1 - 0.48f;
                int roomId = rooms[i].roomId;

                var card = UiFactory.Create($"Room{roomId}", root, new Vector2(x0, y0), new Vector2(x0 + 0.5f, y1), new Vector2(8, 8), new Vector2(-8, -8));
                UiFactory.AddImage(card, new Color(0.13f, 0.16f, 0.22f, 1f), false);
                names[i] = UiFactory.CreateLabel("Name", card, new Vector2(0, 0.72f), Vector2.one, new Vector2(20, 0), new Vector2(-20, -12), rooms[i].roomName, 28, Color.white, TextAnchor.MiddleLeft);
                currentTemps[i] = UiFactory.CreateLabel("Current", card, new Vector2(0, 0.42f), new Vector2(0.5f, 0.72f), new Vector2(20, 0), Vector2.zero, "22.0℃", 24, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft);
                targetTemps[i] = UiFactory.CreateLabel("Target", card, new Vector2(0.35f, 0.42f), new Vector2(1, 0.72f), Vector2.zero, new Vector2(-20, 0), "24.0℃", 36, Color.white, TextAnchor.MiddleRight);

                var down = UiFactory.CreateButton("Down", card, new Vector2(0.02f, 0.08f), new Vector2(0.16f, 0.38f), Vector2.zero, Vector2.zero, "−", new Color(0.18f, 0.2f, 0.26f), 32);
                var up = UiFactory.CreateButton("Up", card, new Vector2(0.18f, 0.08f), new Vector2(0.32f, 0.38f), Vector2.zero, Vector2.zero, "+", new Color(0.18f, 0.2f, 0.26f), 32);
                var power = UiFactory.CreateButton("Power", card, new Vector2(0.36f, 0.08f), new Vector2(0.66f, 0.38f), Vector2.zero, Vector2.zero, "난방 켬", new Color(0.78f, 0.38f, 0.22f), 22);
                var away = UiFactory.CreateButton("Away", card, new Vector2(0.68f, 0.08f), new Vector2(0.98f, 0.38f), Vector2.zero, Vector2.zero, "일반", new Color(0.18f, 0.2f, 0.26f), 22);
                powerTexts[i] = power.GetComponentInChildren<Text>();
                awayTexts[i] = away.GetComponentInChildren<Text>();

                down.onClick.AddListener(() =>
                {
                    var room = WallpadManager.Instance.HeatingRooms[roomId - 1];
                    WallpadManager.Instance.SetHeatingTargetTemp(roomId, room.targetTemp - 0.5f);
                });
                up.onClick.AddListener(() =>
                {
                    var room = WallpadManager.Instance.HeatingRooms[roomId - 1];
                    WallpadManager.Instance.SetHeatingTargetTemp(roomId, room.targetTemp + 0.5f);
                });
                power.onClick.AddListener(() => WallpadManager.Instance.ToggleHeatingPower(roomId));
                away.onClick.AddListener(() => WallpadManager.Instance.ToggleHeatingAway(roomId));
            }

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
            if (WallpadManager.Instance == null || names == null) return;
            var rooms = WallpadManager.Instance.HeatingRooms;
            for (int i = 0; i < rooms.Count && i < names.Length; i++)
            {
                var room = rooms[i];
                if (names[i] != null) names[i].text = room.roomName;
                if (currentTemps[i] != null) currentTemps[i].text = $"현재 {room.currentTemp:F1}℃";
                if (targetTemps[i] != null) targetTemps[i].text = $"{room.targetTemp:F1}℃";
                if (powerTexts[i] != null)
                {
                    powerTexts[i].text = room.isPowered ? "난방 켬" : "난방 끔";
                }

                if (awayTexts[i] != null)
                {
                    awayTexts[i].text = room.isAwayMode ? "외출 중" : "일반";
                }
            }
        }
    }
}
