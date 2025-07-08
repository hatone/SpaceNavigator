#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace SpaceNavigatorDriver {

	[Serializable]
	public class SpaceNavigatorWindow : EditorWindow, IDisposable {

		/// <summary>
		/// Initializes the window.
		/// </summary>
		[MenuItem("Window/SpaceNavigator/Settings &s", false, 1)]
		public static void Init() {
			SpaceNavigatorWindow window = GetWindow(typeof(SpaceNavigatorWindow)) as SpaceNavigatorWindow;
			if (window) {
				window.titleContent = new GUIContent("SpaceNavigator");
				window.Show();
				
				// Show welcome message for first-time users
				if (EditorPrefs.GetBool("SpaceNavigator_FirstRun", true))
				{
					window.ShowWelcomeDialog();
				}
			}
		}
		
		private void ShowWelcomeDialog()
		{
			bool shouldShow = EditorUtility.DisplayDialog(
				"Welcome to SpaceNavigator!",
				"This is your first time using SpaceNavigator.\n\n" +
				"Would you like to see the quick setup guide?\n\n" +
				"You can always access help from the settings window.",
				"Show Guide",
				"Skip"
			);
			
			if (shouldShow)
			{
				// The help section will be visible by default for first-time users
				EditorPrefs.SetBool("SpaceNavigator_ShowHelp", true);
			}
			
			EditorPrefs.SetBool("SpaceNavigator_FirstRun", false);
		}

		public static void OnDisable() {
			// Write settings to PlayerPrefs when EditorWindow is closed.
			Settings.Write();
		}

		public static void OnDestroy() {
			// Write settings to PlayerPrefs when EditorWindow is closed.
			Settings.Write();
		}

		// This does not get called, unfortunately...
		public void OnApplicationQuit() {
			ViewportController.OnApplicationQuit();
		}

		public void OnSelectionChange() {
			ViewportController.StoreSelectionTransforms();
		}

		public void OnGUI() {
			if (Settings.OnGUI())
				SpaceNavigatorToolbar.Instance.TriggerRefresh();
		}

		public void Dispose() {
			// Write settings to PlayerPrefs when EditorWindow is closed.
			Settings.Write();
		}
	}
}
#endif