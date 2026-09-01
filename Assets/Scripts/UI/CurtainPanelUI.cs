using Homepad.Home;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class CurtainPanelUI : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Text valueText;
        [SerializeField] private RectTransform track;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button openButton;
        [SerializeField] private Text titleText;

        private string instanceId;

        public void Focus(string itemInstanceId)
        {
            instanceId = itemInstanceId;
            var item = HomeController.Instance != null ? HomeController.Instance.Layout.FindItem(instanceId) : null;
            if (titleText != null && item != null) titleText.text = item.DisplayName;
            BindButtons();
            Refresh();
        }

        private void Start()
        {
            BindButtons();
        }

        private void BindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => SetOpen(0f));
            }

            if (openButton != null)
            {
                openButton.onClick.RemoveAllListeners();
                openButton.onClick.AddListener(() => SetOpen(1f));
            }
        }

        private void Update()
        {
            if (track == null || Mouse.current == null) return;
            if (!Mouse.current.leftButton.isPressed) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(track, Mouse.current.position.ReadValue(), null))
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    track,
                    Mouse.current.position.ReadValue(),
                    null,
                    out var local))
            {
                float width = track.rect.width;
                float t = Mathf.InverseLerp(-width * 0.5f, width * 0.5f, local.x);
                SetOpen(t);
            }
        }

        private void SetOpen(float open)
        {
            if (string.IsNullOrEmpty(instanceId)) return;
            HomeController.Instance?.SetCurtainOpen(instanceId, open);
            Refresh();
        }

        private void Refresh()
        {
            var home = HomeController.Instance;
            var item = home != null ? home.Layout.FindItem(instanceId) : null;
            float open = item != null ? item.CurtainOpen : 0f;
            if (fill != null) fill.fillAmount = open;
            if (valueText != null)
            {
                int pct = Mathf.RoundToInt(open * 100f);
                valueText.text = pct <= 5 ? "닫힘" : pct >= 95 ? "열림" : $"{pct}% 열림";
            }
        }
    }
}
