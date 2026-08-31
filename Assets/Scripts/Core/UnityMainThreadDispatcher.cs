using System;
using System.Collections.Generic;
using UnityEngine;

namespace Homepad.Core
{
    /// <summary>
    /// 백그라운드 소켓 스레드에서 Unity 메인 스레드로 작업을 전달하는 디스패처
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> executionQueue = new Queue<Action>();
        private static UnityMainThreadDispatcher instance;

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

        private void Update()
        {
            lock (executionQueue)
            {
                while (executionQueue.Count > 0)
                {
                    executionQueue.Dequeue()?.Invoke();
                }
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null) return;

            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }
        }
    }
}
