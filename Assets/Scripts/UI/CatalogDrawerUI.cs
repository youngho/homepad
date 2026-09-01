using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class CatalogDrawerUI : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private GameObject scrim;
        [SerializeField] private GameObject edge;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private float width = 420f;
        [SerializeField] private float edgeGrabPx = 64f;
        [SerializeField] private float snap = 0.35f;

        private float progress;
        private float target;
        private bool dragging;
        private bool maybeDrag;
        private float dragStartX;
        private float dragStartY;
        private float dragStartProgress;

        public bool IsOpen => progress > 0.5f;

        private void Awake()
        {
            if (panel == null) panel = transform as RectTransform;
            Bind(openButton, Toggle);
            Bind(closeButton, Close);
            if (edge != null)
            {
                Bind(edge.GetComponent<Button>(), Open);
            }
            if (scrim != null)
            {
                var button = scrim.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(Close);
                }
            }

            target = 0f;
            progress = 0f;
            Apply(1f);
        }

        public void Open()
        {
            GetComponentInParent<DeviceOverlayUI>()?.Hide();
            if (settingsPanel != null) settingsPanel.SetActive(false);
            target = 1f;
        }

        public void Close()
        {
            target = 0f;
            dragging = false;
            maybeDrag = false;
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void Update()
        {
            HandlePointer();
            if (!dragging)
            {
                Apply(Time.unscaledDeltaTime * 12f);
            }
        }

        private void HandlePointer()
        {
            if (!TryPointer(out Vector2 pos, out bool down, out bool held, out bool up))
            {
                return;
            }

            if (down)
            {
                bool fromEdge = !IsOpen && pos.x >= Screen.width - edgeGrabPx;
                bool fromPanel = IsOpen && IsOverPanel(pos);
                if (fromEdge || fromPanel)
                {
                    maybeDrag = true;
                    dragging = fromEdge;
                    dragStartX = pos.x;
                    dragStartY = pos.y;
                    dragStartProgress = progress;
                }
            }

            if (maybeDrag && held && !dragging)
            {
                float dx = pos.x - dragStartX;
                float dy = pos.y - dragStartY;
                if (dx * dx + dy * dy > 18f * 18f)
                {
                    if (Mathf.Abs(dx) > Mathf.Abs(dy) * 1.15f) dragging = true;
                    else maybeDrag = false;
                }
            }

            if (dragging && held)
            {
                float delta = (dragStartX - pos.x) / Mathf.Max(80f, width);
                progress = Mathf.Clamp01(dragStartProgress + delta);
                target = progress;
                Apply(1f);
            }

            if ((dragging || maybeDrag) && up)
            {
                if (dragging)
                {
                    target = progress >= snap ? 1f : 0f;
                }

                dragging = false;
                maybeDrag = false;
            }
        }

        private void Apply(float t)
        {
            progress = Mathf.Lerp(progress, target, Mathf.Clamp01(t));
            if (Mathf.Abs(progress - target) < 0.002f) progress = target;

            if (panel != null)
            {
                var pos = panel.anchoredPosition;
                pos.x = Mathf.Lerp(0f, -width, progress);
                panel.anchoredPosition = pos;
            }

            if (scrim != null)
            {
                bool show = progress > 0.02f;
                if (scrim.activeSelf != show) scrim.SetActive(show);
                var image = scrim.GetComponent<Image>();
                if (image != null)
                {
                    var color = image.color;
                    color.a = 0.28f * progress;
                    image.color = color;
                }
            }

            if (edge != null)
            {
                bool show = progress < 0.98f;
                if (edge.activeSelf != show) edge.SetActive(show);
            }
        }

        private bool IsOverPanel(Vector2 screen)
        {
            if (panel == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(panel, screen, null);
        }

        private static bool TryPointer(out Vector2 pos, out bool down, out bool held, out bool up)
        {
            var touch = Touchscreen.current;
            if (touch != null)
            {
                var primary = touch.primaryTouch;
                if (primary.press.isPressed || primary.press.wasReleasedThisFrame)
                {
                    pos = primary.position.ReadValue();
                    down = primary.press.wasPressedThisFrame;
                    held = primary.press.isPressed;
                    up = primary.press.wasReleasedThisFrame;
                    return true;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                pos = mouse.position.ReadValue();
                down = mouse.leftButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed;
                up = mouse.leftButton.wasReleasedThisFrame;
                return down || held || up;
            }

            pos = default;
            down = held = up = false;
            return false;
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
