#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceNavigatorDriver
{
    public class SpaceNavigatorDebugHelper : EditorWindow
    {
        [MenuItem("Window/SpaceNavigator/Debug Helper")]
        public static void ShowWindow()
        {
            GetWindow<SpaceNavigatorDebugHelper>("SpaceNavigator Debug");
        }

        private void OnGUI()
        {
            GUILayout.Label("SpaceNavigator Debug Information", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Input System status
            GUILayout.Label("Input System Status:");
            GUILayout.Label($"Input System enabled: {InputSystem.settings != null}");
            GUILayout.Space(5);

            // Device connection status
            GUILayout.Label("Device Connection:");
            if (SpaceNavigatorHID.current != null)
            {
                GUILayout.Label($"Device connected: {SpaceNavigatorHID.current.displayName}");
                GUILayout.Label($"Device ID: {SpaceNavigatorHID.current.deviceId}");
                GUILayout.Label($"Device enabled: {SpaceNavigatorHID.current.enabled}");
                
                GUILayout.Space(5);
                GUILayout.Label("Current Input Values:");
                Vector3 translation = SpaceNavigatorHID.current.Translation.ReadValue();
                Vector3 rotation = SpaceNavigatorHID.current.Rotation.ReadValue();
                
                GUILayout.Label($"Translation: {translation}");
                GUILayout.Label($"Rotation: {rotation}");
                
                GUILayout.Space(5);
                GUILayout.Label("Button States:");
                GUILayout.Label($"Button 1: {SpaceNavigatorHID.current.Button1.isPressed}");
                GUILayout.Label($"Button 2: {SpaceNavigatorHID.current.Button2.isPressed}");
            }
            else
            {
                GUILayout.Label("No SpaceNavigator device connected");
            }

            GUILayout.Space(10);
            
            // All connected devices
            GUILayout.Label("All Connected Input Devices:");
            foreach (var device in InputSystem.devices)
            {
                if (device.description.interfaceName == "HID")
                {
                    GUILayout.Label($"- {device.displayName} ({device.description.manufacturer})");
                }
            }

            GUILayout.Space(10);
            
            // Unity focus status
            GUILayout.Label($"Unity has focus: {EditorApplication.isFocused}");
            GUILayout.Label($"Scene view active: {SceneView.lastActiveSceneView != null}");

            // Refresh button
            if (GUILayout.Button("Refresh"))
            {
                Repaint();
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}
#endif 