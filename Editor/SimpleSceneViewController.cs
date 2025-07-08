#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceNavigatorDriver
{
    public enum NavigationMode
    {
        Object,      // Object mode - camera moves around selected object
        Camera,      // Camera mode - traditional FPS camera controls
        TargetCamera, // Target camera mode - orbit around view pivot
        Fly,         // Fly mode - helicopter-like movement
        Helicopter,  // Helicopter mode - keep altitude, no roll
        Walk,        // Walk mode - ground-clamped movement
        Drone,       // Drone mode - free 6DoF movement with hover
        Orbit,       // Legacy orbit mode
        Telekinesis, // Move objects with SpaceMouse
        GrabMove     // Grab and move objects
    }

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
        
        // ナビゲーションモード
        private static NavigationMode _currentMode = NavigationMode.Fly;
        
        // Lock states
        private static bool _horizonLock = false;
        private static bool _rotationLock = false;
        
        // Movement coordinate system for MoveObjects mode
        public enum CoordinateSystem
        {
            Local,
            Parent,
            World,
            Camera
        }
        
        private static CoordinateSystem _coordinateSystem = CoordinateSystem.World;
        
        // オブジェクト操作用
        private static Dictionary<Transform, Quaternion> _unsnappedRotations = new Dictionary<Transform, Quaternion>();
        private static Dictionary<Transform, Vector3> _unsnappedTranslations = new Dictionary<Transform, Vector3>();
        
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

            // Apply movement based on current mode
            switch (_currentMode)
            {
                case NavigationMode.Object:
                    ObjectMode(sceneView, translation, rotation);
                    break;
                case NavigationMode.Camera:
                    CameraMode(sceneView, translation, rotation);
                    break;
                case NavigationMode.TargetCamera:
                    TargetCameraMode(sceneView, translation, rotation);
                    break;
                case NavigationMode.Fly:
                    FlyMode(sceneView, translation, rotation);
                    break;
                case NavigationMode.Helicopter:
                    HelicopterMode(sceneView, translation, rotation);
                    break;
                case NavigationMode.Walk:
                    WalkMode(sceneView, translation, rotation);
                    break;
                case NavigationMode.Drone:
                    DroneMode(sceneView, translation, rotation);
                    break;
                case NavigationMode.Orbit:
                    OrbitMode(sceneView, translation, rotation);
                    break;
                case NavigationMode.Telekinesis:
                    TelekinesisMode(sceneView, translation, rotation);
                    break;
                case NavigationMode.GrabMove:
                    GrabMoveMode(sceneView, translation, rotation);
                    break;
            }
            
            // Lock cursor on macOS to prevent mouse movement
            CursorLock();
            _wasIdle = false;
        }

        private static void ObjectMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            // Object mode - camera moves around selected object, similar to Orbit but with different feel
            if (Selection.gameObjects.Length == 0)
            {
                // No selection, fall back to camera mode
                CameraMode(sceneView, translation, rotation);
                return;
            }
            
            Vector3 selectionCenter = GetSelectionCenter();
            Vector3 cameraPosition = sceneView.camera.transform.position;
            
            // Calculate movement speed based on distance to selection
            float movementSpeed = Mathf.Max(0.1f, Vector3.Distance(cameraPosition, selectionCenter) * 0.1f);
            
            // Apply translation relative to selection center
            Vector3 newPosition = cameraPosition;
            if (!_rotationLock)
            {
                newPosition += sceneView.rotation * Vector3.right * translation.x * movementSpeed;
                newPosition += sceneView.rotation * Vector3.up * translation.y * movementSpeed;
                newPosition += sceneView.rotation * Vector3.forward * translation.z * movementSpeed;
            }
            
            // Apply rotation around selection center
            if (!_rotationLock && rotation.magnitude > 0.001f)
            {
                float rotationSensitivity = 57.2958f;
                
                // Orbit around selection
                if (Mathf.Abs(rotation.y) > 0.0001f)
                {
                    Vector3 upVector = _horizonLock ? Vector3.up : sceneView.rotation * Vector3.up;
                    newPosition = RotatePointAroundPivot(newPosition, selectionCenter, upVector, rotation.y * rotationSensitivity);
                }
                
                if (Mathf.Abs(rotation.x) > 0.0001f)
                {
                    Vector3 rightVector = Vector3.Cross(Vector3.up, (newPosition - selectionCenter).normalized);
                    newPosition = RotatePointAroundPivot(newPosition, selectionCenter, rightVector, -rotation.x * rotationSensitivity);
                }
            }
            
            // Look at selection
            Vector3 lookDirection = (selectionCenter - newPosition).normalized;
            Quaternion newRotation = Quaternion.LookRotation(lookDirection, _horizonLock ? Vector3.up : sceneView.rotation * Vector3.up);
            
            sceneView.pivot = selectionCenter;
            sceneView.rotation = newRotation;
            sceneView.Repaint();
        }
        
        private static void CameraMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            // Traditional FPS camera controls
            Transform cameraTransform = sceneView.camera.transform;
            Vector3 currentPosition = cameraTransform.position;
            Quaternion currentRotation = sceneView.rotation;
            
            float movementSpeed = Mathf.Max(0.1f, Vector3.Distance(currentPosition, sceneView.pivot) * 0.1f);
            
            // Apply translation
            Vector3 newPosition = currentPosition;
            newPosition += currentRotation * Vector3.right * translation.x * movementSpeed;
            newPosition += currentRotation * Vector3.up * translation.y * movementSpeed;
            newPosition += currentRotation * Vector3.forward * translation.z * movementSpeed;
            
            // Apply rotation
            Quaternion newRotation = currentRotation;
            if (!_rotationLock && rotation.magnitude > 0.001f)
            {
                float rotationSensitivity = 57.2958f;
                
                // Pitch (up/down)
                if (Mathf.Abs(rotation.x) > 0.0001f)
                {
                    Vector3 rightVector = _horizonLock ? Vector3.Cross(Vector3.up, newRotation * Vector3.forward).normalized : newRotation * Vector3.right;
                    Quaternion pitchRotation = Quaternion.AngleAxis(-rotation.x * rotationSensitivity, rightVector);
                    newRotation = pitchRotation * newRotation;
                }
                
                // Yaw (left/right)
                if (Mathf.Abs(rotation.y) > 0.0001f)
                {
                    Vector3 upVector = _horizonLock ? Vector3.up : newRotation * Vector3.up;
                    Quaternion yawRotation = Quaternion.AngleAxis(rotation.y * rotationSensitivity, upVector);
                    newRotation = yawRotation * newRotation;
                }
                
                // Roll (only if horizon lock is disabled)
                if (!_horizonLock && Mathf.Abs(rotation.z) > 0.0001f)
                {
                    Quaternion rollRotation = Quaternion.AngleAxis(rotation.z * rotationSensitivity, newRotation * Vector3.forward);
                    newRotation = rollRotation * newRotation;
                }
            }
            
            // Update pivot to maintain camera movement
            Vector3 newPivot = newPosition + (newRotation * Vector3.forward) * Vector3.Distance(currentPosition, sceneView.pivot);
            
            sceneView.pivot = newPivot;
            sceneView.rotation = newRotation;
            sceneView.Repaint();
        }
        
        private static void TargetCameraMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            // Orbit around current view pivot
            Vector3 cameraPosition = sceneView.camera.transform.position;
            Vector3 pivotPosition = sceneView.pivot;
            
            float movementSpeed = Mathf.Max(0.1f, Vector3.Distance(cameraPosition, pivotPosition) * 0.1f);
            
            // Apply translation to move closer/farther from pivot
            Vector3 newPosition = cameraPosition;
            Vector3 toPivot = (pivotPosition - cameraPosition).normalized;
            newPosition += toPivot * translation.z * movementSpeed;
            
            // Pan around pivot
            newPosition += sceneView.rotation * Vector3.right * translation.x * movementSpeed;
            newPosition += sceneView.rotation * Vector3.up * translation.y * movementSpeed;
            
            // Apply rotation around pivot
            if (!_rotationLock && rotation.magnitude > 0.001f)
            {
                float rotationSensitivity = 57.2958f;
                
                if (Mathf.Abs(rotation.y) > 0.0001f)
                {
                    Vector3 upVector = _horizonLock ? Vector3.up : sceneView.rotation * Vector3.up;
                    newPosition = RotatePointAroundPivot(newPosition, pivotPosition, upVector, rotation.y * rotationSensitivity);
                }
                
                if (Mathf.Abs(rotation.x) > 0.0001f)
                {
                    Vector3 rightVector = Vector3.Cross(Vector3.up, (newPosition - pivotPosition).normalized);
                    newPosition = RotatePointAroundPivot(newPosition, pivotPosition, rightVector, -rotation.x * rotationSensitivity);
                }
            }
            
            // Look at pivot
            Vector3 lookDirection = (pivotPosition - newPosition).normalized;
            Quaternion newRotation = Quaternion.LookRotation(lookDirection, _horizonLock ? Vector3.up : sceneView.rotation * Vector3.up);
            
            sceneView.rotation = newRotation;
            sceneView.Repaint();
        }
        
        private static void HelicopterMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            // Helicopter mode - keep altitude, no roll
            Transform cameraTransform = sceneView.camera.transform;
            Vector3 currentPosition = cameraTransform.position;
            Quaternion currentRotation = sceneView.rotation;
            
            float movementSpeed = Mathf.Max(0.1f, Vector3.Distance(currentPosition, sceneView.pivot) * 0.1f);
            
            // Apply translation with altitude lock
            Vector3 newPosition = currentPosition;
            newPosition += currentRotation * Vector3.right * translation.x * movementSpeed;
            newPosition += currentRotation * Vector3.forward * translation.z * movementSpeed;
            
            // Vertical movement only with explicit Y input
            if (Mathf.Abs(translation.y) > 0.001f)
            {
                newPosition += Vector3.up * translation.y * movementSpeed;
            }
            
            // Apply rotation with horizon lock enforced
            Quaternion newRotation = currentRotation;
            if (!_rotationLock && rotation.magnitude > 0.001f)
            {
                float rotationSensitivity = 57.2958f;
                
                // Only yaw allowed (left/right rotation)
                if (Mathf.Abs(rotation.y) > 0.0001f)
                {
                    Quaternion yawRotation = Quaternion.AngleAxis(rotation.y * rotationSensitivity, Vector3.up);
                    newRotation = yawRotation * newRotation;
                }
                
                // Pitch with limited range
                if (Mathf.Abs(rotation.x) > 0.0001f)
                {
                    Vector3 rightVector = Vector3.Cross(Vector3.up, newRotation * Vector3.forward).normalized;
                    Quaternion pitchRotation = Quaternion.AngleAxis(-rotation.x * rotationSensitivity, rightVector);
                    newRotation = pitchRotation * newRotation;
                    
                    // Clamp pitch to prevent flipping
                    Vector3 euler = newRotation.eulerAngles;
                    euler.x = Mathf.Clamp(euler.x > 180 ? euler.x - 360 : euler.x, -80, 80);
                    newRotation = Quaternion.Euler(euler.x, euler.y, 0); // Force roll to 0
                }
            }
            
            Vector3 newPivot = newPosition + (newRotation * Vector3.forward) * Vector3.Distance(currentPosition, sceneView.pivot);
            
            sceneView.pivot = newPivot;
            sceneView.rotation = newRotation;
            sceneView.Repaint();
        }
        
        private static void WalkMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            // Walk mode - ground-clamped movement
            Transform cameraTransform = sceneView.camera.transform;
            Vector3 currentPosition = cameraTransform.position;
            Quaternion currentRotation = sceneView.rotation;
            
            float movementSpeed = Mathf.Max(0.1f, Vector3.Distance(currentPosition, sceneView.pivot) * 0.1f);
            
            // Apply horizontal translation only
            Vector3 newPosition = currentPosition;
            Vector3 forward = Vector3.ProjectOnPlane(currentRotation * Vector3.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            
            newPosition += right * translation.x * movementSpeed;
            newPosition += forward * translation.z * movementSpeed;
            
            // Keep Y position or clamp to ground
            // For now, just maintain current height, but could raycast to ground
            float groundHeight = 0f; // Could be replaced with terrain/ground detection
            newPosition.y = Mathf.Max(groundHeight + 1.8f, newPosition.y); // Assume 1.8m eye height
            
            // Apply rotation with horizon lock enforced
            Quaternion newRotation = currentRotation;
            if (!_rotationLock && rotation.magnitude > 0.001f)
            {
                float rotationSensitivity = 57.2958f;
                
                // Only yaw allowed (left/right rotation)
                if (Mathf.Abs(rotation.y) > 0.0001f)
                {
                    Quaternion yawRotation = Quaternion.AngleAxis(rotation.y * rotationSensitivity, Vector3.up);
                    newRotation = yawRotation * newRotation;
                }
                
                // Limited pitch
                if (Mathf.Abs(rotation.x) > 0.0001f)
                {
                    Vector3 rightVector = Vector3.Cross(Vector3.up, newRotation * Vector3.forward).normalized;
                    Quaternion pitchRotation = Quaternion.AngleAxis(-rotation.x * rotationSensitivity, rightVector);
                    newRotation = pitchRotation * newRotation;
                    
                    // Clamp pitch for walking
                    Vector3 euler = newRotation.eulerAngles;
                    euler.x = Mathf.Clamp(euler.x > 180 ? euler.x - 360 : euler.x, -60, 60);
                    newRotation = Quaternion.Euler(euler.x, euler.y, 0); // Force roll to 0
                }
            }
            
            Vector3 newPivot = newPosition + (newRotation * Vector3.forward) * Vector3.Distance(currentPosition, sceneView.pivot);
            
            sceneView.pivot = newPivot;
            sceneView.rotation = newRotation;
            sceneView.Repaint();
        }
        
        private static void DroneMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            // Drone mode - free 6DoF movement with hover capability
            Transform cameraTransform = sceneView.camera.transform;
            Vector3 currentPosition = cameraTransform.position;
            Quaternion currentRotation = sceneView.rotation;
            
            float movementSpeed = Mathf.Max(0.1f, Vector3.Distance(currentPosition, sceneView.pivot) * 0.1f);
            
            // Apply full 6DoF translation
            Vector3 newPosition = currentPosition;
            newPosition += currentRotation * Vector3.right * translation.x * movementSpeed;
            newPosition += currentRotation * Vector3.up * translation.y * movementSpeed;
            newPosition += currentRotation * Vector3.forward * translation.z * movementSpeed;
            
            // Apply full 6DoF rotation
            Quaternion newRotation = currentRotation;
            if (!_rotationLock && rotation.magnitude > 0.001f)
            {
                float rotationSensitivity = 57.2958f;
                
                // Pitch
                if (Mathf.Abs(rotation.x) > 0.0001f)
                {
                    Vector3 rightVector = _horizonLock ? Vector3.Cross(Vector3.up, newRotation * Vector3.forward).normalized : newRotation * Vector3.right;
                    Quaternion pitchRotation = Quaternion.AngleAxis(-rotation.x * rotationSensitivity, rightVector);
                    newRotation = pitchRotation * newRotation;
                }
                
                // Yaw
                if (Mathf.Abs(rotation.y) > 0.0001f)
                {
                    Vector3 upVector = _horizonLock ? Vector3.up : newRotation * Vector3.up;
                    Quaternion yawRotation = Quaternion.AngleAxis(rotation.y * rotationSensitivity, upVector);
                    newRotation = yawRotation * newRotation;
                }
                
                // Roll (only if horizon lock is disabled)
                if (!_horizonLock && Mathf.Abs(rotation.z) > 0.0001f)
                {
                    Quaternion rollRotation = Quaternion.AngleAxis(rotation.z * rotationSensitivity, newRotation * Vector3.forward);
                    newRotation = rollRotation * newRotation;
                }
            }
            
            Vector3 newPivot = newPosition + (newRotation * Vector3.forward) * Vector3.Distance(currentPosition, sceneView.pivot);
            
            sceneView.pivot = newPivot;
            sceneView.rotation = newRotation;
            sceneView.Repaint();
        }

        private static void FlyMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: Fly Mode - Translation: {translation}, Rotation: {rotation}");
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

        private static void OrbitMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: Orbit Mode - Translation: {translation}, Rotation: {rotation}");
            }

            // If no object is selected, fall back to fly mode
            if (Selection.gameObjects.Length == 0)
            {
                FlyMode(sceneView, translation, rotation);
                return;
            }

            // Get current camera state
            Vector3 cameraPosition = sceneView.camera.transform.position;
            Vector3 pivotPosition = sceneView.pivot;
            Quaternion cameraRotation = sceneView.rotation;

            // Calculate movement speed based on distance to pivot
            float movementSpeed = Mathf.Max(0.1f, Vector3.Distance(cameraPosition, pivotPosition) * 0.1f);

            // === TRANSLATION (move camera in local space) ===
            Vector3 newPosition = cameraPosition;
            newPosition += cameraRotation * Vector3.right * translation.x * movementSpeed;
            newPosition += cameraRotation * Vector3.up * translation.y * movementSpeed;
            newPosition += cameraRotation * Vector3.forward * translation.z * movementSpeed;

            // === ROTATION (orbit around selection) ===
            Vector3 orbitCenter = GetSelectionCenter();
            Vector3 newCameraPosition = newPosition;

            if (!sceneView.orthographic && rotation.magnitude > 0.001f)
            {
                float rotationSensitivity = 57.2958f;

                // Orbit around selection center
                if (Mathf.Abs(rotation.y) > 0.0001f) // Yaw
                {
                    newCameraPosition = RotatePointAroundPivot(newCameraPosition, orbitCenter, Vector3.up, rotation.y * rotationSensitivity);
                }
                
                if (Mathf.Abs(rotation.x) > 0.0001f) // Pitch
                {
                    Vector3 rightVector = Vector3.Cross(Vector3.up, (newCameraPosition - orbitCenter).normalized);
                    newCameraPosition = RotatePointAroundPivot(newCameraPosition, orbitCenter, rightVector, -rotation.x * rotationSensitivity);
                }
                
                if (Mathf.Abs(rotation.z) > 0.0001f) // Roll
                {
                    Vector3 forwardVector = (newCameraPosition - orbitCenter).normalized;
                    newCameraPosition = RotatePointAroundPivot(newCameraPosition, orbitCenter, forwardVector, rotation.z * rotationSensitivity);
                }
            }

            // Update camera to look at orbit center
            Vector3 lookDirection = (orbitCenter - newCameraPosition).normalized;
            Quaternion newRotation = Quaternion.LookRotation(lookDirection, Vector3.up);

            // Apply to SceneView
            sceneView.pivot = orbitCenter;
            sceneView.rotation = newRotation;

            // Handle orthographic mode
            if (sceneView.orthographic)
            {
                float sizeChange = translation.z * movementSpeed * 0.1f;
                sceneView.size = Mathf.Max(0.01f, sceneView.size - sizeChange);
            }

            sceneView.Repaint();
        }

        private static void TelekinesisMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: Telekinesis Mode - Translation: {translation}, Rotation: {rotation}");
            }

            if (_wasIdle)
                Undo.IncrementCurrentGroup();

            Transform[] selection = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);
            if (selection.Length == 0) 
            {
                // No objects selected, switch to camera movement
                CameraMode(sceneView, translation, rotation);
                return;
            }

            Undo.SetCurrentGroupName("MoveObjects");
            Undo.RecordObjects(selection, "MoveObjects");

            // Store selection transforms if we were idle
            if (_wasIdle)
                StoreSelectionTransforms();

            foreach (Transform transform in selection)
            {
                if (!_unsnappedRotations.ContainsKey(transform)) continue;

                // Apply translation based on coordinate system
                Vector3 worldTranslation = GetWorldTranslation(transform, translation, sceneView);
                _unsnappedTranslations[transform] += worldTranslation;

                // Apply rotation based on coordinate system
                if (!_rotationLock && rotation.magnitude > 0.001f)
                {
                    Quaternion rotationDelta = GetWorldRotation(transform, rotation, sceneView);
                    _unsnappedRotations[transform] = rotationDelta * _unsnappedRotations[transform];
                }

                // Apply to transform
                transform.position = _unsnappedTranslations[transform];
                transform.rotation = _unsnappedRotations[transform];
            }
        }
        
        private static Vector3 GetWorldTranslation(Transform transform, Vector3 translation, SceneView sceneView)
        {
            switch (_coordinateSystem)
            {
                case CoordinateSystem.Local:
                    return transform.TransformVector(translation);
                case CoordinateSystem.Parent:
                    return transform.parent != null ? transform.parent.TransformVector(translation) : translation;
                case CoordinateSystem.World:
                    return translation;
                case CoordinateSystem.Camera:
                    return sceneView.camera.transform.TransformVector(translation);
                default:
                    return translation;
            }
        }
        
        private static Quaternion GetWorldRotation(Transform transform, Vector3 rotation, SceneView sceneView)
        {
            Quaternion rotationDelta = Quaternion.Euler(rotation * 57.2958f);
            
            switch (_coordinateSystem)
            {
                case CoordinateSystem.Local:
                    return rotationDelta;
                case CoordinateSystem.Parent:
                    return transform.parent != null ? transform.parent.rotation * rotationDelta * Quaternion.Inverse(transform.parent.rotation) : rotationDelta;
                case CoordinateSystem.World:
                    return rotationDelta;
                case CoordinateSystem.Camera:
                    return sceneView.rotation * rotationDelta * Quaternion.Inverse(sceneView.rotation);
                default:
                    return rotationDelta;
            }
        }

        private static void GrabMoveMode(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
            if (_debugMode)
            {
                Debug.Log($"SimpleSceneViewController: GrabMove Mode - Translation: {translation}, Rotation: {rotation}");
            }

            if (_wasIdle)
                Undo.IncrementCurrentGroup();

            Transform[] selection = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);
            if (selection.Length == 0)
            {
                // No selection, just move camera
                FlyMode(sceneView, translation, rotation);
                return;
            }

            Undo.SetCurrentGroupName("GrabMove");
            Undo.RecordObjects(selection, "GrabMove");

            // Store selection transforms if we were idle
            if (_wasIdle)
                StoreSelectionTransforms();

            Vector3 cameraPosition = sceneView.camera.transform.position;

            foreach (Transform transform in selection)
            {
                if (!_unsnappedRotations.ContainsKey(transform)) continue;

                // Initialize transform to unsnapped state
                transform.rotation = _unsnappedRotations[transform];
                transform.position = _unsnappedTranslations[transform];
                Vector3 oldPos = transform.position;

                // Rotate around camera position
                transform.RotateAround(cameraPosition, Vector3.up, rotation.y * 57.2958f);
                transform.RotateAround(cameraPosition, sceneView.camera.transform.right, rotation.x * 57.2958f);

                // Move in camera space
                Vector3 worldTranslation = sceneView.camera.transform.TransformPoint(translation) - sceneView.camera.transform.position;
                transform.position += worldTranslation;

                // Store new unsnapped state
                _unsnappedRotations[transform] = transform.rotation;
                _unsnappedTranslations[transform] += transform.position - oldPos;
            }

            // Also move the camera
            FlyMode(sceneView, translation, rotation);
        }

        private static Vector3 GetSelectionCenter()
        {
            if (Selection.gameObjects.Length == 0)
                return Vector3.zero;

            Vector3 center = Vector3.zero;
            foreach (GameObject obj in Selection.gameObjects)
            {
                center += obj.transform.position;
            }
            return center / Selection.gameObjects.Length;
        }

        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 axis, float angle)
        {
            return Quaternion.AngleAxis(angle, axis) * (point - pivot) + pivot;
        }

        private static void StoreSelectionTransforms()
        {
            _unsnappedRotations.Clear();
            _unsnappedTranslations.Clear();
            foreach (Transform transform in Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable))
            {
                _unsnappedRotations.Add(transform, transform.rotation);
                _unsnappedTranslations.Add(transform, transform.position);
            }
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

        // Navigation Mode Menu Items
        [MenuItem("Window/SpaceNavigator/Navigation Modes/Object Mode")]
        public static void SetObjectMode()
        {
            _currentMode = NavigationMode.Object;
            Debug.Log("SimpleSceneViewController: Switched to Object Mode");
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Camera Mode")]
        public static void SetCameraMode()
        {
            _currentMode = NavigationMode.Camera;
            Debug.Log("SimpleSceneViewController: Switched to Camera Mode");
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Target Camera Mode")]
        public static void SetTargetCameraMode()
        {
            _currentMode = NavigationMode.TargetCamera;
            Debug.Log("SimpleSceneViewController: Switched to Target Camera Mode");
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Fly Mode")]
        public static void SetFlyMode()
        {
            _currentMode = NavigationMode.Fly;
            Debug.Log("SimpleSceneViewController: Switched to Fly Mode");
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Helicopter Mode")]
        public static void SetHelicopterMode()
        {
            _currentMode = NavigationMode.Helicopter;
            Debug.Log("SimpleSceneViewController: Switched to Helicopter Mode");
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Walk Mode")]
        public static void SetWalkMode()
        {
            _currentMode = NavigationMode.Walk;
            Debug.Log("SimpleSceneViewController: Switched to Walk Mode");
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Drone Mode")]
        public static void SetDroneMode()
        {
            _currentMode = NavigationMode.Drone;
            Debug.Log("SimpleSceneViewController: Switched to Drone Mode");
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Orbit Mode")]
        public static void SetOrbitMode()
        {
            _currentMode = NavigationMode.Orbit;
            Debug.Log("SimpleSceneViewController: Switched to Orbit Mode");
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/MoveObjects Mode")]
        public static void SetTelekinesisMode()
        {
            _currentMode = NavigationMode.Telekinesis;
            StoreSelectionTransforms(); // Store current selection
            Debug.Log("SimpleSceneViewController: Switched to MoveObjects Mode");
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/GrabMove Mode")]
        public static void SetGrabMoveMode()
        {
            _currentMode = NavigationMode.GrabMove;
            StoreSelectionTransforms(); // Store current selection
            Debug.Log("SimpleSceneViewController: Switched to GrabMove Mode");
        }

        // Navigation Mode Validation Functions
        [MenuItem("Window/SpaceNavigator/Navigation Modes/Object Mode", true)]
        public static bool SetObjectModeValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/Object Mode", _currentMode == NavigationMode.Object);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Camera Mode", true)]
        public static bool SetCameraModeValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/Camera Mode", _currentMode == NavigationMode.Camera);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Target Camera Mode", true)]
        public static bool SetTargetCameraModeValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/Target Camera Mode", _currentMode == NavigationMode.TargetCamera);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Fly Mode", true)]
        public static bool SetFlyModeValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/Fly Mode", _currentMode == NavigationMode.Fly);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Helicopter Mode", true)]
        public static bool SetHelicopterModeValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/Helicopter Mode", _currentMode == NavigationMode.Helicopter);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Walk Mode", true)]
        public static bool SetWalkModeValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/Walk Mode", _currentMode == NavigationMode.Walk);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Drone Mode", true)]
        public static bool SetDroneModeValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/Drone Mode", _currentMode == NavigationMode.Drone);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/Orbit Mode", true)]
        public static bool SetOrbitModeValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/Orbit Mode", _currentMode == NavigationMode.Orbit);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/MoveObjects Mode", true)]
        public static bool SetTelekinesisValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/MoveObjects Mode", _currentMode == NavigationMode.Telekinesis);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Navigation Modes/GrabMove Mode", true)]
        public static bool SetGrabMoveValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Navigation Modes/GrabMove Mode", _currentMode == NavigationMode.GrabMove);
            return true;
        }

        // Lock Toggle Functions
        [MenuItem("Window/SpaceNavigator/Locks/Toggle Horizon Lock")]
        public static void ToggleHorizonLock()
        {
            _horizonLock = !_horizonLock;
            Debug.Log($"SimpleSceneViewController: Horizon Lock {(_horizonLock ? "enabled" : "disabled")}");
        }

        [MenuItem("Window/SpaceNavigator/Locks/Toggle Horizon Lock", true)]
        public static bool ToggleHorizonLockValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Locks/Toggle Horizon Lock", _horizonLock);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Locks/Toggle Rotation Lock")]
        public static void ToggleRotationLock()
        {
            _rotationLock = !_rotationLock;
            Debug.Log($"SimpleSceneViewController: Rotation Lock {(_rotationLock ? "enabled" : "disabled")}");
        }

        [MenuItem("Window/SpaceNavigator/Locks/Toggle Rotation Lock", true)]
        public static bool ToggleRotationLockValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Locks/Toggle Rotation Lock", _rotationLock);
            return true;
        }

        // Coordinate System Functions
        [MenuItem("Window/SpaceNavigator/Coordinate System/Local")]
        public static void SetLocalCoordinateSystem()
        {
            _coordinateSystem = CoordinateSystem.Local;
            Debug.Log("SimpleSceneViewController: Coordinate System set to Local");
        }

        [MenuItem("Window/SpaceNavigator/Coordinate System/Parent")]
        public static void SetParentCoordinateSystem()
        {
            _coordinateSystem = CoordinateSystem.Parent;
            Debug.Log("SimpleSceneViewController: Coordinate System set to Parent");
        }

        [MenuItem("Window/SpaceNavigator/Coordinate System/World")]
        public static void SetWorldCoordinateSystem()
        {
            _coordinateSystem = CoordinateSystem.World;
            Debug.Log("SimpleSceneViewController: Coordinate System set to World");
        }

        [MenuItem("Window/SpaceNavigator/Coordinate System/Camera")]
        public static void SetCameraCoordinateSystem()
        {
            _coordinateSystem = CoordinateSystem.Camera;
            Debug.Log("SimpleSceneViewController: Coordinate System set to Camera");
        }

        [MenuItem("Window/SpaceNavigator/Coordinate System/Local", true)]
        public static bool SetLocalCoordinateSystemValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Coordinate System/Local", _coordinateSystem == CoordinateSystem.Local);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Coordinate System/Parent", true)]
        public static bool SetParentCoordinateSystemValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Coordinate System/Parent", _coordinateSystem == CoordinateSystem.Parent);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Coordinate System/World", true)]
        public static bool SetWorldCoordinateSystemValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Coordinate System/World", _coordinateSystem == CoordinateSystem.World);
            return true;
        }

        [MenuItem("Window/SpaceNavigator/Coordinate System/Camera", true)]
        public static bool SetCameraCoordinateSystemValidate()
        {
            Menu.SetChecked("Window/SpaceNavigator/Coordinate System/Camera", _coordinateSystem == CoordinateSystem.Camera);
            return true;
        }

        // Public Properties for UI access
        public static bool HorizonLock { get => _horizonLock; set => _horizonLock = value; }
        public static bool RotationLock { get => _rotationLock; set => _rotationLock = value; }
        public static NavigationMode CurrentMode { get => _currentMode; set => _currentMode = value; }
        public static CoordinateSystem CurrentCoordinateSystem { get => _coordinateSystem; set => _coordinateSystem = value; }
        
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