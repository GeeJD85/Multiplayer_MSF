#if UNITY_EDITOR
using Barebones.Logging;
using UnityEditor;
#endif
using UnityEngine;

namespace Barebones.MasterServer
{
    public class MsfRuntime
    {
        private string webGLQuitMessage = "You are in web browser window. The Quit command is not supported!";

        public bool IsEditor => Application.isEditor;

        public bool SupportsThreads { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void MsfAlert(string msg);
#endif

        public void Quit()
        {
#if UNITY_EDITOR && !UNITY_WEBGL
            EditorApplication.isPlaying = false;
#elif !UNITY_EDITOR && !UNITY_WEBGL
            Application.Quit();
#elif !UNITY_EDITOR && UNITY_WEBGL
            MsfAlert(webGLQuitMessage);
            Logs.Info(webGLQuitMessage);
#endif
        }

        public MsfRuntime()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SupportsThreads = false;
#else
            SupportsThreads = true;
#endif
        }
    }
}