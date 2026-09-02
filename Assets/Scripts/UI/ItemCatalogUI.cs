using System.Collections.Generic;
using Homepad.Home;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class ItemCatalogUI : MonoBehaviour
    {
        [SerializeField] private Button[] buttons;
        [SerializeField] private CatalogDrawerUI drawer;

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
            var home = HomeController.Instance;
            if (home != null) home.LayoutChanged -= Refresh;
        }

        private void Subscribe(bool on)
        {
            var home = HomeController.Instance;
            if (on && home == null) home = HomeController.EnsureExists();
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
            if (index < 0 || index >= HomeItemDef.Catalog.Length) return;
            var home = HomeController.EnsureExists();
            if (home == null) return;
            home.PlaceFromCatalog(HomeItemDef.Catalog[index]);
        }

        /// <summary>
        /// Rule-based addition of a device kind to a specified room.
        /// </summary>
        public bool AddDevice(HomeItemKind kind, RoomHint room)
        {
            var home = HomeController.EnsureExists();
            if (home == null) return false;

            var def = HomeItemDef.Create(kind, room);
            return home.PlaceFromCatalog(def);
        }

        /// <summary>
        /// Query allowed rooms for a given device kind and check if each is currently available or blocked.
        /// </summary>
        public List<(RoomHint room, string roomName, bool isAvailable)> GetRoomAvailability(HomeItemKind kind)
        {
            var result = new List<(RoomHint, string, bool)>();
            if (!HomeItemDef.CategoryRules.TryGetValue(kind, out var rule))
                return result;

            var home = HomeController.Instance;
            var layout = home != null ? home.Layout : null;

            for (int i = 0; i < rule.DefaultAllowedRooms.Length; i++)
            {
                var room = rule.DefaultAllowedRooms[i];
                var def = HomeItemDef.Create(kind, room);
                bool blocked = layout != null && layout.IsCatalogBlocked(def);
                result.Add((room, HomeItemDef.RoomName(room), !blocked));
            }

            return result;
        }

        public void Refresh()
        {
            var home = HomeController.Instance;
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
