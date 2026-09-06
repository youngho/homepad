using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Homepad.Core
{
    public static class IosScreenEdgePan
    {
        public const int StateBegan = 1;
        public const int StateChanged = 2;
        public const int StateEnded = 3;
        public const int StateCancelled = 4;
        public const int StateFailed = 5;

        public delegate void PanHandler(int state, float translationX, float translationY, float velocityX);

        public static bool IsAvailable { get; private set; }

        private static PanHandler handlers;
        private static NativeCallback nativeKeepAlive;

        private delegate void NativeCallback(int state, float translationX, float translationY, float velocityX);

        public static void AddListener(PanHandler handler)
        {
            if (handler == null) return;
            handlers += handler;
            EnsureInstalled();
        }

        public static void RemoveListener(PanHandler handler)
        {
            if (handler == null) return;
            handlers -= handler;
            if (handlers == null)
            {
                Uninstall();
            }
        }

        private static void EnsureInstalled()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (IsAvailable) return;
            UnityEngine.iOS.Device.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.RightEdge;
            nativeKeepAlive = OnNativePan;
            Homepad_InstallRightEdgePan(nativeKeepAlive);
            IsAvailable = true;
#endif
        }

        private static void Uninstall()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Homepad_UninstallRightEdgePan();
            nativeKeepAlive = null;
            IsAvailable = false;
#endif
        }

        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnNativePan(int state, float translationX, float translationY, float velocityX)
        {
            handlers?.Invoke(state, translationX, translationY, velocityX);
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Homepad_InstallRightEdgePan(NativeCallback callback);

        [DllImport("__Internal")]
        private static extern void Homepad_UninstallRightEdgePan();
#endif
    }
}
