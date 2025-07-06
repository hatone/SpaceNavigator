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
        
        static SimpleSceneViewController()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            // Exit if no SpaceNavigator device is connected
            if (SpaceNavigatorHID.current == null) return;
            
            // Exit if Unity doesn't have focus (optional)
            if (!EditorApplication.isFocused) return;
            
            // Get the active SceneView
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;

            // Calculate delta time for frame-rate independent movement
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;

            // Read input from SpaceNavigator
            Vector3 translation = SpaceNavigatorHID.current.Translation.ReadValue();
            Vector3 rotation = SpaceNavigatorHID.current.Rotation.ReadValue();

            // Apply deadzone - exit if device is idle
            if (IsApproximatelyZero(translation, 0.001f) && IsApproximatelyZero(rotation, 0.001f))
                return;

            // Make movement frame-rate independent
            translation *= deltaTime * _deltaTimeFactor;
            rotation *= deltaTime * _deltaTimeFactor;

            // Apply sensitivity scaling
            translation *= 0.5f; // Adjust translation sensitivity
            rotation *= 2.0f;    // Adjust rotation sensitivity

            // Apply camera transformations
            ApplyCameraMovement(sceneView, translation, rotation);
        }

        private static void ApplyCameraMovement(SceneView sceneView, Vector3 translation, Vector3 rotation)
        {
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