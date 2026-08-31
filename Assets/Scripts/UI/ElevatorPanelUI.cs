using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class ElevatorPanelUI : MonoBehaviour
    {
        private Button callButton;
        private Text callButtonText;
        private Text floorText;
        private Text statusText;

        public void Build()
        {
            var root = GetComponent<RectTransform>();
            var card = UiFactory.Create("Card", root, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.92f), Vector2.zero, Vector2.zero);
            UiFactory.AddImage(card, new Color(0.13f, 0.16f, 0.22f), false);

            int floor = WallpadManager.Instance != null ? WallpadManager.Instance.HouseholdFloor : 12;
            UiFactory.CreateLabel("Title", card, new Vector2(0, 0.78f), Vector2.one, Vector2.zero, Vector2.zero, $"{floor}층 호출", 28, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            floorText = UiFactory.CreateLabel("Floor", card, new Vector2(0, 0.42f), new Vector2(1, 0.78f), Vector2.zero, Vector2.zero, "1F", 88, Color.white, TextAnchor.MiddleCenter);
            statusText = UiFactory.CreateLabel("Status", card, new Vector2(0, 0.28f), new Vector2(1, 0.42f), Vector2.zero, Vector2.zero, "대기 상태", 26, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);

            callButton = UiFactory.CreateButton("Call", card, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.24f), Vector2.zero, Vector2.zero, "엘리베이터 호출", new Color(0.18f, 0.45f, 0.9f), 28);
            callButtonText = callButton.GetComponentInChildren<Text>();
            callButton.onClick.AddListener(() =>
            {
                if (WallpadManager.Instance != null && !WallpadManager.Instance.Elevator.isCalled)
                {
                    WallpadManager.Instance.CallElevator();
                }
            });

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
            if (WallpadManager.Instance == null) return;
            var elevator = WallpadManager.Instance.Elevator;
            if (callButtonText != null)
            {
                callButtonText.text = elevator.isCalled ? "호출 중..." : "엘리베이터 호출";
            }

            if (callButton != null)
            {
                callButton.interactable = !elevator.isCalled;
            }

            if (floorText != null)
            {
                floorText.text = $"{elevator.currentFloor}F";
            }

            if (statusText != null)
            {
                if (elevator.isCalled)
                {
                    statusText.text = elevator.direction == ElevatorDirection.Down ? "하강 중" : "상승 중";
                }
                else
                {
                    statusText.text = "대기 상태";
                }
            }
        }
    }
}
