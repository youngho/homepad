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
            EnsureUiActionMap();

            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (eventSystems.Length == 0)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
                return;
            }

            for (int i = 0; i < eventSystems.Length; i++)
            {
                var eventSystem = eventSystems[i];
                // 구버전 StandaloneInputModule이 남아있다면 비활성화
                var standalone = eventSystem.GetComponent<StandaloneInputModule>();
                if (standalone != null)
                {
                    standalone.enabled = false;
                }

                if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                eventSystem.enabled = true;
            }
        }

        public static void GiveMouseToUi()
        {
            EnsureUiActionMap();
        }

        private static void EnsureUiActionMap()
        {
            var asset = InputSystem.actions;
            if (asset == null) return;

            var uiMap = asset.FindActionMap("UI");
            if (uiMap != null && !uiMap.enabled)
            {
                uiMap.Enable();
            }
        }
    }
}
