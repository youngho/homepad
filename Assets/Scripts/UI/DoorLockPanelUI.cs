using Homepad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class DoorLockPanelUI : MonoBehaviour
    {
        private static readonly Color Locked = new Color(0.95f, 0.42f, 0.38f);
        private static readonly Color Open = new Color(0.35f, 0.82f, 0.52f);
        private static readonly Color Busy = new Color(0.95f, 0.78f, 0.32f);

        [SerializeField] private Button unlockButton;
        [SerializeField] private Text unlockButtonText;
        [SerializeField] private Text stateText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text titleText;

        public void Bind(Button unlock, Text unlockText, Text state, Text status, Text title)
        {
            unlockButton = unlock;
            unlockButtonText = unlockText;
            stateText = state;
            statusText = status;
            titleText = title;
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        private void Start()
        {
            if (titleText != null) titleText.text = "현관 도어락";

            if (unlockButton != null)
            {
                unlockButton.onClick.RemoveAllListeners();
                unlockButton.onClick.AddListener(() =>
                {
                    WallpadManager.Instance?.UnlockDoor();
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
            var door = WallpadManager.Instance.DoorLock;

            if (stateText != null)
            {
                if (door.isOpen)
                {
                    stateText.text = "열림";
                    stateText.color = Open;
                }
                else if (door.isUnlocking)
                {
                    stateText.text = "해제";
                    stateText.color = Busy;
                }
                else
                {
                    stateText.text = "잠김";
                    stateText.color = Locked;
                }
            }

            if (statusText != null)
            {
                if (door.isOpen) statusText.text = "현관문이 열려 있습니다";
                else if (door.isUnlocking) statusText.text = "도어락 해제 중";
                else statusText.text = "잠금 상태";
            }

            bool canUnlock = !door.isOpen && !door.isUnlocking;
            if (unlockButton != null) unlockButton.interactable = canUnlock;
            if (unlockButtonText != null)
            {
                unlockButtonText.text = door.isOpen ? "열려 있음" : (door.isUnlocking ? "여는 중..." : "문열림");
            }
        }
    }
}
