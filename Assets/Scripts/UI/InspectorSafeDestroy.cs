using UnityEngine;

namespace Homepad.UI
{
    internal static class InspectorSafeDestroy
    {
        public static void GameObject(GameObject go)
        {
            if (go == null) return;
#if UNITY_EDITOR
            ReleaseFromInspector(go);
#endif
            Object.Destroy(go);
        }

#if UNITY_EDITOR
        public static void ReleaseFromInspector(GameObject go)
        {
            if (go == null) return;

            var selected = UnityEditor.Selection.objects;
            if (selected == null || selected.Length == 0) return;

            for (int i = 0; i < selected.Length; i++)
            {
                if (IsUnder(selected[i], go))
                {
                    UnityEditor.Selection.activeObject = null;
                    return;
                }
            }
        }

        private static bool IsUnder(Object obj, GameObject root)
        {
            if (obj == null || root == null) return obj == null;
            var selectedGo = obj as GameObject;
            if (obj is Component component && component != null)
            {
                selectedGo = component.gameObject;
            }

            if (selectedGo == null) return false;
            return selectedGo.transform.IsChildOf(root.transform);
        }
#endif
    }
}
