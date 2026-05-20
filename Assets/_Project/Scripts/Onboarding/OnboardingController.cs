using UnityEngine;
using TransportManager.Core;

namespace TransportManager.Onboarding
{
    public class OnboardingController : MonoBehaviour
    {
        public OnboardingStep CurrentStep { get; private set; }

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Save != null && gm.Save.company.onboardingCompleted)
            {
                gameObject.SetActive(false);
                return;
            }
            CurrentStep = OnboardingStep.AdvisorIntro;
            // TODO: drive UI panels per step (advisor sprite, welcome modal, name input, tutorial overlays).
        }

        public void Advance(OnboardingStep next)
        {
            CurrentStep = next;
        }

        public void SetCompanyName(string companyName)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null) return;
            gm.Save.company.companyName = companyName;
            if (gm.Save.company.createdAtUtcTicks == 0)
                gm.Save.company.createdAtUtcTicks = System.DateTime.UtcNow.Ticks;
        }

        public void Complete()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null) return;
            gm.Save.company.onboardingCompleted = true;
            gm.SaveNow();
            CurrentStep = OnboardingStep.Done;
            gameObject.SetActive(false);
        }
    }
}
