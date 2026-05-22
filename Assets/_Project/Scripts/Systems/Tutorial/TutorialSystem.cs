using System;
using System.Collections.Generic;
using TransportManager.Save;

namespace TransportManager.Systems.Tutorial
{
    public class TutorialSystem
    {
        public class StepData
        {
            public string id;
            public string advisorTitle;
            public string advisorMessage;
            public string highlightTarget;   // logical name: "tab:depot", "btn:repair_hangar", "tab:vehicles", "card:first", ...
            public bool blocksOtherInput = true;
        }

        public event Action<StepData> OnStepChanged;
        public event Action OnTutorialCompleted;

        private readonly GameSaveData _save;
        private readonly Dictionary<string, StepData> _steps;
        private string _currentStepId;

        public bool IsActive => !_save.tutorial.completed;
        public bool IsCompleted() => _save.tutorial.completed;
        public string CurrentStepId => _currentStepId;
        public StepData CurrentStep => _steps.TryGetValue(_currentStepId, out var s) ? s : null;

        public TutorialSystem(GameSaveData save)
        {
            _save = save;
            _steps = BuildSteps();
            _currentStepId = string.IsNullOrEmpty(save.tutorial.currentStepId) ? TutorialStep.CompanyCreate : save.tutorial.currentStepId;
        }

        public void Start()
        {
            if (_save.tutorial.completed) return;
            EmitCurrent();
        }

        public void Advance(string fromStepId)
        {
            if (_save.tutorial.completed) return;
            if (fromStepId != _currentStepId) return;

            string next = NextStepId(_currentStepId);
            _currentStepId = next;
            _save.tutorial.currentStepId = next;

            if (next == TutorialStep.Complete)
            {
                _save.tutorial.completed = true;
                OnTutorialCompleted?.Invoke();
                return;
            }
            EmitCurrent();
        }

        public void SkipAll()
        {
            _save.tutorial.completed = true;
            _save.tutorial.currentStepId = TutorialStep.Complete;
            _currentStepId = TutorialStep.Complete;
            OnTutorialCompleted?.Invoke();
        }

        private void EmitCurrent()
        {
            if (_steps.TryGetValue(_currentStepId, out var step))
                OnStepChanged?.Invoke(step);
        }

        private static string NextStepId(string current)
        {
            switch (current)
            {
                case TutorialStep.CompanyCreate:      return TutorialStep.GoToMap;
                case TutorialStep.GoToMap:            return TutorialStep.MapRealtime;
                case TutorialStep.MapRealtime:        return TutorialStep.GoToDepot;
                case TutorialStep.GoToDepot:          return TutorialStep.ExplainDepotLevels;
                case TutorialStep.ExplainDepotLevels: return TutorialStep.GoToVehicles;
                case TutorialStep.GoToVehicles:       return TutorialStep.BuyFirstVehicle;
                case TutorialStep.BuyFirstVehicle:    return TutorialStep.GoToShop;
                case TutorialStep.GoToShop:           return TutorialStep.ClaimDailyOffer;
                case TutorialStep.ClaimDailyOffer:    return TutorialStep.Complete;
                default:                              return TutorialStep.Complete;
            }
        }

        private static Dictionary<string, StepData> BuildSteps()
        {
            return new Dictionary<string, StepData>
            {
                [TutorialStep.CompanyCreate] = new StepData
                {
                    id = TutorialStep.CompanyCreate,
                    advisorTitle = "Bienvenue",
                    advisorMessage = "Bonjour ! Je suis Élise, votre conseillère. Avant tout, donnez un nom à votre entreprise.",
                    highlightTarget = "ui:company_name_input",
                },
                [TutorialStep.GoToMap] = new StepData
                {
                    id = TutorialStep.GoToMap,
                    advisorTitle = "La carte",
                    advisorMessage = "Commençons par la carte ! Appuyez sur \"Carte\" dans la barre de navigation à gauche.",
                    highlightTarget = "tab:map",
                },
                [TutorialStep.MapRealtime] = new StepData
                {
                    id = TutorialStep.MapRealtime,
                    advisorTitle = "Suivi en temps réel",
                    advisorMessage = "Sur cette carte, vous pouvez voir l'ensemble de vos transports en temps réel. Chaque camion en route y est affiché !",
                    highlightTarget = null,
                    blocksOtherInput = false,
                },
                [TutorialStep.GoToDepot] = new StepData
                {
                    id = TutorialStep.GoToDepot,
                    advisorTitle = "Le dépôt",
                    advisorMessage = "Direction le dépôt ! Appuyez sur \"Dépôt\" dans la barre de navigation à gauche.",
                    highlightTarget = "tab:depot",
                },
                [TutorialStep.ExplainDepotLevels] = new StepData
                {
                    id = TutorialStep.ExplainDepotLevels,
                    advisorTitle = "Améliorer le dépôt",
                    advisorMessage = "Chaque bâtiment possède un niveau. En les améliorant, vous débloquez de nouvelles capacités : plus de camions, plus de carburant, plus de conducteurs !",
                    highlightTarget = null,
                    blocksOtherInput = false,
                },
                [TutorialStep.GoToVehicles] = new StepData
                {
                    id = TutorialStep.GoToVehicles,
                    advisorTitle = "Vos véhicules",
                    advisorMessage = "Allons voir vos véhicules ! Appuyez sur \"Véhicules\" dans la barre de navigation à gauche.",
                    highlightTarget = "tab:vehicles",
                },
                [TutorialStep.BuyFirstVehicle] = new StepData
                {
                    id = TutorialStep.BuyFirstVehicle,
                    advisorTitle = "Premier achat",
                    advisorMessage = "Achetez votre premier camion pour démarrer votre activité.",
                    highlightTarget = "card:first_vehicle",
                },
                [TutorialStep.GoToShop] = new StepData
                {
                    id = TutorialStep.GoToShop,
                    advisorTitle = "Le magasin",
                    advisorMessage = "Excellent ! Direction le magasin — appuyez sur \"Magasin\" dans la barre de navigation à gauche.",
                    highlightTarget = "tab:shop",
                },
                [TutorialStep.ClaimDailyOffer] = new StepData
                {
                    id = TutorialStep.ClaimDailyOffer,
                    advisorTitle = "Offre du jour",
                    advisorMessage = "Récupérez l'offre du jour ! Elle se renouvelle chaque jour — ne la manquez pas.",
                    highlightTarget = "btn:daily_offer",
                },
            };
        }
    }
}
