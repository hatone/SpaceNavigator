#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace SpaceNavigatorDriver
{
    [Overlay(typeof(SceneView), "SpaceMouse", "SpaceMouse Control")]
    public class SpaceMouseOverlay : Overlay
    {
        private VisualElement _root;
        private Label _modeLabel;
        private Label _lockStatusLabel;
        private Button _fitViewButton;
        private Button _frameSelectedButton;
        private Button _modeToggleButton;

        public override VisualElement CreatePanelContent()
        {
            _root = new VisualElement();
            _root.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.9f));
            _root.style.paddingLeft = 8;
            _root.style.paddingRight = 8;
            _root.style.paddingTop = 4;
            _root.style.paddingBottom = 4;
            _root.style.borderTopLeftRadius = 4;
            _root.style.borderTopRightRadius = 4;
            _root.style.borderBottomLeftRadius = 4;
            _root.style.borderBottomRightRadius = 4;
            _root.style.minWidth = 200;

            // Title
            var titleLabel = new Label("SpaceMouse");
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Color.white;
            _root.Add(titleLabel);

            // Mode display
            _modeLabel = new Label($"Mode: {SimpleSceneViewController.CurrentMode}");
            _modeLabel.style.fontSize = 10;
            _modeLabel.style.color = Color.white;
            _root.Add(_modeLabel);

            // Lock status
            _lockStatusLabel = new Label();
            _lockStatusLabel.style.fontSize = 10;
            _lockStatusLabel.style.color = Color.yellow;
            _root.Add(_lockStatusLabel);

            // Buttons row
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 4;
            _root.Add(buttonRow);

            // Fit View button
            _fitViewButton = new Button(() => {
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }
            });
            _fitViewButton.text = "Fit";
            _fitViewButton.style.fontSize = 10;
            _fitViewButton.style.height = 20;
            _fitViewButton.style.marginRight = 2;
            buttonRow.Add(_fitViewButton);

            // Frame Selected button
            _frameSelectedButton = new Button(() => {
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }
            });
            _frameSelectedButton.text = "Frame";
            _frameSelectedButton.style.fontSize = 10;
            _frameSelectedButton.style.height = 20;
            _frameSelectedButton.style.marginRight = 2;
            buttonRow.Add(_frameSelectedButton);

            // Mode toggle button
            _modeToggleButton = new Button(() => {
                CycleModes();
            });
            _modeToggleButton.text = "Mode";
            _modeToggleButton.style.fontSize = 10;
            _modeToggleButton.style.height = 20;
            buttonRow.Add(_modeToggleButton);

            // Coordinate system display (when in MoveObjects mode)
            var coordinateLabel = new Label();
            coordinateLabel.style.fontSize = 9;
            coordinateLabel.style.color = Color.cyan;
            coordinateLabel.style.marginTop = 2;
            _root.Add(coordinateLabel);

            // Update display periodically
            EditorApplication.update += UpdateDisplay;

            return _root;
        }

        private void UpdateDisplay()
        {
            if (_root == null) return;

            // Update mode display
            if (_modeLabel != null)
            {
                _modeLabel.text = $"Mode: {SimpleSceneViewController.CurrentMode}";
            }

            // Update lock status
            if (_lockStatusLabel != null)
            {
                string lockStatus = "";
                if (SimpleSceneViewController.HorizonLock) lockStatus += "H ";
                if (SimpleSceneViewController.RotationLock) lockStatus += "R ";
                
                _lockStatusLabel.text = lockStatus.Length > 0 ? $"Locks: {lockStatus.Trim()}" : "";
                _lockStatusLabel.style.display = lockStatus.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Update coordinate system display for MoveObjects mode
            if (_root.childCount > 4) // Has coordinate label
            {
                var coordinateLabel = _root[4] as Label;
                if (coordinateLabel != null)
                {
                    if (SimpleSceneViewController.CurrentMode == NavigationMode.Telekinesis)
                    {
                        coordinateLabel.text = $"Coords: {SimpleSceneViewController.CurrentCoordinateSystem}";
                        coordinateLabel.style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        coordinateLabel.style.display = DisplayStyle.None;
                    }
                }
            }
        }

        private void CycleModes()
        {
            NavigationMode[] modes = {
                NavigationMode.Object,
                NavigationMode.Camera,
                NavigationMode.TargetCamera,
                NavigationMode.Fly,
                NavigationMode.Helicopter,
                NavigationMode.Walk,
                NavigationMode.Drone,
                NavigationMode.Orbit,
                NavigationMode.Telekinesis,
                NavigationMode.GrabMove
            };

            var currentMode = SimpleSceneViewController.CurrentMode;
            int currentIndex = System.Array.IndexOf(modes, currentMode);
            int nextIndex = (currentIndex + 1) % modes.Length;
            
            SimpleSceneViewController.CurrentMode = modes[nextIndex];
            
            // Store selection for object manipulation modes
            if (modes[nextIndex] == NavigationMode.Telekinesis || modes[nextIndex] == NavigationMode.GrabMove)
            {
                // This would call the internal method if accessible
                // For now, we'll just switch the mode
            }
        }

        public override void OnWillBeDestroyed()
        {
            EditorApplication.update -= UpdateDisplay;
            base.OnWillBeDestroyed();
        }
    }

    [Overlay(typeof(SceneView), "SpaceMouse Quick Actions", "SpaceMouse Quick Actions")]
    public class SpaceMouseQuickActionsOverlay : Overlay
    {
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.9f));
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 4;
            root.style.paddingBottom = 4;
            root.style.borderTopLeftRadius = 4;
            root.style.borderTopRightRadius = 4;
            root.style.borderBottomLeftRadius = 4;
            root.style.borderBottomRightRadius = 4;

            // Quick action buttons
            var horizonLockButton = new Button(() => {
                SimpleSceneViewController.ToggleHorizonLock();
            });
            horizonLockButton.text = "H-Lock";
            horizonLockButton.style.fontSize = 10;
            horizonLockButton.style.height = 20;
            horizonLockButton.style.marginBottom = 2;
            root.Add(horizonLockButton);

            var rotationLockButton = new Button(() => {
                SimpleSceneViewController.ToggleRotationLock();
            });
            rotationLockButton.text = "R-Lock";
            rotationLockButton.style.fontSize = 10;
            rotationLockButton.style.height = 20;
            rotationLockButton.style.marginBottom = 2;
            root.Add(rotationLockButton);

            var recalibrateButton = new Button(() => {
                SimpleSceneViewController.RecalibrateDrift();
            });
            recalibrateButton.text = "Recal";
            recalibrateButton.style.fontSize = 10;
            recalibrateButton.style.height = 20;
            root.Add(recalibrateButton);

            return root;
        }
    }
}
#endif