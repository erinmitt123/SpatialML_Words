using UnityEngine;

namespace WordWorld.App
{
    /// <summary>
    /// Leaving Word World ends it, so opening it again is a fresh launch.
    ///
    /// PICO's "Back to Home" only stops the activity: the process lives on, and re-opening showed
    /// the old session (Nov 16 recording: tour stuck on 6/9, "Recognition off" because the camera
    /// feed does not come back after the app is backgrounded, practice objects standing in).
    /// Everything is saved as it changes, so ending the process loses nothing.
    ///
    /// A headset sleep (taken off) also pauses the app; that keeps the session, so only a pause
    /// while the screen is still on counts as leaving. Permission prompts pause it too and are
    /// excluded via <see cref="AskingPermission"/>.
    /// </summary>
    public sealed class AppLifecycle : MonoBehaviour
    {
        private const string LogTag = "[WordWorld.App]";

        /// <summary>Set while a system permission prompt is up; it pauses the app without leaving it.</summary>
        public static bool AskingPermission { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject(nameof(AppLifecycle)) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<AppLifecycle>();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            bool screenOn = IsScreenOn();
            Debug.Log($"{LogTag} paused (screen on: {screenOn}, asking permission: {AskingPermission})");
            if (screenOn && !AskingPermission) EndProcess(false);
        }

        /// <summary>
        /// Ends the app without Application.Quit: Unity's XR shutdown crashes inside PICO's spatial
        /// runtime (SIGSEGV in SpatialInputProvider::Stop -> HMDTracking::stop, Nov 12).
        /// </summary>
        public static void EndProcess(bool leaveScreenFirst)
        {
            Debug.Log($"{LogTag} ending process");
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (leaveScreenFirst)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    activity.Call<bool>("moveTaskToBack", true);
                }
                using var process = new AndroidJavaClass("android.os.Process");
                process.CallStatic("killProcess", process.CallStatic<int>("myPid"));
                return;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogTag} quick exit failed ({e.Message}); quitting normally");
            }
#endif
            Application.Quit();
        }

        // Asleep (headset off the head) the screen is off; a deliberate exit leaves it on.
        private static bool IsScreenOn()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject power = activity.Call<AndroidJavaObject>("getSystemService", "power");
                return power.Call<bool>("isInteractive");
            }
            catch (System.Exception e)
            {
                // Unknown: keep the session rather than end it by mistake.
                Debug.LogWarning($"{LogTag} screen state unknown ({e.Message})");
                return false;
            }
#else
            return false;
#endif
        }
    }
}
