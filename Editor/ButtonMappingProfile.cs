#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SpaceNavigatorDriver
{
    [System.Serializable]
    public class ButtonAction
    {
        public enum ActionType
        {
            UnityHotkey,
            MenuItem,
            CustomAction,
            RadialMenu,
            Macro
        }

        public ActionType actionType;
        public string actionData; // Hotkey string, menu path, action name, etc.
        public float longPressDuration = 0.5f; // For long-press actions
        public string longPressAction; // Action to execute on long press
    }

    [CreateAssetMenu(fileName = "SpaceMouseButtonProfile", menuName = "SpaceMouse/Button Profile")]
    public class ButtonMappingProfile : ScriptableObject
    {
        [Header("Profile Information")]
        public string profileName = "Default Profile";
        public string description = "Default button mappings for SpaceMouse";

        [Header("Button Mappings")]
        public ButtonAction button1 = new ButtonAction();
        public ButtonAction button2 = new ButtonAction();
        public ButtonAction button3 = new ButtonAction();
        public ButtonAction button4 = new ButtonAction();
        public ButtonAction button5 = new ButtonAction();
        public ButtonAction button6 = new ButtonAction();
        public ButtonAction button7 = new ButtonAction();
        public ButtonAction button8 = new ButtonAction();

        [Header("Special Buttons")]
        public ButtonAction menuButton = new ButtonAction();
        public ButtonAction fitButton = new ButtonAction();

        private void OnEnable()
        {
            SetupDefaultMappings();
        }

        private void SetupDefaultMappings()
        {
            if (string.IsNullOrEmpty(button1.actionData))
            {
                // B1 = Frame Selected
                button1.actionType = ButtonAction.ActionType.MenuItem;
                button1.actionData = "Window/SpaceNavigator/Frame Selected";
                button1.longPressAction = "RadialMenu:nav";

                // B2 = Fit View
                button2.actionType = ButtonAction.ActionType.CustomAction;
                button2.actionData = "FitView";

                // B3 = Toggle Scene/Game
                button3.actionType = ButtonAction.ActionType.CustomAction;
                button3.actionData = "ToggleSceneGame";

                // B4 = Play/Pause
                button4.actionType = ButtonAction.ActionType.UnityHotkey;
                button4.actionData = "ctrl+p";

                // B5 = Undo
                button5.actionType = ButtonAction.ActionType.UnityHotkey;
                button5.actionData = "ctrl+z";

                // B6 = Redo
                button6.actionType = ButtonAction.ActionType.UnityHotkey;
                button6.actionData = "ctrl+y";

                // Menu Button = Radial Menu
                menuButton.actionType = ButtonAction.ActionType.RadialMenu;
                menuButton.actionData = "nav";

                // Fit Button = Frame Selected
                fitButton.actionType = ButtonAction.ActionType.CustomAction;
                fitButton.actionData = "FrameSelected";
            }
        }

        public ButtonAction GetButtonAction(int buttonIndex)
        {
            switch (buttonIndex)
            {
                case 1: return button1;
                case 2: return button2;
                case 3: return button3;
                case 4: return button4;
                case 5: return button5;
                case 6: return button6;
                case 7: return button7;
                case 8: return button8;
                case 100: return menuButton; // Special menu button
                case 101: return fitButton;  // Special fit button
                default: return null;
            }
        }
    }

    [CustomEditor(typeof(ButtonMappingProfile))]
    public class ButtonMappingProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var profile = (ButtonMappingProfile)target;

            EditorGUILayout.LabelField("SpaceMouse Button Profile", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Test Button Mappings"))
            {
                ButtonMappingSystem.TestButtonMappings(profile);
            }

            if (GUILayout.Button("Set as Active Profile"))
            {
                ButtonMappingSystem.SetActiveProfile(profile);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Action Types:\n" +
                "• Unity Hotkey: Standard Unity shortcuts (e.g., 'ctrl+s', 'f')\n" +
                "• Menu Item: Unity menu paths (e.g., 'Edit/Undo')\n" +
                "• Custom Action: Built-in SpaceMouse actions\n" +
                "• Radial Menu: Show radial menu by ID\n" +
                "• Macro: Execute macro sequence by name",
                MessageType.Info
            );
        }
    }

    public static class ButtonMappingSystem
    {
        private static ButtonMappingProfile _activeProfile;
        private static Dictionary<int, float> _buttonPressStartTimes = new Dictionary<int, float>();
        private static Dictionary<int, bool> _longPressTriggered = new Dictionary<int, bool>();

        static ButtonMappingSystem()
        {
            LoadActiveProfile();
        }

        public static void LoadActiveProfile()
        {
            string profilePath = EditorPrefs.GetString("SpaceMouse_ActiveButtonProfile", "");
            if (!string.IsNullOrEmpty(profilePath))
            {
                _activeProfile = AssetDatabase.LoadAssetAtPath<ButtonMappingProfile>(profilePath);
            }

            if (_activeProfile == null)
            {
                // Try to find a default profile
                string[] guids = AssetDatabase.FindAssets("t:ButtonMappingProfile");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _activeProfile = AssetDatabase.LoadAssetAtPath<ButtonMappingProfile>(path);
                }
            }
        }

        public static void SetActiveProfile(ButtonMappingProfile profile)
        {
            _activeProfile = profile;
            string path = AssetDatabase.GetAssetPath(profile);
            EditorPrefs.SetString("SpaceMouse_ActiveButtonProfile", path);
            Debug.Log($"Set active SpaceMouse button profile: {profile.profileName}");
        }

        public static void HandleButtonPress(int buttonIndex)
        {
            if (_activeProfile == null) return;

            var action = _activeProfile.GetButtonAction(buttonIndex);
            if (action == null) return;

            // Record press start time for long-press detection
            _buttonPressStartTimes[buttonIndex] = Time.realtimeSinceStartup;
            _longPressTriggered[buttonIndex] = false;
        }

        public static void HandleButtonHold(int buttonIndex)
        {
            if (_activeProfile == null) return;

            var action = _activeProfile.GetButtonAction(buttonIndex);
            if (action == null) return;

            // Check for long press
            if (_buttonPressStartTimes.ContainsKey(buttonIndex) && 
                !_longPressTriggered.GetValueOrDefault(buttonIndex, false))
            {
                float holdTime = Time.realtimeSinceStartup - _buttonPressStartTimes[buttonIndex];
                if (holdTime >= action.longPressDuration && !string.IsNullOrEmpty(action.longPressAction))
                {
                    _longPressTriggered[buttonIndex] = true;
                    ExecuteActionString(action.longPressAction);
                }
            }
        }

        public static void HandleButtonRelease(int buttonIndex)
        {
            if (_activeProfile == null) return;

            var action = _activeProfile.GetButtonAction(buttonIndex);
            if (action == null) return;

            // Execute normal action only if long press wasn't triggered
            if (!_longPressTriggered.GetValueOrDefault(buttonIndex, false))
            {
                ExecuteAction(action);
            }

            // Clean up press tracking
            _buttonPressStartTimes.Remove(buttonIndex);
            _longPressTriggered.Remove(buttonIndex);
        }

        private static void ExecuteAction(ButtonAction action)
        {
            switch (action.actionType)
            {
                case ButtonAction.ActionType.UnityHotkey:
                    ExecuteHotkey(action.actionData);
                    break;
                case ButtonAction.ActionType.MenuItem:
                    EditorApplication.ExecuteMenuItem(action.actionData);
                    break;
                case ButtonAction.ActionType.CustomAction:
                    ExecuteCustomAction(action.actionData);
                    break;
                case ButtonAction.ActionType.RadialMenu:
                    RadialMenuSystem.ShowMenu(action.actionData);
                    break;
                case ButtonAction.ActionType.Macro:
                    MacroSystem.ExecuteMacro(action.actionData);
                    break;
            }
        }

        private static void ExecuteActionString(string actionString)
        {
            if (actionString.StartsWith("RadialMenu:"))
            {
                string menuId = actionString.Substring(11);
                RadialMenuSystem.ShowMenu(menuId);
            }
            else if (actionString.StartsWith("Macro:"))
            {
                string macroName = actionString.Substring(6);
                MacroSystem.ExecuteMacro(macroName);
            }
            else if (actionString.StartsWith("Hotkey:"))
            {
                string hotkey = actionString.Substring(7);
                ExecuteHotkey(hotkey);
            }
            else
            {
                ExecuteCustomAction(actionString);
            }
        }

        private static void ExecuteHotkey(string hotkey)
        {
            // This would need platform-specific implementation
            Debug.Log($"Execute hotkey: {hotkey}");
            
            // For now, handle common Unity shortcuts
            switch (hotkey.ToLower())
            {
                case "ctrl+z":
                    Undo.PerformUndo();
                    break;
                case "ctrl+y":
                    Undo.PerformRedo();
                    break;
                case "ctrl+s":
                    EditorApplication.ExecuteMenuItem("File/Save");
                    break;
                case "f":
                    if (SceneView.lastActiveSceneView != null)
                        SceneView.lastActiveSceneView.FrameSelected();
                    break;
                case "ctrl+p":
                    EditorApplication.isPlaying = !EditorApplication.isPlaying;
                    break;
                default:
                    Debug.LogWarning($"Hotkey not implemented: {hotkey}");
                    break;
            }
        }

        private static void ExecuteCustomAction(string actionName)
        {
            switch (actionName)
            {
                case "FrameSelected":
                    if (SceneView.lastActiveSceneView != null)
                        SceneView.lastActiveSceneView.FrameSelected();
                    break;
                case "FitView":
                    if (SceneView.lastActiveSceneView != null)
                        SceneView.lastActiveSceneView.FrameSelected();
                    break;
                case "ToggleSceneGame":
                    // Toggle between Scene and Game view
                    Debug.Log("Toggle Scene/Game view");
                    break;
                case "RecalibrateDrift":
                    SimpleSceneViewController.RecalibrateDrift();
                    break;
                case "ToggleHorizonLock":
                    SimpleSceneViewController.ToggleHorizonLock();
                    break;
                case "ToggleRotationLock":
                    SimpleSceneViewController.ToggleRotationLock();
                    break;
                default:
                    Debug.LogWarning($"Custom action not implemented: {actionName}");
                    break;
            }
        }

        public static void TestButtonMappings(ButtonMappingProfile profile)
        {
            Debug.Log($"Testing button mappings for profile: {profile.profileName}");
            for (int i = 1; i <= 8; i++)
            {
                var action = profile.GetButtonAction(i);
                if (action != null && !string.IsNullOrEmpty(action.actionData))
                {
                    Debug.Log($"Button {i}: {action.actionType} - {action.actionData}");
                }
            }
        }

        [MenuItem("Window/SpaceMouse/Create Button Profile")]
        public static void CreateButtonProfile()
        {
            var profile = ScriptableObject.CreateInstance<ButtonMappingProfile>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/SpaceMouse/ButtonProfile.asset");
            
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Selection.activeObject = profile;
            Debug.Log($"Created button profile at: {path}");
        }
    }
}
#endif