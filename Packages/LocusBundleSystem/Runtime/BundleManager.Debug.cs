using UnityEngine;

namespace BundleSystem
{
    public static partial class BundleManager
    {
        private static bool s_ShowDebugGUI = false;
        public static bool ShowDebugGUI
        {
            get => s_ShowDebugGUI;
            set { s_ShowDebugGUI = value; if (s_DebugGUI != null) s_DebugGUI.enabled = value; }
        }

        private class DebugGuiHelper : MonoBehaviour
        {
            private void OnGUI()
            {
                GUILayout.Label("Bundle RefCounts");
                GUILayout.Label("-----------------");
                foreach (var kv in s_BundleRefCounts)
                {
                    if (kv.Value == 0) continue;
                    GUILayout.Label($"Name : {kv.Key} - {kv.Value}");
                }

                if (Application.isEditor)
                {
                    GUILayout.Label("-----------------");
                    GUILayout.Label("Bundle UseCounts");
                    GUILayout.Label("-----------------");
                    foreach (var kv in s_BundleDirectUseCount)
                    {
                        if (kv.Value == 0) continue;
                        GUILayout.Label($"Name : {kv.Key} - {kv.Value}");
                    }
                }

                GUILayout.Label("-----------------");
                GUILayout.Label($"Tracking Object Count {s_TrackingObjects.Count}");
                GUILayout.Label($"Tracking Owner Count {s_TrackingOwners.Count}");
            }
        }
    }
}
