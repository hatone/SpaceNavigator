#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SpaceNavigatorDriver
{
    [System.Serializable]
    public class MacroStep
    {
        public enum StepType
        {
            MenuItem,
            CustomAction,
            Hotkey,
            Delay
        }

        public StepType stepType;
        public string actionData;
        public float delay = 0f; // Additional delay after this step
    }

    [CreateAssetMenu(fileName = "SpaceMouseMacro", menuName = "SpaceMouse/Macro")]
    public class SpaceMouseMacro : ScriptableObject
    {
        [Header("Macro Information")]
        public string macroName = "New Macro";
        public string description = "Macro description";

        [Header("Macro Steps")]
        public List<MacroStep> steps = new List<MacroStep>();

        private void OnEnable()
        {
            if (steps.Count == 0)
            {
                // Add a sample step
                steps.Add(new MacroStep
                {
                    stepType = MacroStep.StepType.CustomAction,
                    actionData = "FrameSelected",
                    delay = 0.1f
                });
            }
        }
    }

    [CustomEditor(typeof(SpaceMouseMacro))]
    public class SpaceMouseMacroEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var macro = (SpaceMouseMacro)target;

            EditorGUILayout.LabelField("SpaceMouse Macro", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Macro info
            macro.macroName = EditorGUILayout.TextField("Macro Name", macro.macroName);
            macro.description = EditorGUILayout.TextField("Description", macro.description);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Macro Steps", EditorStyles.boldLabel);

            // Steps list
            for (int i = 0; i < macro.steps.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Step {i + 1}", EditorStyles.boldLabel, GUILayout.Width(60));
                
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    macro.steps.RemoveAt(i);
                    EditorUtility.SetDirty(macro);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                var step = macro.steps[i];
                step.stepType = (MacroStep.StepType)EditorGUILayout.EnumPopup("Type", step.stepType);
                
                switch (step.stepType)
                {
                    case MacroStep.StepType.MenuItem:
                        step.actionData = EditorGUILayout.TextField("Menu Path", step.actionData);
                        EditorGUILayout.HelpBox("Example: Edit/Undo", MessageType.Info);
                        break;
                    case MacroStep.StepType.CustomAction:
                        step.actionData = EditorGUILayout.TextField("Action Name", step.actionData);
                        EditorGUILayout.HelpBox("Examples: FrameSelected, FitView, ToggleHorizonLock", MessageType.Info);
                        break;
                    case MacroStep.StepType.Hotkey:
                        step.actionData = EditorGUILayout.TextField("Hotkey", step.actionData);
                        EditorGUILayout.HelpBox("Examples: ctrl+s, f, ctrl+z", MessageType.Info);
                        break;
                    case MacroStep.StepType.Delay:
                        step.actionData = EditorGUILayout.FloatField("Duration (seconds)", float.Parse(step.actionData ?? "1")).ToString();
                        break;
                }

                step.delay = EditorGUILayout.FloatField("Additional Delay", step.delay);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            // Add step button
            if (GUILayout.Button("Add Step"))
            {
                macro.steps.Add(new MacroStep());
                EditorUtility.SetDirty(macro);
            }

            EditorGUILayout.Space();

            // Test and execute buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Macro"))
            {
                MacroSystem.ExecuteMacro(macro);
            }
            if (GUILayout.Button("Register Macro"))
            {
                MacroSystem.RegisterMacro(macro);
            }
            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(macro);
            }
        }
    }

    public static class MacroSystem
    {
        private static Dictionary<string, SpaceMouseMacro> _registeredMacros = new Dictionary<string, SpaceMouseMacro>();
        private static EditorCoroutine _currentMacro;

        static MacroSystem()
        {
            LoadRegisteredMacros();
        }

        public static void RegisterMacro(SpaceMouseMacro macro)
        {
            if (macro != null && !string.IsNullOrEmpty(macro.macroName))
            {
                _registeredMacros[macro.macroName] = macro;
                Debug.Log($"Registered macro: {macro.macroName}");
                SaveRegisteredMacros();
            }
        }

        public static void ExecuteMacro(string macroName)
        {
            if (_registeredMacros.TryGetValue(macroName, out SpaceMouseMacro macro))
            {
                ExecuteMacro(macro);
            }
            else
            {
                Debug.LogWarning($"Macro not found: {macroName}");
            }
        }

        public static void ExecuteMacro(SpaceMouseMacro macro)
        {
            if (macro == null || macro.steps == null || macro.steps.Count == 0)
            {
                Debug.LogWarning("Cannot execute empty macro");
                return;
            }

            Debug.Log($"Executing macro: {macro.macroName}");
            
            // Stop any currently running macro
            if (_currentMacro != null)
            {
                EditorCoroutine.Stop(_currentMacro);
            }

            _currentMacro = EditorCoroutine.Start(ExecuteMacroCoroutine(macro));
        }

        private static IEnumerator ExecuteMacroCoroutine(SpaceMouseMacro macro)
        {
            foreach (var step in macro.steps)
            {
                ExecuteStep(step);
                
                // Wait for step delay plus any additional delay
                float totalDelay = step.delay;
                if (step.stepType == MacroStep.StepType.Delay && float.TryParse(step.actionData, out float delayTime))
                {
                    totalDelay += delayTime;
                }

                if (totalDelay > 0)
                {
                    yield return new WaitForSecondsRealtime(totalDelay);
                }
            }

            _currentMacro = null;
            Debug.Log($"Macro completed: {macro.macroName}");
        }

        private static void ExecuteStep(MacroStep step)
        {
            try
            {
                switch (step.stepType)
                {
                    case MacroStep.StepType.MenuItem:
                        if (!string.IsNullOrEmpty(step.actionData))
                        {
                            EditorApplication.ExecuteMenuItem(step.actionData);
                            Debug.Log($"Executed menu item: {step.actionData}");
                        }
                        break;

                    case MacroStep.StepType.CustomAction:
                        ExecuteCustomAction(step.actionData);
                        break;

                    case MacroStep.StepType.Hotkey:
                        ExecuteHotkey(step.actionData);
                        break;

                    case MacroStep.StepType.Delay:
                        // Delay is handled in the coroutine
                        Debug.Log($"Delaying for {step.actionData} seconds");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing macro step: {e.Message}");
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
                case "RecalibrateDrift":
                    SimpleSceneViewController.RecalibrateDrift();
                    break;
                case "ToggleHorizonLock":
                    SimpleSceneViewController.ToggleHorizonLock();
                    break;
                case "ToggleRotationLock":
                    SimpleSceneViewController.ToggleRotationLock();
                    break;
                case "SetFlyMode":
                    SimpleSceneViewController.SetFlyMode();
                    break;
                case "SetCameraMode":
                    SimpleSceneViewController.SetCameraMode();
                    break;
                case "SetObjectMode":
                    SimpleSceneViewController.SetObjectMode();
                    break;
                case "SetWalkMode":
                    SimpleSceneViewController.SetWalkMode();
                    break;
                case "SetDroneMode":
                    SimpleSceneViewController.SetDroneMode();
                    break;
                default:
                    Debug.LogWarning($"Custom action not implemented: {actionName}");
                    break;
            }
            Debug.Log($"Executed custom action: {actionName}");
        }

        private static void ExecuteHotkey(string hotkey)
        {
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
            Debug.Log($"Executed hotkey: {hotkey}");
        }

        private static void LoadRegisteredMacros()
        {
            string[] guids = AssetDatabase.FindAssets("t:SpaceMouseMacro");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SpaceMouseMacro macro = AssetDatabase.LoadAssetAtPath<SpaceMouseMacro>(path);
                if (macro != null && !string.IsNullOrEmpty(macro.macroName))
                {
                    _registeredMacros[macro.macroName] = macro;
                }
            }
            Debug.Log($"Loaded {_registeredMacros.Count} macros");
        }

        private static void SaveRegisteredMacros()
        {
            // For now, macros are saved as ScriptableObject assets
            // In the future, this could save to a registry file
        }

        [MenuItem("Window/SpaceMouse/Create Macro")]
        public static void CreateMacro()
        {
            var macro = ScriptableObject.CreateInstance<SpaceMouseMacro>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/SpaceMouse/Macro.asset");
            
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            AssetDatabase.CreateAsset(macro, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Selection.activeObject = macro;
            Debug.Log($"Created macro at: {path}");
        }

        [MenuItem("Window/SpaceMouse/Reload Macros")]
        public static void ReloadMacros()
        {
            _registeredMacros.Clear();
            LoadRegisteredMacros();
        }
    }

    // Simple EditorCoroutine implementation for macro execution
    public class EditorCoroutine
    {
        private static List<EditorCoroutine> _coroutines = new List<EditorCoroutine>();
        private IEnumerator _routine;
        private bool _isDone = false;

        static EditorCoroutine()
        {
            EditorApplication.update += UpdateCoroutines;
            
            // Clear coroutines on domain reload to prevent memory leaks
            _coroutines.Clear();
        }

        private EditorCoroutine(IEnumerator routine)
        {
            _routine = routine;
        }

        public static EditorCoroutine Start(IEnumerator routine)
        {
            var coroutine = new EditorCoroutine(routine);
            _coroutines.Add(coroutine);
            return coroutine;
        }

        public static void Stop(EditorCoroutine coroutine)
        {
            if (coroutine != null)
            {
                coroutine._isDone = true;
                _coroutines.Remove(coroutine);
            }
        }

        private static void UpdateCoroutines()
        {
            for (int i = _coroutines.Count - 1; i >= 0; i--)
            {
                var coroutine = _coroutines[i];
                if (coroutine._isDone || !coroutine._routine.MoveNext())
                {
                    _coroutines.RemoveAt(i);
                }
            }
        }
    }

    // Simple wait class for editor coroutines
    public class WaitForSecondsRealtime
    {
        public float Duration { get; private set; }
        private float _startTime;

        public WaitForSecondsRealtime(float duration)
        {
            Duration = duration;
            _startTime = (float)EditorApplication.timeSinceStartup;
        }

        public bool IsComplete => (float)EditorApplication.timeSinceStartup - _startTime >= Duration;
    }
}
#endif