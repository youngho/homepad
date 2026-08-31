using System.Collections;
using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    /// <summary>
    /// 엘리베이터 호출 및 상태 표시 패널 UI
    /// </summary>
    public class ElevatorPanelUI : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] private Button callButton;
        [SerializeField] private Text callButtonText;
        [SerializeField] private Text currentFloorText;
        [SerializeField] private Text statusText;
        [SerializeField] private Image directionIcon;

        private Coroutine simulationCoroutine;

        private void Start()
        {
            if (callButton != null)
            {
                callButton.onClick.AddListener(OnCallButtonClicked);
            }

            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnElevatorChanged += OnElevatorChanged;
                WallpadManager.Instance.OnStateChanged += RefreshAll;
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnElevatorChanged -= OnElevatorChanged;
                WallpadManager.Instance.OnStateChanged -= RefreshAll;
            }
        }

        private void OnCallButtonClicked()
        {
            var el = WallpadManager.Instance.Elevator;
            if (!el.isCalled)
            {
                WallpadManager.Instance.CallElevator(12); // 예: 현재 세대 12층 호출
                if (simulationCoroutine != null) StopCoroutine(simulationCoroutine);
                simulationCoroutine = StartCoroutine(ElevatorSimulationRoutine());
            }
        }

        private IEnumerator ElevatorSimulationRoutine()
        {
            var el = WallpadManager.Instance.Elevator;
            el.direction = ElevatorDirection.Up;
            
            for (int f = 1; f <= 12; f++)
            {
                el.currentFloor = f;
                RefreshAll();
                yield return new WaitForSeconds(0.7f);
            }

            el.direction = ElevatorDirection.Stop;
            if (statusText != null) statusText.text = "엘리베이터가 도착했습니다.";
            yield return new WaitForSeconds(3.0f);

            WallpadManager.Instance.ResetElevatorCall();
            RefreshAll();
        }

        private void OnElevatorChanged(ElevatorState el) => RefreshAll();

        public void RefreshAll()
        {
            if (WallpadManager.Instance == null) return;
            var el = WallpadManager.Instance.Elevator;

            if (callButtonText != null)
            {
                callButtonText.text = el.isCalled ? "호출 중..." : "엘리베이터 호출";
            }
            if (callButton != null)
            {
                callButton.interactable = !el.isCalled;
            }

            if (currentFloorText != null)
            {
                currentFloorText.text = $"{el.currentFloor}F";
            }

            if (statusText != null)
            {
                if (el.isCalled)
                {
                    statusText.text = el.direction == ElevatorDirection.Up ? "상승 중 (이동 중)" : "하강 중";
                }
                else
                {
                    statusText.text = "대기 상태";
                }
            }
        }
    }
}
