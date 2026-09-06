using System.Collections.Generic;
using Homepad.Core;
using Homepad.Home;
using UnityEngine;
using UnityEngine.UI;

namespace Homepad.UI
{
    public class LightingPanelUI : MonoBehaviour
    {
        private static readonly Color OnColor = new Color(0.32f, 0.18f, 0.04f);
        private static readonly Color OffColor = new Color(0.55f, 0.58f, 0.64f);
        private static readonly Color OnLabel = new Color(0.16f, 0.10f, 0.04f);
        private static readonly Color OffLabel = new Color(0.78f, 0.82f, 0.88f);
        private static readonly Color OnBg = new Color(0.96f, 0.74f, 0.22f);
        private static readonly Color OffBg = new Color(0.10f, 0.12f, 0.16f);

        [SerializeField] private Button allOffButton;
        [SerializeField] private GameObject slotTemplate;
        [SerializeField] private Transform slotList;
        [SerializeField] private Text titleText;
        [SerializeField] private LightSlot[] lights;

        [System.Serializable]
        public class LightSlot
        {
            public int lightId;
            public Button button;
            public Text nameText;
            public Text statusText;
        }

        private readonly List<LightSlot> activeSlots = new List<LightSlot>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private ushort focusedRoomCode;
        private bool templateResolved;

        public void Focus(int lightId)
        {
            var item = FindPlacedLight(lightId);
            if (item != null)
            {
                FocusRoom(item);
                return;
            }

            var manager = WallpadManager.Instance;
            var state = manager != null ? FindLight(manager.Lights, lightId) : null;
            if (state != null)
            {
                FocusRoomCode(state.roomCode, DescribeRoom(state.roomCode));
            }
        }

        public void FocusRoom(PlacedItem item)
        {
            if (item == null) return;
            FocusRoomCode(HomeItemDef.RoomCode(item.RoomHint), RoomName(item.RoomHint));
        }

        private void Awake()
        {
            ResolveTemplate();
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        private void Start()
        {
            BindClicks();
            if (WallpadManager.Instance != null)
            {
                WallpadManager.Instance.OnStateChanged += RefreshAll;
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (WallpadManager.Instance == null) return;
            WallpadManager.Instance.OnStateChanged -= RefreshAll;
        }

        public void RefreshAll()
        {
            if (WallpadManager.Instance == null) return;
            var managerLights = WallpadManager.Instance.Lights;
            for (int i = 0; i < activeSlots.Count; i++)
            {
                ApplySlotVisual(activeSlots[i], FindLight(managerLights, activeSlots[i].lightId));
            }
        }

        private void FocusRoomCode(ushort roomCode, string roomName)
        {
            ResolveTemplate();
            focusedRoomCode = roomCode;
            if (titleText != null) titleText.text = string.IsNullOrEmpty(roomName) ? "조명" : $"{roomName} 조명";

            var roomLights = CollectRoomLights(roomCode);
            RebuildSlots(roomLights);
            BindClicks();
            RefreshAll();
        }

        private void ResolveTemplate()
        {
            if (templateResolved) return;
            templateResolved = true;

            if (slotTemplate == null && lights != null && lights.Length > 0 && lights[0].button != null)
            {
                slotTemplate = lights[0].button.gameObject;
            }

            if (slotTemplate == null)
            {
                var found = transform.Find("LightSlot");
                if (found != null) slotTemplate = found.gameObject;
            }

            if (slotList == null) slotList = transform;
            if (titleText == null)
            {
                var title = transform.Find("Title");
                if (title != null) titleText = title.GetComponent<Text>();
            }

            if (slotTemplate != null) slotTemplate.SetActive(false);
        }

        private List<LightState> CollectRoomLights(ushort roomCode)
        {
            var result = new List<LightState>();
            var manager = WallpadManager.Instance;
            if (manager == null) return result;

            var layout = HomeController.Instance != null ? HomeController.Instance.Layout : null;
            if (layout != null)
            {
                for (int i = 0; i < layout.Items.Count; i++)
                {
                    var item = layout.Items[i];
                    if (item.Kind != HomeItemKind.Light) continue;
                    if (HomeItemDef.RoomCode(item.RoomHint) != roomCode) continue;
                    var state = FindLight(manager.Lights, item.DeviceId);
                    if (state != null) result.Add(state);
                }
            }

            if (result.Count == 0)
            {
                for (int i = 0; i < manager.Lights.Count; i++)
                {
                    if (manager.Lights[i].roomCode == roomCode) result.Add(manager.Lights[i]);
                }
            }

            result.Sort((a, b) => a.slot.CompareTo(b.slot));
            return result;
        }

        private void RebuildSlots(List<LightState> roomLights)
        {
            ClearSpawned();
            if (slotTemplate == null || roomLights == null) return;

            for (int i = 0; i < roomLights.Count; i++)
            {
                var go = Instantiate(slotTemplate, slotList);
                go.name = $"LightSlot_{i + 1}";
                go.SetActive(true);
                LayoutSlot(go.transform as RectTransform, i, roomLights.Count);

                var slot = new LightSlot
                {
                    lightId = roomLights[i].id,
                    button = go.GetComponent<Button>(),
                    nameText = go.transform.Find("Name")?.GetComponent<Text>(),
                    statusText = go.transform.Find("Status")?.GetComponent<Text>()
                };
                StyleSlotLabel(slot.nameText, $"조명{i + 1}", 30, true);
                StyleSlotLabel(slot.statusText, "OFF", 24, false);
                LayoutSlotLabel(slot.nameText != null ? slot.nameText.rectTransform : null, 0.42f, 0.94f);
                LayoutSlotLabel(slot.statusText != null ? slot.statusText.rectTransform : null, 0.08f, 0.48f);

                activeSlots.Add(slot);
                spawned.Add(go);
            }
        }

        private void StyleSlotLabel(Text text, string value, int fontSize, bool titleWeight)
        {
            if (text == null) return;
            if (titleWeight && titleText != null && titleText.font != null) text.font = titleText.font;
            text.fontStyle = FontStyle.Normal;
            text.fontSize = Mathf.Max(22, fontSize);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.text = value;
        }

        private static void LayoutSlotLabel(RectTransform rt, float yMin, float yMax)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.06f, yMin);
            rt.anchorMax = new Vector2(0.94f, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void LayoutSlot(RectTransform rt, int index, int count)
        {
            if (rt == null) return;
            int cols = Mathf.Max(1, count);
            float pad = 0.05f;
            float gap = 0.025f;
            float usable = 1f - pad * 2f - gap * (cols - 1);
            float width = usable / cols;
            float xMin = pad + index * (width + gap);
            rt.anchorMin = new Vector2(xMin, 0.14f);
            rt.anchorMax = new Vector2(xMin + width, 0.78f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                InspectorSafeDestroy.GameObject(spawned[i]);
            }

            spawned.Clear();
            activeSlots.Clear();
        }

        private void BindClicks()
        {
            if (allOffButton != null)
            {
                allOffButton.onClick.RemoveAllListeners();
                allOffButton.onClick.AddListener(() =>
                {
                    if (WallpadManager.Instance != null)
                    {
                        WallpadManager.Instance.TurnOffRoomLights(focusedRoomCode);
                    }
                });
            }

            for (int i = 0; i < activeSlots.Count; i++)
            {
                var slot = activeSlots[i];
                if (slot.button == null) continue;
                int lightId = slot.lightId;
                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() => WallpadManager.Instance.ToggleLight(lightId));
            }
        }

        private void ApplySlotVisual(LightSlot slot, LightState state)
        {
            if (slot == null || state == null) return;
            bool isOn = state.isOn;
            if (slot.nameText != null)
            {
                slot.nameText.color = isOn ? OnLabel : OffLabel;
            }

            if (slot.statusText != null)
            {
                slot.statusText.text = isOn ? "ON" : "OFF";
                slot.statusText.color = isOn ? OnColor : OffColor;
            }

            if (slot.button != null)
            {
                var image = slot.button.GetComponent<Image>();
                if (image != null) image.color = isOn ? OnBg : OffBg;
            }
        }

        private static PlacedItem FindPlacedLight(int lightId)
        {
            var layout = HomeController.Instance != null ? HomeController.Instance.Layout : null;
            if (layout == null) return null;
            for (int i = 0; i < layout.Items.Count; i++)
            {
                var item = layout.Items[i];
                if (item.Kind == HomeItemKind.Light && item.DeviceId == lightId) return item;
            }

            return null;
        }

        private static string RoomName(RoomHint hint)
        {
            var room = HomeController.Instance != null ? HomeController.Instance.Layout.FindRoom(hint) : null;
            if (room != null && !string.IsNullOrEmpty(room.Name)) return room.Name;
            return HomeItemDef.RoomName(hint);
        }

        private static string DescribeRoom(ushort roomCode)
        {
            return roomCode switch
            {
                0x0001 => "거실",
                0x0101 => "방1",
                0x0201 => "방2",
                0x0301 => "방3",
                _ => "조명"
            };
        }

        private static LightState FindLight(IReadOnlyList<LightState> managerLights, int lightId)
        {
            if (managerLights == null) return null;
            for (int i = 0; i < managerLights.Count; i++)
            {
                if (managerLights[i].id == lightId) return managerLights[i];
            }

            return null;
        }
    }
}
