#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

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
        
        // カーソルロック用（macOS対応）
        private static Vector2 _lastCursorPosition;
        private static bool _wasIdle = true;
        
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
                _wasIdle = true;
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
            
            // Lock cursor on macOS to prevent mouse movement
            CursorLock();
            _wasIdle = false;
        }

        private static void ApplyCameraMovement(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: Applying 6-axis camera movement - Translation: {translation}, Rotation: {rotation}");
            }

            // Get current camera transform
            Transform cameraTransform = sceneView.camera.transform;
            Vector3 currentPosition = cameraTransform.position;
            Quaternion currentRotation = sceneView.rotation;

            // Calculate movement speed based on camera distance from objects
            float movementSpeed = Mathf.Max(0.1f, Vector3.Distance(currentPosition, sceneView.pivot) * 0.1f);

            // === 平行移動（3軸）===
            Vector3 newPosition = currentPosition;
            
            // 左右（Pan）- カメラのローカル右方向に移動
            newPosition += currentRotation * Vector3.right * translation.x * movementSpeed;
            
            // 上下（Elevate）- カメラのローカル上方向に移動
            newPosition += currentRotation * Vector3.up * translation.y * movementSpeed;
            
            // 前後（Zoom/Dolly）- カメラのローカル前方向に移動
            newPosition += currentRotation * Vector3.forward * translation.z * movementSpeed;

            // === 回転（3軸）===
            Quaternion newRotation = currentRotation;
            
            if (!sceneView.orthographic && rotation.magnitude > 0.001f)
            {
                // 回転感度の調整
                float rotationSensitivity = 57.2958f; // ラジアンから度への変換
                
                // ピッチ（上下に回転）- カメラのローカル右軸周りで回転
                if (Mathf.Abs(rotation.x) > 0.0001f)
                {
                    Quaternion pitchRotation = Quaternion.AngleAxis(-rotation.x * rotationSensitivity, currentRotation * Vector3.right);
                    newRotation = pitchRotation * newRotation;
                }
                
                // ヨー（左右に回転）- 世界のY軸周りで回転（水平方向を旋回）
                if (Mathf.Abs(rotation.y) > 0.0001f)
                {
                    Quaternion yawRotation = Quaternion.AngleAxis(rotation.y * rotationSensitivity, Vector3.up);
                    newRotation = yawRotation * newRotation;
                }
                
                // ロール（軸回転）- カメラのローカル前方軸周りで回転
                if (Mathf.Abs(rotation.z) > 0.0001f)
                {
                    Quaternion rollRotation = Quaternion.AngleAxis(rotation.z * rotationSensitivity, currentRotation * Vector3.forward);
                    newRotation = rollRotation * newRotation;
                }

                if (_debugMode)
                {
                    Debug.Log($"SimpleSceneViewController: Applied rotations - Pitch: {-rotation.x * rotationSensitivity:F2}°, Yaw: {rotation.y * rotationSensitivity:F2}°, Roll: {rotation.z * rotationSensitivity:F2}°");
                }
            }

            // 新しいピボット位置を計算（カメラの移動に合わせて）
            Vector3 newPivotPosition = newPosition + (newRotation * Vector3.forward) * Vector3.Distance(currentPosition, sceneView.pivot);

            // SceneViewに適用
            sceneView.pivot = newPivotPosition;
            sceneView.rotation = newRotation;
            
            // Orthographicモードでの特別な処理
            if (sceneView.orthographic)
            {
                // Orthographicモードでは、前後移動をサイズ変更として扱う
                float sizeChange = translation.z * movementSpeed * 0.1f;
                sceneView.size = Mathf.Max(0.01f, sceneView.size - sizeChange);
            }

            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: Camera moved to position: {newPosition}, rotation: {newRotation.eulerAngles}");
            }

            // SceneViewを更新
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
        
        /// <summary>
        /// On MacOS 3dconnexion pitch & roll input always moves the mouse pointer.
        /// This method locks the pointer in place while input is being received.
        /// </summary>
        private static void CursorLock()
        {
            if (Application.platform != RuntimePlatform.OSXEditor) return;
            
            if (_wasIdle)
            {
                // デバイスがアイドル状態から動き始めた時、現在のカーソル位置を記録
                if (Mouse.current != null)
                {
                    _lastCursorPosition = Mouse.current.position.ReadValue();
                    if (_debugMode)
                    {
                        Debug.Log($"SimpleSceneViewController: Cursor position saved: {_lastCursorPosition}");
                    }
                }
            }
            else
            {
                // デバイスが動いている間、カーソルを元の位置に戻す
                if (EditorApplication.isFocused && Mouse.current != null)
                {
                    Mouse.current.WarpCursorPosition(_lastCursorPosition);
                    if (_debugMode)
                    {
                        Debug.Log($"SimpleSceneViewController: Cursor position restored to: {_lastCursorPosition}");
                    }
                }
            }
        }
    }
}
#endif