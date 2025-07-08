#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace SpaceNavigatorDriver
{
    [System.Serializable]
    public class RadialMenuItem
    {
        public string id;
        public string label;
        public string icon;
        public string action;
        public string hotkey;
        public string menuPath;
        public List<RadialMenuItem> submenu;
    }

    [System.Serializable]
    public class RadialMenuConfig
    {
        public string id;
        public string name;
        public List<RadialMenuItem> items;
    }

    [System.Serializable]
    public class RadialMenuData
    {
        public List<RadialMenuConfig> menus;
    }

    public class RadialMenuSystem : EditorWindow
    {
        private static RadialMenuSystem _instance;
        private RadialMenuData _menuData;
        private Vector2 _mousePosition;
        private bool _isVisible = false;
        private RadialMenuConfig _currentMenu;
        private int _selectedIndex = -1;
        private float _menuRadius = 100f;
        private string _configPath = "Assets/SpaceMouse/menus.json";

        public static void ShowMenu(string menuId = "nav")
        {
            if (_instance == null)
            {
                _instance = CreateInstance<RadialMenuSystem>();
                _instance.LoadMenuConfiguration();
            }

            _instance._currentMenu = _instance.GetMenu(menuId);
            if (_instance._currentMenu != null)
            {
                _instance._mousePosition = Event.current.mousePosition;
                _instance._isVisible = true;
                _instance.ShowPopup();
                _instance.Focus();
            }
        }

        public static void HideMenu()
        {
            if (_instance != null)
            {
                _instance._isVisible = false;
                _instance.Close();
            }
        }

        private void LoadMenuConfiguration()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    _menuData = JsonUtility.FromJson<RadialMenuData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load RadialMenu config: {e.Message}");
                    try
                    {
                        CreateDefaultConfiguration();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to create default RadialMenu configuration: {ex.Message}");
                        _menuData = new RadialMenuData { menus = new List<RadialMenuConfig>() };
                    }
                }
            }
            else
            {
                try
                {
                    CreateDefaultConfiguration();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to create default RadialMenu configuration: {ex.Message}");
                    _menuData = new RadialMenuData { menus = new List<RadialMenuConfig>() };
                }
            }
        }

        private void CreateDefaultConfiguration()
        {
            _menuData = new RadialMenuData
            {
                menus = new List<RadialMenuConfig>
                {
                    new RadialMenuConfig
                    {
                        id = "nav",
                        name = "Navigation",
                        items = new List<RadialMenuItem>
                        {
                            new RadialMenuItem
                            {
                                id = "object_mode",
                                label = "Object",
                                action = "SetObjectMode",
                                hotkey = "",
                                menuPath = "Window/SpaceNavigator/Navigation Modes/Object Mode"
                            },
                            new RadialMenuItem
                            {
                                id = "camera_mode",
                                label = "Camera",
                                action = "SetCameraMode",
                                hotkey = "",
                                menuPath = "Window/SpaceNavigator/Navigation Modes/Camera Mode"
                            },
                            new RadialMenuItem
                            {
                                id = "fly_mode",
                                label = "Fly",
                                action = "SetFlyMode",
                                hotkey = "",
                                menuPath = "Window/SpaceNavigator/Navigation Modes/Fly Mode"
                            },
                            new RadialMenuItem
                            {
                                id = "walk_mode",
                                label = "Walk",
                                action = "SetWalkMode",
                                hotkey = "",
                                menuPath = "Window/SpaceNavigator/Navigation Modes/Walk Mode"
                            },
                            new RadialMenuItem
                            {
                                id = "drone_mode",
                                label = "Drone",
                                action = "SetDroneMode",
                                hotkey = "",
                                menuPath = "Window/SpaceNavigator/Navigation Modes/Drone Mode"
                            },
                            new RadialMenuItem
                            {
                                id = "moveobjects_mode",
                                label = "MoveObjects",
                                action = "SetTelekinesisMode",
                                hotkey = "",
                                menuPath = "Window/SpaceNavigator/Navigation Modes/MoveObjects Mode"
                            },
                            new RadialMenuItem
                            {
                                id = "frame_selected",
                                label = "Frame",
                                action = "FrameSelected",
                                hotkey = "f",
                                menuPath = ""
                            },
                            new RadialMenuItem
                            {
                                id = "horizon_lock",
                                label = "H-Lock",
                                action = "ToggleHorizonLock",
                                hotkey = "",
                                menuPath = "Window/SpaceNavigator/Locks/Toggle Horizon Lock"
                            }
                        }
                    },
                    new RadialMenuConfig
                    {
                        id = "tools",
                        name = "Tools",
                        items = new List<RadialMenuItem>
                        {
                            new RadialMenuItem
                            {
                                id = "play_pause",
                                label = "Play/Pause",
                                action = "PlayPause",
                                hotkey = "ctrl+p",
                                menuPath = ""
                            },
                            new RadialMenuItem
                            {
                                id = "undo",
                                label = "Undo",
                                action = "Undo",
                                hotkey = "ctrl+z",
                                menuPath = ""
                            },
                            new RadialMenuItem
                            {
                                id = "redo",
                                label = "Redo",
                                action = "Redo",
                                hotkey = "ctrl+y",
                                menuPath = ""
                            },
                            new RadialMenuItem
                            {
                                id = "toggle_scene_game",
                                label = "Scene/Game",
                                action = "ToggleSceneGame",
                                hotkey = "",
                                menuPath = ""
                            }
                        }
                    }
                }
            };

            SaveMenuConfiguration();
        }

        private void SaveMenuConfiguration()
        {
            try
            {
                string directory = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(_menuData, true);
                File.WriteAllText(_configPath, json);
                AssetDatabase.Refresh();
                Debug.Log($"RadialMenu configuration saved to {_configPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save RadialMenu config: {e.Message}");
            }
        }

        private RadialMenuConfig GetMenu(string menuId)
        {
            if (_menuData?.menus != null)
            {
                foreach (var menu in _menuData.menus)
                {
                    if (menu.id == menuId)
                        return menu;
                }
            }
            return null;
        }

        private void OnGUI()
        {
            if (!_isVisible || _currentMenu == null) return;

            // Semi-transparent background
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, position.width, position.height), EditorGUIUtility.whiteTexture);
            GUI.color = Color.white;

            // Calculate menu positions
            Vector2 center = new Vector2(position.width * 0.5f, position.height * 0.5f);
            int itemCount = _currentMenu.items.Count;
            
            if (itemCount == 0) return;

            // Update selected index based on mouse position
            UpdateSelection(center);

            // Draw menu items
            for (int i = 0; i < itemCount; i++)
            {
                float angle = (i / (float)itemCount) * 2 * Mathf.PI - Mathf.PI / 2;
                Vector2 itemPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _menuRadius;
                
                var item = _currentMenu.items[i];
                bool isSelected = i == _selectedIndex;
                
                // Draw item background
                GUI.color = isSelected ? Color.yellow : Color.white;
                var rect = new Rect(itemPos.x - 40, itemPos.y - 20, 80, 40);
                GUI.Box(rect, "");
                
                // Draw item label
                GUI.color = isSelected ? Color.black : Color.white;
                GUI.Label(rect, item.label, new GUIStyle(GUI.skin.label) 
                { 
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12
                });
            }

            GUI.color = Color.white;

            // Handle input
            if (Event.current.type == EventType.MouseDown)
            {
                if (_selectedIndex >= 0 && _selectedIndex < _currentMenu.items.Count)
                {
                    ExecuteAction(_currentMenu.items[_selectedIndex]);
                }
                HideMenu();
            }
            else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                HideMenu();
            }

            Repaint();
        }

        private void UpdateSelection(Vector2 center)
        {
            Vector2 mousePos = Event.current.mousePosition;
            Vector2 direction = mousePos - center;
            
            if (direction.magnitude < 30f)
            {
                _selectedIndex = -1;
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) + Mathf.PI / 2;
            if (angle < 0) angle += 2 * Mathf.PI;
            
            int itemCount = _currentMenu.items.Count;
            float segmentAngle = (2 * Mathf.PI) / itemCount;
            _selectedIndex = Mathf.FloorToInt(angle / segmentAngle);
            
            if (_selectedIndex >= itemCount) _selectedIndex = itemCount - 1;
        }

        private void ExecuteAction(RadialMenuItem item)
        {
            switch (item.action)
            {
                case "SetObjectMode":
                    SimpleSceneViewController.SetObjectMode();
                    break;
                case "SetCameraMode":
                    SimpleSceneViewController.SetCameraMode();
                    break;
                case "SetFlyMode":
                    SimpleSceneViewController.SetFlyMode();
                    break;
                case "SetWalkMode":
                    SimpleSceneViewController.SetWalkMode();
                    break;
                case "SetDroneMode":
                    SimpleSceneViewController.SetDroneMode();
                    break;
                case "SetTelekinesisMode":
                    SimpleSceneViewController.SetTelekinesisMode();
                    break;
                case "ToggleHorizonLock":
                    SimpleSceneViewController.ToggleHorizonLock();
                    break;
                case "FrameSelected":
                    if (SceneView.lastActiveSceneView != null)
                        SceneView.lastActiveSceneView.FrameSelected();
                    break;
                case "PlayPause":
                    EditorApplication.isPlaying = !EditorApplication.isPlaying;
                    break;
                case "Undo":
                    Undo.PerformUndo();
                    break;
                case "Redo":
                    Undo.PerformRedo();
                    break;
                case "ToggleSceneGame":
                    // Toggle between Scene and Game view
                    var sceneView = SceneView.lastActiveSceneView;
                    if (sceneView != null)
                    {
                        // This would require more complex logic to actually toggle views
                        Debug.Log("Toggle Scene/Game view");
                    }
                    break;
                default:
                    if (!string.IsNullOrEmpty(item.menuPath))
                    {
                        EditorApplication.ExecuteMenuItem(item.menuPath);
                    }
                    break;
            }
        }

        [MenuItem("Window/SpaceMouse/Test Radial Menu")]
        public static void TestRadialMenu()
        {
            ShowMenu("nav");
        }

        [MenuItem("Window/SpaceMouse/Edit Radial Menu Config")]
        public static void EditRadialMenuConfig()
        {
            var instance = CreateInstance<RadialMenuSystem>();
            instance.LoadMenuConfiguration();
            
            string configPath = "Assets/SpaceMouse/menus.json";
            if (File.Exists(configPath))
            {
                EditorUtility.OpenWithDefaultApp(configPath);
            }
            else
            {
                instance.SaveMenuConfiguration();
                EditorUtility.OpenWithDefaultApp(configPath);
            }
        }
    }
}
#endif