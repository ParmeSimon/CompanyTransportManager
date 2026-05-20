using UnityEngine;
using TransportManager.Enums;
using TransportManager.Events;
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

            bool firstLaunch = gm.Tutorial != null && !gm.Tutorial.IsCompleted();
            // On first launch we land on the Depot view (story starts there).
            // Once the player has gone through the intro, default to Map.
            GameEvents.RaiseTabChanged(firstLaunch ? TabType.Depot : TabType.Map);

            ShowTutorialOverlay();
            if (firstLaunch && gm.Tutorial.CurrentStepId == Systems.Tutorial.TutorialStep.CompanyCreate)
                ShowIntroDialogue();
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

        private static void ShowIntroDialogue()
        {
            var go = new GameObject("IntroDialogue", typeof(RectTransform));
            DontDestroyOnLoad(go);
            go.AddComponent<IntroDialogueView>();
        }
    }
}
