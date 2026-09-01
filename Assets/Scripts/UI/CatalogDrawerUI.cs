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
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private float width = 420f;
        [SerializeField] private float edgeGrabPx = 48f;
        [SerializeField] private float snap = 0.35f;

        private CanvasGroup panelGroup;
        private RectTransform closeRect;
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
            panelGroup = panel != null ? panel.GetComponent<CanvasGroup>() : null;
            if (panel != null && panelGroup == null)
            {
                panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            }

            closeRect = closeButton != null ? closeButton.transform as RectTransform : null;
            Bind(closeButton, Close);
            if (scrim != null)
            {
                Bind(scrim.GetComponent<Button>(), Close);
            }

            if (edge != null)
            {
                var edgeButton = edge.GetComponent<Button>();
                if (edgeButton != null) edgeButton.enabled = false;
            }

            target = 0f;
            progress = 0f;
            Apply(1f);
        }

        public void Open()
        {
            GetComponentInParent<DeviceOverlayUI>()?.Hide();
            if (settingsPanel != null) settingsPanel.SetActive(false);
            dragging = false;
            maybeDrag = false;
            target = 1f;
        }

        public void Close()
        {
            dragging = false;
            maybeDrag = false;
            target = 0f;
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
                if (dragging || maybeDrag)
                {
                    target = progress >= snap ? 1f : 0f;
                    dragging = false;
                    maybeDrag = false;
                }

                return;
            }

            if (down)
            {
                if (IsOpen)
                {
                    if (IsOverClose(pos) || !IsOverPanel(pos))
                    {
                        Close();
                        return;
                    }

                    maybeDrag = true;
                    dragging = false;
                    dragStartX = pos.x;
                    dragStartY = pos.y;
                    dragStartProgress = progress;
                }
                else if (pos.x >= Screen.width - edgeGrabPx && pos.y < Screen.height - 110f)
                {
                    dragging = true;
                    maybeDrag = false;
                    dragStartX = pos.x;
                    dragStartY = pos.y;
                    dragStartProgress = progress;
                }
            }

            if (maybeDrag && held && !dragging)
            {
                float dx = pos.x - dragStartX;
                float dy = pos.y - dragStartY;
                if (dx * dx + dy * dy > 28f * 28f)
                {
                    if (dx > Mathf.Abs(dy) * 1.15f) dragging = true;
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

            if ((dragging || maybeDrag) && (up || !held))
            {
                if (dragging) target = progress >= snap ? 1f : 0f;
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

            bool visible = progress > 0.02f;
            if (panelGroup != null)
            {
                panelGroup.alpha = 1f;
                panelGroup.interactable = visible;
                panelGroup.blocksRaycasts = visible;
            }

            if (scrim != null)
            {
                if (scrim.activeSelf != visible) scrim.SetActive(visible);
                var image = scrim.GetComponent<Image>();
                if (image != null)
                {
                    var color = image.color;
                    color.a = 0.28f * progress;
                    image.color = color;
                    image.raycastTarget = visible;
                }
            }

            if (edge != null)
            {
                bool showEdge = progress < 0.98f;
                if (edge.activeSelf != showEdge) edge.SetActive(showEdge);
            }
        }

        private bool IsOverPanel(Vector2 screen)
        {
            if (panel == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(panel, screen, null);
        }

        private bool IsOverClose(Vector2 screen)
        {
            if (closeRect == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(closeRect, screen, null);
        }

        private static bool TryPointer(out Vector2 pos, out bool down, out bool held, out bool up)
        {
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame || mouse.leftButton.wasReleasedThisFrame))
            {
                pos = mouse.position.ReadValue();
                down = mouse.leftButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed;
                up = mouse.leftButton.wasReleasedThisFrame;
                return true;
            }

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
