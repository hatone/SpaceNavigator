# SpaceMouse Unity Plugin

A professional Unity editor plugin for 3DConnexion SpaceMouse devices, providing feature parity with the official Windows Unity plugin for macOS and enhanced functionality.

## Features

### 🎮 Navigation Modes
- **Object Mode**: Camera orbits around selected objects
- **Camera Mode**: Traditional FPS camera controls  
- **Target Camera**: Orbit around current view pivot
- **Fly Mode**: Helicopter-like movement with full 6DoF
- **Helicopter Mode**: Keep altitude with limited roll
- **Walk Mode**: Ground-clamped movement for architectural walkthroughs
- **Drone Mode**: Free 6DoF movement with hover capability
- **Orbit Mode**: Legacy orbit implementation
- **MoveObjects Mode**: Transform selected objects with SpaceMouse
- **GrabMove Mode**: Grab and move objects in 3D space

### 🔒 Lock Controls
- **Horizon Lock**: Keeps the horizon level during movement
- **Rotation Lock**: Disables all rotation input
- Toggle locks via menu or SceneView overlay

### 🎯 Object Manipulation
When objects are selected, MoveObjects mode supports multiple coordinate systems:
- **Local**: Move relative to object's local axes
- **Parent**: Move relative to parent object's axes  
- **World**: Move in world space coordinates
- **Camera**: Move relative to camera's view

### ⚙️ Unity Integration
- **Preferences Panel**: Access via `Window > SpaceMouse > Preferences`
  - Navigation mode selection
  - Speed sliders with logarithmic scaling (0.1-10x)
  - Axis inversion checkboxes for all 6 axes
  - Lock control toggles
  - Real-time device status display
- **SceneView Overlays**: 
  - Current mode and lock status display
  - Quick action buttons (Fit, Frame, Mode cycling)
  - Horizon/Rotation lock toggles

### 🎯 Radial Menu System
- Press and hold the Menu button to display circular context menu
- Configurable via JSON file: `Assets/SpaceMouse/menus.json`
- Default menus include:
  - Navigation mode switching
  - Frame/Fit view commands
  - Lock toggles
  - Custom actions

### 🎮 Button Mapping
- Create button mapping profiles as ScriptableObject assets
- Support for multiple action types:
  - Unity hotkeys (ctrl+s, f, etc.)
  - Menu items (Edit/Undo, etc.) 
  - Custom actions (FrameSelected, etc.)
  - Radial menu triggers
  - Macro execution
- Long-press detection for secondary actions
- Default profile includes:
  - B1: Frame Selected (long-press: Radial Menu)
  - B2: Fit View
  - B3: Toggle Scene/Game
  - B4: Play/Pause (Ctrl+P)
  - B5: Undo (Ctrl+Z)
  - B6: Redo (Ctrl+Y)

### 🤖 Macro System
- Create custom macro sequences as ScriptableObject assets
- Support for:
  - Menu item execution
  - Custom action triggers
  - Hotkey simulation
  - Timed delays
- Chain multiple actions together
- Execute via button mapping or radial menu

## Installation

### Unity Package Manager (Recommended)
1. Open Unity Package Manager
2. Click the `+` button and select "Add package from git URL"
3. Enter: `https://github.com/PatHightree/SpaceNavigator.git#enhanced-spacemouse-plugin`

### Manual Installation
1. Download the latest release
2. Extract to your project's `Packages` folder
3. Unity will automatically import the package

## Quick Start

1. Connect your SpaceMouse device
2. Open `Window > SpaceMouse > Preferences` to configure settings
3. In SceneView, the SpaceMouse overlay shows current mode and status
4. Use `Window > SpaceNavigator > Navigation Modes` to switch modes
5. Create custom button profiles via `Window > SpaceMouse > Create Button Profile`

## Device Compatibility

Tested with:
- SpaceMouse Pro
- SpaceMouse Enterprise
- SpaceMouse Wireless
- SpaceNavigator (classic)

The plugin uses Unity's InputSystem with HID backend, avoiding the need for signed kernel extensions on macOS.

## Advanced Configuration

### Radial Menu Customization
Edit `Assets/SpaceMouse/menus.json` to customize radial menus:

```json
{
  "menus": [
    {
      "id": "nav",
      "name": "Navigation",
      "items": [
        {
          "id": "fly_mode",
          "label": "Fly",
          "action": "SetFlyMode",
          "menuPath": "Window/SpaceNavigator/Navigation Modes/Fly Mode"
        }
      ]
    }
  ]
}
```

### Button Mapping Profiles
Create custom profiles via `Window > SpaceMouse > Create Button Profile`:
- Assign actions to each button (1-8)
- Configure long-press actions
- Set custom action delays

### Macro Creation
Create macros via `Window > SpaceMouse > Create Macro`:
- Add sequential steps
- Mix menu items, actions, and delays
- Test macros in the inspector

## API Reference

### Core Classes
- `SimpleSceneViewController`: Main navigation controller
- `SpaceMousePreferences`: Settings management
- `ButtonMappingProfile`: Button configuration
- `SpaceMouseMacro`: Macro sequences
- `RadialMenuSystem`: Context menu system

### Public Properties
```csharp
// Access current settings
bool horizonLock = SimpleSceneViewController.HorizonLock;
NavigationMode mode = SimpleSceneViewController.CurrentMode;
CoordinateSystem coords = SimpleSceneViewController.CurrentCoordinateSystem;

// Programmatic control
SimpleSceneViewController.SetFlyMode();
SimpleSceneViewController.ToggleHorizonLock();
```

## Troubleshooting

### Device Not Detected
1. Check `Window > SpaceMouse > Preferences` for device status
2. Ensure InputSystem is enabled in Project Settings
3. Try unplugging and reconnecting the device
4. Use `Window > SpaceNavigator > Recalibrate Drift` if movement feels off

### Performance Issues
1. Disable debug mode via `Window > SpaceNavigator > Toggle Debug Mode`
2. Reduce movement/rotation speed in preferences
3. Check for conflicting input handlers

### macOS Specific
- No kernel extension required
- If cursor movement occurs, the plugin includes automatic cursor locking
- Ensure Unity has accessibility permissions if needed

## Development

### Building from Source
1. Clone the repository
2. Open in Unity 2022.3 LTS or later
3. Install dependencies: InputSystem, UI Toolkit
4. Run tests via `Window > General > Test Runner`

### Contributing
1. Fork the repository
2. Create a feature branch
3. Add unit tests for new functionality
4. Submit a pull request

## License

MIT License - see LICENSE.md for details.

## Acknowledgments

- Based on the original SpaceNavigator driver by Patrick Hogenboom
- Inspired by 3Dconnexion's official Unity plugin
- Community feedback and testing

## Support

- Report issues on GitHub: https://github.com/PatHightree/SpaceNavigator/issues
- Unity Forum thread: [Link to be added]
- Documentation: [Link to be added]

---

**Note**: This plugin is designed for Unity Editor use. For runtime SpaceMouse support, see the included runtime samples.

## Migration from Original Driver

This enhanced plugin maintains backward compatibility with the original PatHightree SpaceNavigator driver while adding significant new functionality. Existing projects can upgrade seamlessly.

### What's New in 3.0.0

#### Enhanced Navigation
- 6 additional navigation modes beyond the original Fly/Orbit/Telekinesis/GrabMove
- Horizon and rotation lock controls
- Coordinate system selection for object manipulation

#### Professional UI
- Unity Preferences panel integration
- SceneView overlays with real-time status
- Comprehensive settings with axis inversion and speed control

#### Advanced Interaction
- Radial menu system with JSON configuration
- Button mapping profiles with ScriptableObject architecture
- Macro system for complex action sequences
- Long-press detection for secondary button actions

#### Technical Improvements
- Full UIToolkit integration for modern Unity versions
- Comprehensive unit test coverage
- Assembly definitions for clean code organization
- Enhanced input processing with drift compensation

## Previous Version (2.0.0) Features

### 3DConnexion driver no longer required
The driver communicates directly with the HID device via Unity's new Input System (Unity 2019.1 and up).   
This means that it **no longer requires the 3DConnexion driver** to be running or even installed.  

### Editor toolbar
Makes common operations available in the scene view:  
- Navigation mode switching  
- Speed switching
- Presentation mode  
- Opening the settings window

### Presentation mode
Automatically smooths out input for demos and video capture.

### Enhanced drift protection
Calibration system stores and subtracts drift values for improved accuracy.

### Focus-aware navigation
Only processes input when Unity has focus, preventing conflicts with other applications.

## Credits
- Jonathan Owen for SpaceMouse Wireless fixes 
- Felix Herbst for the input helper and adding scene focus to runtime navigation
- William Iturzaeta from Canon Medical Systems USA, for hiring me to make this project compatible with Unity 2020  
- Stephen Wolter for further refinement to the mac drift fix 
- Enrico Tuttobene for contributing the mac drift fix
- Kieron Lanning for implementing navigation at runtime
- Chase Cobb from Google for motivating me to implement the mac version
- Manuela Maier and Dave Buchhoffer (@vsaitoo) for testing and development feedback
- Ewoud Wijma for loaning me the Hackingtosh for building the Mac port
- Quaternion math by Minahito
- Original SpaceNavigator driver by Patrick Hogenboom