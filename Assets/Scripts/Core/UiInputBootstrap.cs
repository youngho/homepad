using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Homepad.Core
{
    public static class UiInputBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ConfigureUiInput();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureUiInput();
        }

        public static void ConfigureUiInput()
        {
            GiveMouseToUi();

            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (eventSystems.Length == 0)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
                GiveMouseToUi();
                return;
            }

            for (int i = 0; i < eventSystems.Length; i++)
            {
                var eventSystem = eventSystems[i];
                if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                eventSystem.enabled = true;
            }
        }

        public static void GiveMouseToUi()
        {
            var asset = InputSystem.actions;
            if (asset == null) return;

            for (int i = 0; i < asset.actionMaps.Count; i++)
            {
                var map = asset.actionMaps[i];
                if (map.name == "UI") map.Enable();
                else map.Disable();
            }
        }
    }
}
