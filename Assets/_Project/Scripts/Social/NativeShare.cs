using System.Runtime.InteropServices;
using UnityEngine;

namespace TransportManager.Social
{
    public static class NativeShare
    {
        public static void ShareText(string text)
        {
#if UNITY_IOS && !UNITY_EDITOR
            _ShareText(text);
#elif UNITY_ANDROID && !UNITY_EDITOR
            ShareAndroid(text);
#else
            GUIUtility.systemCopyBuffer = text;
            Debug.Log($"[NativeShare] Copié dans le presse-papier : {text}");
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _ShareText(string text);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void ShareAndroid(string text)
        {
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                using (var unity    = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var chooser  = AndroidJavaClass.CallStaticObjectMethod(
                                          "android.content.Intent", "createChooser", intent, "Partager via"))
                {
                    activity.Call("startActivity", chooser);
                }
            }
        }
#endif
    }
}
