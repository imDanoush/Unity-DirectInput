using UnityEditor;
using UnityEngine;

namespace DirectInputManager.Editor
{
    /// <summary>
    /// Edit-mode half of the startup self-check: reports a DirectInput plugin that cannot load before anyone
    /// presses Play (Play Mode reports through DirectInputLogger instead).
    /// </summary>
    [InitializeOnLoad]
    static class PluginSelfCheck
    {
        const string PluginGuid = "fa5394ba60c8227438c47b1d0d16805c"; // Runtime/Plugins/DLL/DirectInputForceFeedback.dll.meta

        static PluginSelfCheck() { EditorApplication.delayCall += Run; } // after load, when the asset database and windows exist

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                if (SessionState.GetBool("DirectInput.WindowsOnlyWarned", false)) { return; }
                SessionState.SetBool("DirectInput.WindowsOnlyWarned", true);
                Debug.LogWarning("[DirectInput] DirectInputForceFeedback.dll is a Windows x64 plugin: wheel input and force feedback work only in the Windows Editor and in Windows x86_64 players.");
                return;
            }
            if (DIManager.Initialize()) { return; }

            Object plugin = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(PluginGuid));
            Debug.LogError("[DirectInput] " + DIManager.LastError, plugin);
            var notification = new GUIContent("DirectInput plugin failed to load. See the Console.");
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window is SceneView || window.GetType().Name == "GameView") { window.ShowNotification(notification); }
            }
        }
    }
}
