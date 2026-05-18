using UnityEngine;
using TransportManager.UI.Common;
using TransportManager.UI.Tutorial;

namespace TransportManager.Core
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private GameManager gameManagerPrefab;

        private void Awake()
        {
            ShowSplashScreen();

            var gm = GameManager.Instance;
            if (gm == null)
            {
                if (gameManagerPrefab == null)
                {
                    Debug.LogError("[Bootstrap] gameManagerPrefab not assigned.");
                    return;
                }
                gm = Instantiate(gameManagerPrefab);
            }
            gm.Initialize();

            ShowTutorialOverlay();
            ShowCompanyCreationIfNeeded(gm);
        }

        private static void ShowSplashScreen()
        {
            var go = new GameObject("SplashScreen", typeof(RectTransform));
            DontDestroyOnLoad(go);
            go.AddComponent<SplashScreen>();
        }

        private static void ShowTutorialOverlay()
        {
            var go = new GameObject("TutorialOverlay", typeof(RectTransform));
            DontDestroyOnLoad(go);
            go.AddComponent<TutorialOverlayView>();
        }

        private static void ShowCompanyCreationIfNeeded(GameManager gm)
        {
            if (gm.Tutorial == null || gm.Tutorial.CurrentStepId != Systems.Tutorial.TutorialStep.CompanyCreate) return;
            var go = new GameObject("CompanyCreation", typeof(RectTransform));
            DontDestroyOnLoad(go);
            go.AddComponent<CompanyCreationView>();
        }
    }
}
