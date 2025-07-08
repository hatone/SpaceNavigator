#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using SpaceNavigatorDriver;

namespace SpaceNavigatorDriver.Tests
{
    public class SpaceMouseTests
    {
        [Test]
        public void NavigationMode_EnumValues_AreCorrect()
        {
            // Test that all required navigation modes exist
            var modes = System.Enum.GetValues(typeof(NavigationMode));
            Assert.IsTrue(modes.Length >= 10, "Should have at least 10 navigation modes");
            
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.Object));
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.Camera));
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.TargetCamera));
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.Fly));
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.Helicopter));
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.Walk));
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.Drone));
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.Orbit));
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.Telekinesis));
            Assert.IsTrue(System.Enum.IsDefined(typeof(NavigationMode), NavigationMode.GrabMove));
        }

        [Test]
        public void CoordinateSystem_EnumValues_AreCorrect()
        {
            // Test coordinate system enum
            var systems = System.Enum.GetValues(typeof(CoordinateSystem));
            Assert.IsTrue(systems.Length == 4, "Should have exactly 4 coordinate systems");
            
            Assert.IsTrue(System.Enum.IsDefined(typeof(CoordinateSystem), CoordinateSystem.Local));
            Assert.IsTrue(System.Enum.IsDefined(typeof(CoordinateSystem), CoordinateSystem.Parent));
            Assert.IsTrue(System.Enum.IsDefined(typeof(CoordinateSystem), CoordinateSystem.World));
            Assert.IsTrue(System.Enum.IsDefined(typeof(CoordinateSystem), CoordinateSystem.Camera));
        }

        [Test]
        public void SimpleSceneViewController_Properties_DefaultValues()
        {
            // Test default values of properties
            Assert.IsFalse(SimpleSceneViewController.HorizonLock, "HorizonLock should default to false");
            Assert.IsFalse(SimpleSceneViewController.RotationLock, "RotationLock should default to false");
            Assert.AreEqual(NavigationMode.Fly, SimpleSceneViewController.CurrentMode, "Default mode should be Fly");
            Assert.AreEqual(CoordinateSystem.World, SimpleSceneViewController.CurrentCoordinateSystem, "Default coordinate system should be World");
        }

        [Test]
        public void SimpleSceneViewController_PropertySetters_Work()
        {
            // Test property setters
            bool originalHorizonLock = SimpleSceneViewController.HorizonLock;
            bool originalRotationLock = SimpleSceneViewController.RotationLock;
            NavigationMode originalMode = SimpleSceneViewController.CurrentMode;
            CoordinateSystem originalCoordSystem = SimpleSceneViewController.CurrentCoordinateSystem;

            try
            {
                SimpleSceneViewController.HorizonLock = !originalHorizonLock;
                Assert.AreEqual(!originalHorizonLock, SimpleSceneViewController.HorizonLock);

                SimpleSceneViewController.RotationLock = !originalRotationLock;
                Assert.AreEqual(!originalRotationLock, SimpleSceneViewController.RotationLock);

                SimpleSceneViewController.CurrentMode = NavigationMode.Camera;
                Assert.AreEqual(NavigationMode.Camera, SimpleSceneViewController.CurrentMode);

                SimpleSceneViewController.CurrentCoordinateSystem = CoordinateSystem.Local;
                Assert.AreEqual(CoordinateSystem.Local, SimpleSceneViewController.CurrentCoordinateSystem);
            }
            finally
            {
                // Restore original values
                SimpleSceneViewController.HorizonLock = originalHorizonLock;
                SimpleSceneViewController.RotationLock = originalRotationLock;
                SimpleSceneViewController.CurrentMode = originalMode;
                SimpleSceneViewController.CurrentCoordinateSystem = originalCoordSystem;
            }
        }

        [Test]
        public void AxisInversion_MathIsCorrect()
        {
            // Test axis inversion logic
            Vector3 testInput = new Vector3(1.0f, -1.0f, 0.5f);
            
            // Test X inversion
            Vector3 xInverted = new Vector3(-testInput.x, testInput.y, testInput.z);
            Assert.AreEqual(-1.0f, xInverted.x, 0.001f);
            Assert.AreEqual(-1.0f, xInverted.y, 0.001f);
            Assert.AreEqual(0.5f, xInverted.z, 0.001f);

            // Test Y inversion
            Vector3 yInverted = new Vector3(testInput.x, -testInput.y, testInput.z);
            Assert.AreEqual(1.0f, yInverted.x, 0.001f);
            Assert.AreEqual(1.0f, yInverted.y, 0.001f);
            Assert.AreEqual(0.5f, yInverted.z, 0.001f);

            // Test Z inversion
            Vector3 zInverted = new Vector3(testInput.x, testInput.y, -testInput.z);
            Assert.AreEqual(1.0f, zInverted.x, 0.001f);
            Assert.AreEqual(-1.0f, zInverted.y, 0.001f);
            Assert.AreEqual(-0.5f, zInverted.z, 0.001f);
        }

        [Test]
        public void RotationMath_QuaternionCalculations_AreCorrect()
        {
            // Test basic rotation calculations
            Vector3 rotationInput = new Vector3(0.1f, 0.2f, 0.05f);
            float rotationSensitivity = 57.2958f; // Radians to degrees
            
            // Test individual axis rotations
            Quaternion pitchRotation = Quaternion.AngleAxis(-rotationInput.x * rotationSensitivity, Vector3.right);
            Quaternion yawRotation = Quaternion.AngleAxis(rotationInput.y * rotationSensitivity, Vector3.up);
            Quaternion rollRotation = Quaternion.AngleAxis(rotationInput.z * rotationSensitivity, Vector3.forward);
            
            Assert.IsTrue(Mathf.Abs(pitchRotation.w) < 1.0f, "Pitch rotation should create a valid quaternion");
            Assert.IsTrue(Mathf.Abs(yawRotation.w) < 1.0f, "Yaw rotation should create a valid quaternion");
            Assert.IsTrue(Mathf.Abs(rollRotation.w) < 1.0f, "Roll rotation should create a valid quaternion");
            
            // Test combined rotation
            Quaternion combinedRotation = yawRotation * pitchRotation * rollRotation;
            Assert.IsTrue(Mathf.Abs(combinedRotation.w) <= 1.0f, "Combined rotation should be normalized");
        }

        [Test]
        public void MovementSpeed_Calculations_AreReasonable()
        {
            // Test movement speed calculation
            Vector3 cameraPosition = new Vector3(0, 0, 0);
            Vector3 pivotPosition = new Vector3(0, 0, 10);
            
            float distance = Vector3.Distance(cameraPosition, pivotPosition);
            float movementSpeed = Mathf.Max(0.1f, distance * 0.1f);
            
            Assert.AreEqual(1.0f, movementSpeed, 0.001f, "Movement speed should be 1.0 for 10 unit distance");
            
            // Test minimum speed
            pivotPosition = new Vector3(0, 0, 0.5f);
            distance = Vector3.Distance(cameraPosition, pivotPosition);
            movementSpeed = Mathf.Max(0.1f, distance * 0.1f);
            
            Assert.AreEqual(0.1f, movementSpeed, 0.001f, "Movement speed should have minimum of 0.1");
        }
    }

    public class ButtonMappingTests
    {
        [Test]
        public void ButtonMappingProfile_DefaultMappings_AreValid()
        {
            var profile = ScriptableObject.CreateInstance<ButtonMappingProfile>();
            
            Assert.IsNotNull(profile.button1, "Button 1 should have a mapping");
            Assert.IsNotNull(profile.button2, "Button 2 should have a mapping");
            Assert.IsNotNull(profile.menuButton, "Menu button should have a mapping");
            Assert.IsNotNull(profile.fitButton, "Fit button should have a mapping");
        }

        [Test]
        public void ButtonAction_EnumValues_AreComplete()
        {
            var actionTypes = System.Enum.GetValues(typeof(ButtonAction.ActionType));
            Assert.IsTrue(actionTypes.Length >= 5, "Should have at least 5 action types");
            
            Assert.IsTrue(System.Enum.IsDefined(typeof(ButtonAction.ActionType), ButtonAction.ActionType.UnityHotkey));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ButtonAction.ActionType), ButtonAction.ActionType.MenuItem));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ButtonAction.ActionType), ButtonAction.ActionType.CustomAction));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ButtonAction.ActionType), ButtonAction.ActionType.RadialMenu));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ButtonAction.ActionType), ButtonAction.ActionType.Macro));
        }

        [Test]
        public void ButtonMappingProfile_GetButtonAction_ReturnsCorrectAction()
        {
            var profile = ScriptableObject.CreateInstance<ButtonMappingProfile>();
            
            Assert.AreSame(profile.button1, profile.GetButtonAction(1));
            Assert.AreSame(profile.button2, profile.GetButtonAction(2));
            Assert.AreSame(profile.menuButton, profile.GetButtonAction(100));
            Assert.AreSame(profile.fitButton, profile.GetButtonAction(101));
            Assert.IsNull(profile.GetButtonAction(999), "Invalid button index should return null");
        }
    }

    public class MacroTests
    {
        [Test]
        public void SpaceMouseMacro_Creation_HasDefaultStep()
        {
            var macro = ScriptableObject.CreateInstance<SpaceMouseMacro>();
            
            Assert.IsNotNull(macro.steps, "Macro should have steps list");
            Assert.IsTrue(macro.steps.Count > 0, "Macro should have at least one default step");
        }

        [Test]
        public void MacroStep_EnumValues_AreComplete()
        {
            var stepTypes = System.Enum.GetValues(typeof(MacroStep.StepType));
            Assert.IsTrue(stepTypes.Length >= 4, "Should have at least 4 step types");
            
            Assert.IsTrue(System.Enum.IsDefined(typeof(MacroStep.StepType), MacroStep.StepType.MenuItem));
            Assert.IsTrue(System.Enum.IsDefined(typeof(MacroStep.StepType), MacroStep.StepType.CustomAction));
            Assert.IsTrue(System.Enum.IsDefined(typeof(MacroStep.StepType), MacroStep.StepType.Hotkey));
            Assert.IsTrue(System.Enum.IsDefined(typeof(MacroStep.StepType), MacroStep.StepType.Delay));
        }
    }

    public class RadialMenuTests
    {
        [Test]
        public void RadialMenuItem_Serialization_Works()
        {
            var item = new RadialMenuItem
            {
                id = "test",
                label = "Test Item",
                action = "TestAction",
                hotkey = "ctrl+t"
            };
            
            Assert.AreEqual("test", item.id);
            Assert.AreEqual("Test Item", item.label);
            Assert.AreEqual("TestAction", item.action);
            Assert.AreEqual("ctrl+t", item.hotkey);
        }

        [Test]
        public void RadialMenuConfig_HasRequiredProperties()
        {
            var config = new RadialMenuConfig
            {
                id = "nav",
                name = "Navigation",
                items = new System.Collections.Generic.List<RadialMenuItem>()
            };
            
            Assert.AreEqual("nav", config.id);
            Assert.AreEqual("Navigation", config.name);
            Assert.IsNotNull(config.items);
        }
    }
}
#endif