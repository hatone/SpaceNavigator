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
            bool translationIdle = IsApproximatelyZero(translation, 0.0001f);
            bool rotationIdle = IsApproximatelyZero(rotation, 0.00005f);  // 回転のデッドゾーンをより小さく
            
            if (translationIdle && rotationIdle)
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
            rotation *= 5.0f;    // Adjust rotation sensitivity (増加)

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
                Debug.Log($"SimpleSceneViewController: Applying camera movement - Translation: {translation}, Rotation: {rotation}");
            }

            // Get current camera transform
            Transform cameraTransform = sceneView.camera.transform;
            Vector3 currentPosition = cameraTransform.position;
            Quaternion currentRotation = cameraTransform.rotation;

            // Apply translation in local space (camera's coordinate system)
            Vector3 newPosition = currentPosition;
            newPosition += cameraTransform.right * translation.x;      // Left/Right
            newPosition += cameraTransform.up * translation.y;        // Up/Down
            newPosition += cameraTransform.forward * translation.z;   // Forward/Backward

            // Apply rotation - use a more direct approach
            Quaternion newRotation = currentRotation;
            if (!sceneView.orthographic && rotation.magnitude > 0.0001f)
            {
                // Apply pitch (X-axis rotation) around camera's right vector
                if (Mathf.Abs(rotation.x) > 0.0001f)
                {
                    newRotation = Quaternion.AngleAxis(rotation.x, cameraTransform.right) * newRotation;
                }
                
                // Apply yaw (Y-axis rotation) around world up vector
                if (Mathf.Abs(rotation.y) > 0.0001f)
                {
                    newRotation = Quaternion.AngleAxis(rotation.y, Vector3.up) * newRotation;
                }
                
                // Apply roll (Z-axis rotation) around camera's forward vector
                if (Mathf.Abs(rotation.z) > 0.0001f)
                {
                    newRotation = Quaternion.AngleAxis(rotation.z, cameraTransform.forward) * newRotation;
                }

                if (_debugMode)
                {
                    Debug.Log($"SimpleSceneViewController: Rotation applied - Pitch: {rotation.x}, Yaw: {rotation.y}, Roll: {rotation.z}");
                }
            }

            // Update SceneView camera
            sceneView.pivot = newPosition;
            sceneView.rotation = newRotation;
            
            // Handle orthographic size for zoom
            if (sceneView.orthographic)
            {
                sceneView.size = Mathf.Max(0.1f, sceneView.size - translation.z);
            }

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