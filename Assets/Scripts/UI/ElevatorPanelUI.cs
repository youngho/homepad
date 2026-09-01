using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class ElevatorPanelUI : MonoBehaviour
    {
        [SerializeField] private Button callButton;
        [SerializeField] private Text callButtonText;
        [SerializeField] private Text floorText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text titleText;

        public void Bind(Button call, Text callText, Text floor, Text status, Text title)
        {
            callButton = call;
            callButtonText = callText;
            floorText = floor;
            statusText = status;
            titleText = title;
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        private void Start()
        {
            if (titleText != null && WallpadManager.Instance != null)
            {
                titleText.text = $"{WallpadManager.Instance.HouseholdFloor}층 호출";
            }

            if (callButton != null)
            {
                callButton.onClick.RemoveAllListeners();
                callButton.onClick.AddListener(() =>
                {
                    if (WallpadManager.Instance != null && !WallpadManager.Instance.Elevator.isCalled)
                    {
                        WallpadManager.Instance.CallElevator();
                    }
                });
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
