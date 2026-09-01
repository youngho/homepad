using Homepad.Home;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class DeviceOverlayUI : MonoBehaviour
    {
        [SerializeField] private GameObject dimmer;
        [SerializeField] private GameObject card;
        [SerializeField] private GameObject lightingRoot;
        [SerializeField] private GameObject heatingRoot;
        [SerializeField] private GameObject gasRoot;
        [SerializeField] private GameObject ventRoot;
        [SerializeField] private GameObject elevatorRoot;
        [SerializeField] private GameObject curtainRoot;
        [SerializeField] private LightingPanelUI lighting;
        [SerializeField] private HeatingPanelUI heating;
        [SerializeField] private CurtainPanelUI curtain;

        public bool IsOpen => card != null && card.activeSelf;

        private void Awake()
        {
            if (dimmer != null)
            {
                var button = dimmer.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(Hide);
                }
            }

            Hide();
        }

        public void Show(PlacedItem item)
        {
            if (item == null || card == null) return;
            HidePanels();
            if (dimmer != null) dimmer.SetActive(true);
            card.SetActive(true);

            switch (item.Kind)
            {
                case HomeItemKind.Light:
                    ShowRoot(lightingRoot);
                    lighting?.Focus(item.DeviceId);
                    break;
                case HomeItemKind.Heating:
                    ShowRoot(heatingRoot);
                    heating?.Focus(item.DeviceId);
                    break;
                case HomeItemKind.Gas:
                    ShowRoot(gasRoot);
                    break;
                case HomeItemKind.Vent:
                    ShowRoot(ventRoot);
                    break;
                case HomeItemKind.Elevator:
                    ShowRoot(elevatorRoot);
                    break;
                case HomeItemKind.ElectricCurtain:
                    ShowRoot(curtainRoot);
                    curtain?.Focus(item.InstanceId);
                    break;
            }
        }

        public void Hide()
        {
            if (dimmer != null) dimmer.SetActive(false);
            if (card != null) card.SetActive(false);
            HidePanels();
        }

        private void HidePanels()
        {
            ShowRoot(null);
        }

        private void ShowRoot(GameObject root)
        {
            Set(lightingRoot, root == lightingRoot);
            Set(heatingRoot, root == heatingRoot);
            Set(gasRoot, root == gasRoot);
            Set(ventRoot, root == ventRoot);
            Set(elevatorRoot, root == elevatorRoot);
            Set(curtainRoot, root == curtainRoot);
        }

        private static void Set(GameObject go, bool on)
        {
            if (go != null) go.SetActive(on);
        }
    }
}
