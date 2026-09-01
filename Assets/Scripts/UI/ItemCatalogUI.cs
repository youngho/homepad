using System.Collections.Generic;
using Homepad.Home;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class ItemCatalogUI : MonoBehaviour
    {
        [SerializeField] private Button[] buttons;
        [SerializeField] private CatalogDrawerUI drawer;

        private int lastPickFrame = -1;
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        private void OnEnable()
        {
            Subscribe(true);
            WireClicks();
            Refresh();
        }

        private void Start()
        {
            Subscribe(true);
            WireClicks();
            Refresh();
        }

        private void OnDisable()
        {
            Subscribe(false);
        }

        private void Update()
        {
            if (drawer != null && !drawer.IsOpen) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current == null || buttons == null) return;

            var data = new PointerEventData(EventSystem.current)
            {
                position = mouse.position.ReadValue()
            };
            Hits.Clear();
            EventSystem.current.RaycastAll(data, Hits);
            for (int i = 0; i < Hits.Count; i++)
            {
                var hit = Hits[i].gameObject;
                for (int b = 0; b < buttons.Length && b < HomeItemDef.Catalog.Length; b++)
                {
                    if (buttons[b] == null) continue;
                    if (hit == buttons[b].gameObject || hit.transform.IsChildOf(buttons[b].transform))
                    {
                        Pick(b);
                        return;
                    }
                }
            }
        }

        private void Subscribe(bool on)
        {
            var home = HomeController.EnsureExists();
            if (home == null) return;
            home.LayoutChanged -= Refresh;
            if (on) home.LayoutChanged += Refresh;
        }

        private void WireClicks()
        {
            if (buttons == null) return;
            int count = Mathf.Min(buttons.Length, HomeItemDef.Catalog.Length);
            for (int i = 0; i < count; i++)
            {
                if (buttons[i] == null) continue;
                int index = i;
                buttons[i].onClick.RemoveAllListeners();
                buttons[i].onClick.AddListener(() => Pick(index));
            }
        }

        public void Pick(int index)
        {
            if (lastPickFrame == Time.frameCount) return;
            if (index < 0 || index >= HomeItemDef.Catalog.Length) return;
            var home = HomeController.EnsureExists();
            if (home == null) return;
            lastPickFrame = Time.frameCount;
            home.PlaceFromCatalog(HomeItemDef.Catalog[index]);
        }

        public void Refresh()
        {
            var home = HomeController.Instance ?? HomeController.EnsureExists();
            if (buttons == null) return;
            int count = Mathf.Min(buttons.Length, HomeItemDef.Catalog.Length);
            for (int i = 0; i < count; i++)
            {
                if (buttons[i] == null) continue;
                bool blocked = home != null && home.Layout != null && home.Layout.IsCatalogBlocked(HomeItemDef.Catalog[i]);
                buttons[i].interactable = !blocked;
            }
        }
    }
}
