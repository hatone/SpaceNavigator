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
        
        // ドリフト補正用
        private static Vector3 _translationDrift = Vector3.zero;
        private static Vector3 _rotationDrift = Vector3.zero;
        private static bool _driftCalibrated = false;
        
        static SimpleSceneViewController()
        {
            EditorApplication.update += Update;  // Re-enabled with improved implementation
            Debug.Log("SimpleSceneViewController: Initialized with improved SpaceMouse behavior");
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

            // 初回実行時にドリフトを記録
            if (!_driftCalibrated)
            {
                _translationDrift = translation;
                _rotationDrift = rotation;
                _driftCalibrated = true;
                Debug.Log($"SimpleSceneViewController: Drift calibrated - Translation: {_translationDrift}, Rotation: {_rotationDrift}");
            }

            // ドリフト補正を適用
            translation -= _translationDrift;
            rotation -= _rotationDrift;

            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: Raw input - Translation: {SpaceNavigatorHID.current.Translation.ReadValue()}, Rotation: {SpaceNavigatorHID.current.Rotation.ReadValue()}");
                Debug.Log($"SimpleSceneViewController: Drift corrected - Translation: {translation}, Rotation: {rotation}");
            }

            // Apply deadzone - exit if device is idle
            bool translationIdle = IsApproximatelyZero(translation, 0.01f);    // 移動のデッドゾーンを上げる
            bool rotationIdle = IsApproximatelyZero(rotation, 0.01f);          // 回転のデッドゾーンを上げる
            
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

            // Apply sensitivity scaling for better feel
            translation *= 1.5f; // Improved translation sensitivity
            rotation *= 1.2f;    // Improved rotation sensitivity for natural orbit

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
                Debug.Log($"SimpleSceneViewController: Applying improved camera movement - Translation: {translation}, Rotation: {rotation}");
            }

            // Get current camera state
            Vector3 cameraPosition = sceneView.camera.transform.position;
            Vector3 pivotPosition = sceneView.pivot;
            Quaternion cameraRotation = sceneView.rotation;

            // Calculate camera-to-pivot distance for proper scaling
            float distanceToPivot = Vector3.Distance(cameraPosition, pivotPosition);
            float scaledTranslationFactor = Mathf.Max(0.1f, distanceToPivot * 0.1f);

            // === TRANSLATION (Panning) ===
            // Apply translation in world space, scaled by distance to pivot
            Vector3 worldTranslation = Vector3.zero;
            
            // Right/Left movement (X-axis)
            worldTranslation += cameraRotation * Vector3.right * translation.x * scaledTranslationFactor;
            
            // Up/Down movement (Y-axis) 
            worldTranslation += cameraRotation * Vector3.up * translation.y * scaledTranslationFactor;
            
            // Update pivot position for panning
            Vector3 newPivotPosition = pivotPosition + worldTranslation;

            // === ZOOM (Dolly) ===
            // Handle forward/backward as camera dolly (moving camera closer/farther from pivot)
            float zoomInput = translation.z * 3.0f;  // Zoom sensitivity
            
            // Calculate zoom direction (from pivot to camera)
            Vector3 zoomDirection = (cameraPosition - pivotPosition).normalized;
            
            // Apply zoom while maintaining minimum distance
            float currentDistance = Vector3.Distance(cameraPosition, pivotPosition);
            float newDistance = Mathf.Max(0.1f, currentDistance - zoomInput);
            
            // Calculate new camera position for zoom
            Vector3 newCameraPosition = newPivotPosition + zoomDirection * newDistance;

            // === ROTATION (Orbit) ===
            // Apply rotation as orbit around pivot point
            Quaternion deltaRotation = Quaternion.identity;
            
            if (!sceneView.orthographic && rotation.magnitude > 0.001f)
            {
                // Create rotation around pivot
                // Pitch (X-axis) - rotate around camera's right vector projected onto world plane
                Vector3 rightVector = Vector3.Cross(Vector3.up, (newCameraPosition - newPivotPosition).normalized);
                if (rightVector.magnitude > 0.001f)
                {
                    rightVector = rightVector.normalized;
                    deltaRotation = Quaternion.AngleAxis(-rotation.x * 57.2958f, rightVector) * deltaRotation;
                }
                
                // Yaw (Y-axis) - rotate around world up vector
                deltaRotation = Quaternion.AngleAxis(rotation.y * 57.2958f, Vector3.up) * deltaRotation;
                
                // Roll (Z-axis) - rotate around camera's forward vector (minimal for natural feel)
                Vector3 forwardVector = (newCameraPosition - newPivotPosition).normalized;
                deltaRotation = Quaternion.AngleAxis(rotation.z * 57.2958f * 0.5f, forwardVector) * deltaRotation;
            }

            // Apply rotation to camera position around pivot
            Vector3 rotatedOffset = deltaRotation * (newCameraPosition - newPivotPosition);
            Vector3 finalCameraPosition = newPivotPosition + rotatedOffset;
            
            // Update camera rotation to look at pivot
            Vector3 lookDirection = (newPivotPosition - finalCameraPosition).normalized;
            Quaternion finalCameraRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            
            // Apply the final transformations to SceneView
            sceneView.pivot = newPivotPosition;
            sceneView.rotation = finalCameraRotation;
            
            // Handle orthographic mode specially
            if (sceneView.orthographic)
            {
                // In orthographic mode, adjust size instead of position for zoom
                float sizeChange = translation.z * 3.0f * 0.1f;
                sceneView.size = Mathf.Max(0.01f, sceneView.size - sizeChange);
            }

            // Refresh the SceneView to show changes
            sceneView.Repaint();
        }

        private static bool IsApproximatelyZero(Vector3 vector, float epsilon)
        {
            return vector.magnitude < epsilon;
        }

        [MenuItem("Window/SpaceNavigator/Recalibrate Drift")]
        public static void RecalibrateDrift()
        {
            if (SpaceNavigatorHID.current != null)
            {
                _translationDrift = SpaceNavigatorHID.current.Translation.ReadValue();
                _rotationDrift = SpaceNavigatorHID.current.Rotation.ReadValue();
                _driftCalibrated = true;
                Debug.Log($"SimpleSceneViewController: Drift recalibrated - Translation: {_translationDrift}, Rotation: {_rotationDrift}");
            }
            else
            {
                Debug.LogWarning("SimpleSceneViewController: No SpaceNavigator device connected for drift calibration");
            }
        }
        
        [MenuItem("Window/SpaceNavigator/Toggle Debug Mode")]
        public static void ToggleDebugMode()
        {
            _debugMode = !_debugMode;
            Debug.Log($"SimpleSceneViewController: Debug mode {(_debugMode ? "enabled" : "disabled")}");
        }
    }
}
#endif