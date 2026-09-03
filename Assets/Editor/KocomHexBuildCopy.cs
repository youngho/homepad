using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Homepad.Editor
{
    public sealed class KocomHexBuildCopy : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [MenuItem("Homepad/Copy Kocom Hex Into Build")]
        public static void CopyNow()
        {
            Copy();
            AssetDatabase.Refresh();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            Copy();
            AssetDatabase.Refresh();
        }

        private static void Copy()
        {
            string src = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/kocom-hex.md"));
            if (!File.Exists(src))
            {
                Debug.LogWarning("[KocomHexBuildCopy] Docs/kocom-hex.md 가 없습니다.");
                return;
            }

            string resourcesDir = Path.Combine(Application.dataPath, "Resources");
            string streamingDir = Path.Combine(Application.dataPath, "StreamingAssets");
            Directory.CreateDirectory(resourcesDir);
            Directory.CreateDirectory(streamingDir);
            File.Copy(src, Path.Combine(resourcesDir, "kocom-hex.txt"), true);
            File.Copy(src, Path.Combine(streamingDir, "kocom-hex.md"), true);
            Debug.Log("[KocomHexBuildCopy] kocom-hex.md 를 Resources/StreamingAssets 에 복사했습니다.");
        }
    }
}
