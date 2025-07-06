#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SpaceNavigatorDriver
{
    [InitializeOnLoad]
    public static class SimpleSceneViewController
    {
        private static double _lastUpdateTime;
        private static float _deltaTimeFactor = 400f;
        private static bool _debugMode = true; // デバッグモードを有効化
        
        static SimpleSceneViewController()
        {
            EditorApplication.update += Update;  // 有効化
            Debug.Log("SimpleSceneViewController: Initialized and enabled");
        }

        private static void Update()
        {
            // Debug: SpaceNavigator device connection check
            if (SpaceNavigatorHID.current == null)
            {
                if (_debugMode)
                {
                    Debug.Log("SimpleSceneViewController: SpaceNavigatorHID.current is null - device not connected");
                }
                return;
            }
            
            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: SpaceNavigator device connected: {SpaceNavigatorHID.current.displayName}");
            }
            
            // Exit if Unity doesn't have focus (optional)
            if (!EditorApplication.isFocused) 
            {
                if (_debugMode)
                {
                    Debug.Log("SimpleSceneViewController: Unity doesn't have focus");
                }
                return;
            }
            
            // Get the active SceneView
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                if (_debugMode)
                {
                    Debug.Log("SimpleSceneViewController: No active SceneView");
                }
                return;
            }

            // Calculate delta time for frame-rate independent movement
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;

            // Read input from SpaceNavigator
            Vector3 translation = SpaceNavigatorHID.current.Translation.ReadValue();
            Vector3 rotation = SpaceNavigatorHID.current.Rotation.ReadValue();

            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: Raw input - Translation: {translation}, Rotation: {rotation}");
            }

            // Apply deadzone - exit if device is idle
            if (IsApproximatelyZero(translation, 0.0001f) && IsApproximatelyZero(rotation, 0.0001f))
            {
                if (_debugMode)
                {
                    Debug.Log("SimpleSceneViewController: Device is idle (within deadzone)");
                }
                return;
            }

            // Make movement frame-rate independent
            translation *= deltaTime * _deltaTimeFactor;
            rotation *= deltaTime * _deltaTimeFactor;

            // Apply sensitivity scaling
            translation *= 0.5f; // Adjust translation sensitivity
            rotation *= 2.0f;    // Adjust rotation sensitivity

            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: Processed input - Translation: {translation}, Rotation: {rotation}");
            }

            // Apply camera transformations
            ApplyCameraMovement(sceneView, translation, rotation);
        }

        private static void ApplyCameraMovement(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            if (_debugMode)
            {
                Debug.Log("SimpleSceneViewController: Applying camera movement");
            }

            // Create a temporary transform to represent the camera
            var tempTransform = new GameObject("TempCamera").transform;
            tempTransform.position = sceneView.camera.transform.position;
            tempTransform.rotation = sceneView.camera.transform.rotation;
            tempTransform.hideFlags = HideFlags.HideAndDontSave;

            // Apply translation in local space (camera's coordinate system)
            tempTransform.Translate(translation, Space.Self);

            // Apply rotation using quaternion math
            if (!sceneView.orthographic)
            {
                // Convert rotation vector to quaternion and apply
                Quaternion rotationQuaternion = Quaternion.Euler(rotation.x, rotation.y, rotation.z);
                tempTransform.rotation = tempTransform.rotation * rotationQuaternion;
            }

            // Update SceneView camera
            sceneView.pivot = tempTransform.position;
            sceneView.rotation = tempTransform.rotation;
            
            // Handle orthographic size for zoom
            if (sceneView.orthographic)
            {
                sceneView.size = Mathf.Max(0.1f, sceneView.size - translation.z);
            }

            // Clean up temporary object
            Object.DestroyImmediate(tempTransform.gameObject);

            // Refresh the SceneView to show changes
            sceneView.Repaint();
        }

        private static bool IsApproximatelyZero(Vector3 vector, float epsilon)
        {
            return vector.magnitude < epsilon;
        }
    }
}
#endif