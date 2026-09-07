using System;
using System.Collections.Generic;
using UnityEngine;

namespace Homepad.Core
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> executionQueue = new Queue<Action>();
        private static UnityMainThreadDispatcher instance;

        public static UnityMainThreadDispatcher EnsureExists()
        {
            if (instance != null) return instance;

            var existing = FindStandalone();
            if (existing != null)
            {
                instance = existing;
                DontDestroyOnLoad(existing.gameObject);
                return instance;
            }

            var go = new GameObject("UnityMainThreadDispatcher");
            instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
            return instance;
        }

        private static UnityMainThreadDispatcher FindStandalone()
        {
            var all = FindObjectsByType<UnityMainThreadDispatcher>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].GetComponent<WallpadManager>() == null)
                {
                    return all[i];
                }
            }

            return null;
        }

        private void Awake()
        {
            // WallpadSystem 위에 붙어 있으면 월패드 전체를 씬에 붙잡아 조명 패널이 비게 된다.
            if (GetComponent<WallpadManager>() != null)
            {
                if (instance == this) instance = null;
                EnsureExists();
                return;
            }

            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                lock (executionQueue)
                {
                    executionQueue.Clear();
                }
            }
        }

        private void Update()
        {
            lock (executionQueue)
            {
                while (executionQueue.Count > 0)
                {
                    var action = executionQueue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            EnsureExists();

            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }
        }
    }
}
