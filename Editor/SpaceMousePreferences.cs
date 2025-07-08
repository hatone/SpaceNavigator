#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace SpaceNavigatorDriver
{
    public class SpaceMousePreferences : EditorWindow
    {
        private static SpaceMousePreferences _window;
        private Vector2 _scrollPosition;
        
        // Settings
        private static NavigationMode _selectedMode = NavigationMode.Fly;
        private static float _movementSpeed = 1.0f;
        private static float _rotationSpeed = 1.0f;
        private static bool _invertTranslationX = false;
        private static bool _invertTranslationY = false;
        private static bool _invertTranslationZ = false;
        private static bool _invertRotationX = false;
        private static bool _invertRotationY = false;
        private static bool _invertRotationZ = false;
        private static bool _horizonLock = false;
        private static bool _rotationLock = false;
        private static CoordinateSystem _coordinateSystem = CoordinateSystem.World;

        [MenuItem("Window/SpaceMouse/Preferences")]
        public static void OpenPreferences()
        {
            _window = GetWindow<SpaceMousePreferences>("SpaceMouse Preferences");
            _window.minSize = new Vector2(400, 500);
            _window.Show();
            LoadSettings();
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Project/SpaceMouse", SettingsScope.Project)
            {
                label = "SpaceMouse",
                guiHandler = (searchContext) =>
                {
                    LoadSettings();
                    DrawPreferencesGUI();
                    SaveSettings();
                },
                keywords = new[] { "SpaceMouse", "3DConnexion", "Navigation", "Input" }
            };
            return provider;
        }

        private void OnGUI()
        {
            LoadSettings();
            DrawPreferencesGUI();
            SaveSettings();
        }

        private static void DrawPreferencesGUI()
        {
            GUILayout.Label("SpaceMouse Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Navigation Mode
            GUILayout.Label("Navigation Mode", EditorStyles.boldLabel);
            _selectedMode = (NavigationMode)EditorGUILayout.EnumPopup("Mode", _selectedMode);
            
            if (GUILayout.Button("Apply Mode"))
            {
                SimpleSceneViewController.CurrentMode = _selectedMode;
            }
            
            EditorGUILayout.Space();

            // Speed Settings
            GUILayout.Label("Speed Settings", EditorStyles.boldLabel);
            _movementSpeed = EditorGUILayout.Slider("Movement Speed", _movementSpeed, 0.1f, 10f);
            _rotationSpeed = EditorGUILayout.Slider("Rotation Speed", _rotationSpeed, 0.1f, 10f);
            
            EditorGUILayout.Space();

            // Axis Inversion
            GUILayout.Label("Axis Inversion", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Translation:", GUILayout.Width(100));
            _invertTranslationX = EditorGUILayout.Toggle("X", _invertTranslationX, GUILayout.Width(50));
            _invertTranslationY = EditorGUILayout.Toggle("Y", _invertTranslationY, GUILayout.Width(50));
            _invertTranslationZ = EditorGUILayout.Toggle("Z", _invertTranslationZ, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Rotation:", GUILayout.Width(100));
            _invertRotationX = EditorGUILayout.Toggle("X", _invertRotationX, GUILayout.Width(50));
            _invertRotationY = EditorGUILayout.Toggle("Y", _invertRotationY, GUILayout.Width(50));
            _invertRotationZ = EditorGUILayout.Toggle("Z", _invertRotationZ, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();

            // Lock Settings
            GUILayout.Label("Lock Settings", EditorStyles.boldLabel);
            _horizonLock = EditorGUILayout.Toggle("Horizon Lock", _horizonLock);
            _rotationLock = EditorGUILayout.Toggle("Rotation Lock", _rotationLock);
            
            if (GUILayout.Button("Apply Locks"))
            {
                SimpleSceneViewController.HorizonLock = _horizonLock;
                SimpleSceneViewController.RotationLock = _rotationLock;
            }
            
            EditorGUILayout.Space();

            // Coordinate System (for MoveObjects mode)
            GUILayout.Label("Coordinate System (MoveObjects Mode)", EditorStyles.boldLabel);
            _coordinateSystem = (CoordinateSystem)EditorGUILayout.EnumPopup("System", _coordinateSystem);
            
            if (GUILayout.Button("Apply Coordinate System"))
            {
                SimpleSceneViewController.CurrentCoordinateSystem = _coordinateSystem;
            }
            
            EditorGUILayout.Space();

            // Quick Actions
            GUILayout.Label("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Recalibrate Drift"))
            {
                SimpleSceneViewController.RecalibrateDrift();
            }
            if (GUILayout.Button("Toggle Debug"))
            {
                SimpleSceneViewController.ToggleDebugMode();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();

            // Device Status
            GUILayout.Label("Device Status", EditorStyles.boldLabel);
            if (SpaceNavigatorHID.current != null)
            {
                EditorGUILayout.HelpBox($"Connected: {SpaceNavigatorHID.current.displayName}", MessageType.Info);
                
                Vector3 translation = SpaceNavigatorHID.current.Translation.ReadValue();
                Vector3 rotation = SpaceNavigatorHID.current.Rotation.ReadValue();
                
                EditorGUILayout.LabelField("Translation", $"X: {translation.x:F3}, Y: {translation.y:F3}, Z: {translation.z:F3}");
                EditorGUILayout.LabelField("Rotation", $"X: {rotation.x:F3}, Y: {rotation.y:F3}, Z: {rotation.z:F3}");
            }
            else
            {
                EditorGUILayout.HelpBox("No SpaceMouse device connected", MessageType.Warning);
            }
            
            EditorGUILayout.Space();

            // Reset to Defaults
            if (GUILayout.Button("Reset to Defaults"))
            {
                ResetToDefaults();
            }
        }

        private static void LoadSettings()
        {
            _selectedMode = (NavigationMode)EditorPrefs.GetInt("SpaceMouse_NavigationMode", (int)NavigationMode.Fly);
            _movementSpeed = EditorPrefs.GetFloat("SpaceMouse_MovementSpeed", 1.0f);
            _rotationSpeed = EditorPrefs.GetFloat("SpaceMouse_RotationSpeed", 1.0f);
            _invertTranslationX = EditorPrefs.GetBool("SpaceMouse_InvertTranslationX", false);
            _invertTranslationY = EditorPrefs.GetBool("SpaceMouse_InvertTranslationY", false);
            _invertTranslationZ = EditorPrefs.GetBool("SpaceMouse_InvertTranslationZ", false);
            _invertRotationX = EditorPrefs.GetBool("SpaceMouse_InvertRotationX", false);
            _invertRotationY = EditorPrefs.GetBool("SpaceMouse_InvertRotationY", false);
            _invertRotationZ = EditorPrefs.GetBool("SpaceMouse_InvertRotationZ", false);
            _horizonLock = EditorPrefs.GetBool("SpaceMouse_HorizonLock", false);
            _rotationLock = EditorPrefs.GetBool("SpaceMouse_RotationLock", false);
            _coordinateSystem = (CoordinateSystem)EditorPrefs.GetInt("SpaceMouse_CoordinateSystem", (int)CoordinateSystem.World);
        }

        private static void SaveSettings()
        {
            EditorPrefs.SetInt("SpaceMouse_NavigationMode", (int)_selectedMode);
            EditorPrefs.SetFloat("SpaceMouse_MovementSpeed", _movementSpeed);
            EditorPrefs.SetFloat("SpaceMouse_RotationSpeed", _rotationSpeed);
            EditorPrefs.SetBool("SpaceMouse_InvertTranslationX", _invertTranslationX);
            EditorPrefs.SetBool("SpaceMouse_InvertTranslationY", _invertTranslationY);
            EditorPrefs.SetBool("SpaceMouse_InvertTranslationZ", _invertTranslationZ);
            EditorPrefs.SetBool("SpaceMouse_InvertRotationX", _invertRotationX);
            EditorPrefs.SetBool("SpaceMouse_InvertRotationY", _invertRotationY);
            EditorPrefs.SetBool("SpaceMouse_InvertRotationZ", _invertRotationZ);
            EditorPrefs.SetBool("SpaceMouse_HorizonLock", _horizonLock);
            EditorPrefs.SetBool("SpaceMouse_RotationLock", _rotationLock);
            EditorPrefs.SetInt("SpaceMouse_CoordinateSystem", (int)_coordinateSystem);
        }

        private static void ResetToDefaults()
        {
            _selectedMode = NavigationMode.Fly;
            _movementSpeed = 1.0f;
            _rotationSpeed = 1.0f;
            _invertTranslationX = false;
            _invertTranslationY = false;
            _invertTranslationZ = false;
            _invertRotationX = false;
            _invertRotationY = false;
            _invertRotationZ = false;
            _horizonLock = false;
            _rotationLock = false;
            _coordinateSystem = CoordinateSystem.World;
            SaveSettings();
        }

        // Public access for the settings
        public static float MovementSpeed => _movementSpeed;
        public static float RotationSpeed => _rotationSpeed;
        public static bool InvertTranslationX => _invertTranslationX;
        public static bool InvertTranslationY => _invertTranslationY;
        public static bool InvertTranslationZ => _invertTranslationZ;
        public static bool InvertRotationX => _invertRotationX;
        public static bool InvertRotationY => _invertRotationY;
        public static bool InvertRotationZ => _invertRotationZ;
    }
}
#endif