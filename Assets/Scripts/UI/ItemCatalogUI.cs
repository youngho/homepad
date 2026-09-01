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
