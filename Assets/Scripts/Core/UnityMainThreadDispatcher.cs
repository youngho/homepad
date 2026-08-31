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

            var existing = FindFirstObjectByType<UnityMainThreadDispatcher>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            var go = new GameObject("UnityMainThreadDispatcher");
            instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
            return instance;
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
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
