using Homepad.Home;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class ItemCatalogUI : MonoBehaviour
    {
        [SerializeField] private Button[] buttons;

        private void OnEnable()
        {
            if (HomeController.Instance != null)
            {
                HomeController.Instance.LayoutChanged += Refresh;
            }

            WireClicks();
            Refresh();
        }

        private void OnDisable()
        {
            if (HomeController.Instance != null)
            {
                HomeController.Instance.LayoutChanged -= Refresh;
            }
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
                buttons[i].onClick.AddListener(() =>
                {
                    HomeController.Instance?.BeginPlacement(HomeItemDef.Catalog[index]);
                });
            }
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
