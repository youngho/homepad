using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Homepad.Core
{
    public static class PointerInput
    {
        private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        public static bool TryPrimary(out Vector2 position, out bool down, out bool held, out bool up)
        {
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame || mouse.leftButton.wasReleasedThisFrame))
            {
                position = mouse.position.ReadValue();
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
                    position = primary.position.ReadValue();
                    down = primary.press.wasPressedThisFrame;
                    held = primary.press.isPressed;
                    up = primary.press.wasReleasedThisFrame;
                    return true;
                }
            }

            position = default;
            down = held = up = false;
            return false;
        }

        public static bool OverUi(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };
            UiHits.Clear();
            EventSystem.current.RaycastAll(data, UiHits);
            return UiHits.Count > 0;
        }
    }
}
