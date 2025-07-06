#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SpaceNavigatorDriver
{
    [InitializeOnLoad]
    public static class ImprovedSpaceNavigatorController
    {
        private static double _lastUpdateTime;
        private static bool _debugMode = false;
        
        // Drift compensation
        private static Vector3 _translationDrift = Vector3.zero;
        private static Vector3 _rotationDrift = Vector3.zero;
        private static bool _driftCalibrated = false;
        
        // Sensitivity settings
        private static float _translationSensitivity = 2.0f;
        private static float _rotationSensitivity = 1.5f;
        private static float _zoomSensitivity = 3.0f;
        
        // Input smoothing
        private static Vector3 _smoothedTranslation = Vector3.zero;
        private static Vector3 _smoothedRotation = Vector3.zero;
        private static float _smoothingFactor = 0.1f;
        
        // Camera state
        private static Vector3 _lastPivotPosition = Vector3.zero;
        private static bool _pivotInitialized = false;
        
        static ImprovedSpaceNavigatorController()
        {
            EditorApplication.update += Update;
            Debug.Log("ImprovedSpaceNavigatorController: Initialized");
        }

        private static void Update()
        {
            if (SpaceNavigatorHID.current == null) return;
            if (!EditorApplication.isFocused) return;
            
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;

            // Calculate delta time
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;
            
            // Skip if delta time is too large (prevents jumps after pause)
            if (deltaTime > 0.1f) return;

            // Read and process input
            Vector3 translation = SpaceNavigatorHID.current.Translation.ReadValue();
            Vector3 rotation = SpaceNavigatorHID.current.Rotation.ReadValue();

            // Calibrate drift on first use
            if (!_driftCalibrated)
            {
                _translationDrift = translation;
                _rotationDrift = rotation;
                _driftCalibrated = true;
                if (_debugMode)
                    Debug.Log($"ImprovedSpaceNavigatorController: Drift calibrated - T:{_translationDrift}, R:{_rotationDrift}");
            }

            // Apply drift compensation
            translation -= _translationDrift;
            rotation -= _rotationDrift;

            // Apply deadzone
            if (IsApproximatelyZero(translation, 0.005f) && IsApproximatelyZero(rotation, 0.005f))
            {
                return;
            }

            // Apply input smoothing for better feel
            _smoothedTranslation = Vector3.Lerp(_smoothedTranslation, translation, _smoothingFactor);
            _smoothedRotation = Vector3.Lerp(_smoothedRotation, rotation, _smoothingFactor);

            // Apply sensitivity and frame rate independence
            Vector3 finalTranslation = _smoothedTranslation * _translationSensitivity * deltaTime;
            Vector3 finalRotation = _smoothedRotation * _rotationSensitivity * deltaTime;

            if (_debugMode)
            {
                Debug.Log($"ImprovedSpaceNavigatorController: T:{finalTranslation}, R:{finalRotation}");
            }

            // Apply camera movement with proper 3D navigation behavior
            ApplyImprovedCameraMovement(sceneView, finalTranslation, finalRotation);
        }

        private static void ApplyImprovedCameraMovement(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            // Initialize pivot if needed
            if (!_pivotInitialized)
            {
                _lastPivotPosition = sceneView.pivot;
                _pivotInitialized = true;
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
            
            // Forward/Backward (Z-axis) - this is ZOOM, not translation
            // We'll handle zoom separately to avoid camera clipping issues
            
            // Update pivot position for panning
            Vector3 newPivotPosition = pivotPosition + worldTranslation;

            // === ZOOM (Dolly) ===
            // Handle forward/backward as camera dolly (moving camera closer/farther from pivot)
            float zoomInput = translation.z * _zoomSensitivity;
            
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
            
            if (rotation.magnitude > 0.001f)
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
                float sizeChange = translation.z * _zoomSensitivity * 0.1f;
                sceneView.size = Mathf.Max(0.01f, sceneView.size - sizeChange);
            }
            else
            {
                // In perspective mode, the camera position is automatically calculated from pivot and rotation
                // Unity's SceneView handles this internally
            }

            // Update our tracking variables
            _lastPivotPosition = newPivotPosition;
            
            // Repaint the scene view
            sceneView.Repaint();
        }

        private static bool IsApproximatelyZero(Vector3 vector, float epsilon)
        {
            return vector.magnitude < epsilon;
        }

        // Public methods for configuration
        [MenuItem("Window/SpaceNavigator/Improved Controller/Recalibrate Drift")]
        public static void RecalibrateDrift()
        {
            if (SpaceNavigatorHID.current != null)
            {
                _translationDrift = SpaceNavigatorHID.current.Translation.ReadValue();
                _rotationDrift = SpaceNavigatorHID.current.Rotation.ReadValue();
                _driftCalibrated = true;
                Debug.Log($"ImprovedSpaceNavigatorController: Drift recalibrated - T:{_translationDrift}, R:{_rotationDrift}");
            }
            else
            {
                Debug.LogWarning("ImprovedSpaceNavigatorController: No SpaceNavigator device connected");
            }
        }

        [MenuItem("Window/SpaceNavigator/Improved Controller/Toggle Debug Mode")]
        public static void ToggleDebugMode()
        {
            _debugMode = !_debugMode;
            Debug.Log($"ImprovedSpaceNavigatorController: Debug mode {(_debugMode ? "enabled" : "disabled")}");
        }

        [MenuItem("Window/SpaceNavigator/Improved Controller/Reset Sensitivity")]
        public static void ResetSensitivity()
        {
            _translationSensitivity = 2.0f;
            _rotationSensitivity = 1.5f;
            _zoomSensitivity = 3.0f;
            Debug.Log("ImprovedSpaceNavigatorController: Sensitivity reset to defaults");
        }

        // Properties for runtime adjustment
        public static float TranslationSensitivity
        {
            get => _translationSensitivity;
            set => _translationSensitivity = Mathf.Clamp(value, 0.1f, 10.0f);
        }

        public static float RotationSensitivity
        {
            get => _rotationSensitivity;
            set => _rotationSensitivity = Mathf.Clamp(value, 0.1f, 10.0f);
        }

        public static float ZoomSensitivity
        {
            get => _zoomSensitivity;
            set => _zoomSensitivity = Mathf.Clamp(value, 0.1f, 10.0f);
        }
    }
}
#endif