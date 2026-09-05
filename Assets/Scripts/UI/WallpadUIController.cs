using Homepad.Core;
using Homepad.Home;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public enum WallpadTab
    {
        Dashboard,
        Lighting,
        Heating,
        GasVent,
        Elevator,
        Settings
    }

    [DefaultExecutionOrder(50)]
    public class WallpadUIController : MonoBehaviour
    {
        [SerializeField] private Text houseTitle;
        [SerializeField] private Image wifiDot;
        [SerializeField] private Text wifiText;
        [SerializeField] private Text hintText;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private DeviceOverlayUI overlay;
        [SerializeField] private ItemCatalogUI catalog;
        [SerializeField] private CatalogDrawerUI catalogDrawer;

        private void Awake()
        {
            HomeController.EnsureExists();
        }

        private void Start()
        {
            Bind(settingsCloseButton, () =>
            {
                if (settingsPanel != null) settingsPanel.SetActive(false);
            });

            if (houseTitle != null && WallpadManager.Instance != null)
            {
                string house = WallpadManager.Instance.Config != null
                    ? WallpadManager.Instance.Config.householdName
                    : "세종시 첫마을 503동 2801호";
                houseTitle.text = house;
            }

            var home = HomeController.Instance;
            if (home != null)
            {
                home.ItemClicked += OnItemClicked;
                home.OverlayDismissed += OnOverlayDismissed;
                home.LayoutChanged += RefreshHint;
            }

            Subscribe();
            RefreshHint();
            catalog?.Refresh();
        }

        private void OnDestroy()
        {
            var home = HomeController.Instance;
            if (home != null)
            {
                home.ItemClicked -= OnItemClicked;
                home.OverlayDismissed -= OnOverlayDismissed;
                home.LayoutChanged -= RefreshHint;
            }

            if (WallpadManager.Instance == null) return;
            if (WallpadManager.Instance.Connector != null)
            {
                WallpadManager.Instance.Connector.OnConnectionStatusChanged -= UpdateWifi;
            }
        }

        private void OnItemClicked(PlacedItem item)
        {
            overlay?.Show(item);
            catalogDrawer?.Close();
        }

        private void OnOverlayDismissed()
        {
            overlay?.Hide();
            if (settingsPanel != null) settingsPanel.SetActive(false);
            catalogDrawer?.Close();
        }

        private void RefreshHint()
        {
            bool empty = HomeController.Instance == null
                         || HomeController.Instance.Layout.Rooms.Count == 0;
            if (hintText != null)
            {
                hintText.gameObject.SetActive(empty);
            }
        }

        private void Subscribe()
        {
            if (WallpadManager.Instance == null) return;
            if (WallpadManager.Instance.Connector != null)
            {
                WallpadManager.Instance.Connector.OnConnectionStatusChanged += UpdateWifi;
                UpdateWifi(WallpadManager.Instance.Connector.IsConnected);
            }
        }

        private void UpdateWifi(bool isConnected)
        {
            bool sim = WallpadManager.Instance != null && WallpadManager.Instance.Connector != null && WallpadManager.Instance.Connector.UseSimulationMode;
            Color color = isConnected ? new Color(0.337f, 0.588f, 0.408f) : new Color(0.753f, 0.337f, 0.337f);
            if (wifiDot != null) wifiDot.color = color;
            if (wifiText != null)
            {
                wifiText.color = color;
                wifiText.text = !isConnected ? "OFFLINE" : (sim ? "시뮬레이션" : "RS485 ONLINE");
            }
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
