using UnityEditor;
using UnityEngine;

namespace Homepad.Editor
{
    [InitializeOnLoad]
    static class StaleInspectorGuard
    {
        static int sweepFrames;

        static StaleInspectorGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                ReleaseTransientSelection();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                sweepFrames = 8;
                EditorApplication.delayCall += SweepDestroyedSelection;
            }
        }

        static void OnEditorUpdate()
        {
            if (sweepFrames <= 0) return;
            sweepFrames--;
            SweepDestroyedSelection();
        }

        static void ReleaseTransientSelection()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0) return;

            for (int i = 0; i < selected.Length; i++)
            {
                if (IsPlayModeTransient(selected[i]))
                {
                    Selection.activeObject = null;
                    return;
                }
            }
        }

        static void SweepDestroyedSelection()
        {
            try
            {
                var selected = Selection.objects;
                if (selected == null || selected.Length == 0)
                {
                    if (sweepFrames == 7)
                    {
                        RebuildInspector();
                    }

                    return;
                }

                int alive = 0;
                bool stale = false;
                for (int i = 0; i < selected.Length; i++)
                {
                    if (IsDestroyedOrNull(selected[i])) stale = true;
                    else alive++;
                }

                if (!stale) return;

                if (alive == 0)
                {
                    Selection.objects = System.Array.Empty<Object>();
                }
                else
                {
                    var kept = new Object[alive];
                    int n = 0;
                    for (int i = 0; i < selected.Length; i++)
                    {
                        if (!IsDestroyedOrNull(selected[i])) kept[n++] = selected[i];
                    }

                    Selection.objects = kept;
                }

                RebuildInspector();
            }
            catch (MissingReferenceException)
            {
                Selection.activeObject = null;
                RebuildInspector();
            }
        }

        static void RebuildInspector()
        {
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        static bool IsPlayModeTransient(Object obj)
        {
            if (IsDestroyedOrNull(obj)) return true;

            var go = obj as GameObject;
            if (obj is Component component && component != null)
            {
                go = component.gameObject;
            }

            if (go == null) return false;
            if (EditorUtility.IsPersistent(go)) return false;

            var t = go.transform;
            while (t != null)
            {
                if ((t.gameObject.hideFlags & HideFlags.DontSave) != 0) return true;
                t = t.parent;
            }

            var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
            return gid.targetObjectId == 0;
        }

        static bool IsDestroyedOrNull(Object obj)
        {
            if (ReferenceEquals(obj, null)) return true;
            if (obj == null) return true;
            if (!obj) return true;
            try
            {
                _ = obj.name;
                return false;
            }
            catch (MissingReferenceException)
            {
                return true;
            }
        }
    }
}
