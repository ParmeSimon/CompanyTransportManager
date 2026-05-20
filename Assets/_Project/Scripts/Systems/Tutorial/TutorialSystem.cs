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
                case TutorialStep.CompanyCreate:        return TutorialStep.GoToDepot;
                case TutorialStep.GoToDepot:            return TutorialStep.RepairHangar;
                case TutorialStep.RepairHangar:         return TutorialStep.GoToVehicles;
                case TutorialStep.GoToVehicles:         return TutorialStep.BuyFirstVehicle;
                case TutorialStep.BuyFirstVehicle:      return TutorialStep.ReturnToDepot1;
                case TutorialStep.ReturnToDepot1:       return TutorialStep.RepairOffice;
                case TutorialStep.RepairOffice:         return TutorialStep.OpenHr;
                case TutorialStep.OpenHr:               return TutorialStep.HireFirstDriver;
                case TutorialStep.HireFirstDriver:      return TutorialStep.ReturnToDepot2;
                case TutorialStep.ReturnToDepot2:       return TutorialStep.RepairFuelTank;
                case TutorialStep.RepairFuelTank:       return TutorialStep.OpenFuel;
                case TutorialStep.OpenFuel:             return TutorialStep.FillFuel;
                case TutorialStep.FillFuel:             return TutorialStep.GoToMap;
                case TutorialStep.GoToMap:              return TutorialStep.AcceptFirstContract;
                case TutorialStep.AcceptFirstContract:  return TutorialStep.Complete;
                default:                                return TutorialStep.Complete;
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
                [TutorialStep.GoToDepot] = new StepData
                {
                    id = TutorialStep.GoToDepot,
                    advisorTitle = "Votre dépôt",
                    advisorMessage = "Bienvenue ! Votre dépôt est… en ruine. Allons-y, vous comprendrez vite.",
                    highlightTarget = "tab:depot",
                },
                [TutorialStep.RepairHangar] = new StepData
                {
                    id = TutorialStep.RepairHangar,
                    advisorTitle = "Le hangar",
                    advisorMessage = "Le hangar abrite vos camions. Réparez-le pour pouvoir en acheter.",
                    highlightTarget = "building:hangar",
                },
                [TutorialStep.GoToVehicles] = new StepData
                {
                    id = TutorialStep.GoToVehicles,
                    advisorTitle = "Achetez un véhicule",
                    advisorMessage = "Parfait ! Allons choisir votre premier camion.",
                    highlightTarget = "tab:vehicles",
                },
                [TutorialStep.BuyFirstVehicle] = new StepData
                {
                    id = TutorialStep.BuyFirstVehicle,
                    advisorTitle = "Premier achat",
                    advisorMessage = "Choisissez un véhicule abordable pour démarrer.",
                    highlightTarget = "card:first_vehicle",
                },
                [TutorialStep.ReturnToDepot1] = new StepData
                {
                    id = TutorialStep.ReturnToDepot1,
                    advisorTitle = "Retour au dépôt",
                    advisorMessage = "Retournons au dépôt, il reste du travail.",
                    highlightTarget = "tab:depot",
                },
                [TutorialStep.RepairOffice] = new StepData
                {
                    id = TutorialStep.RepairOffice,
                    advisorTitle = "Le bureau",
                    advisorMessage = "Le bureau accueille vos conducteurs. Réparez-le pour pouvoir embaucher.",
                    highlightTarget = "building:office",
                },
                [TutorialStep.OpenHr] = new StepData
                {
                    id = TutorialStep.OpenHr,
                    advisorTitle = "Embauche",
                    advisorMessage = "Ouvrez le panneau RH pour voir les candidats.",
                    highlightTarget = "btn:open_hr",
                },
                [TutorialStep.HireFirstDriver] = new StepData
                {
                    id = TutorialStep.HireFirstDriver,
                    advisorTitle = "Premier conducteur",
                    advisorMessage = "Embauchez un conducteur — sans lui, vos camions ne roulent pas.",
                    highlightTarget = "card:first_driver",
                },
                [TutorialStep.ReturnToDepot2] = new StepData
                {
                    id = TutorialStep.ReturnToDepot2,
                    advisorTitle = "Dernière étape au dépôt",
                    advisorMessage = "Il reste la cuve à carburant. Retournons au dépôt.",
                    highlightTarget = "tab:depot",
                },
                [TutorialStep.RepairFuelTank] = new StepData
                {
                    id = TutorialStep.RepairFuelTank,
                    advisorTitle = "La cuve",
                    advisorMessage = "La cuve permet de stocker du carburant. Réparez-la.",
                    highlightTarget = "building:fuel_tank",
                },
                [TutorialStep.OpenFuel] = new StepData
                {
                    id = TutorialStep.OpenFuel,
                    advisorTitle = "Approvisionnement",
                    advisorMessage = "Ouvrez le panneau Carburant.",
                    highlightTarget = "btn:open_fuel",
                },
                [TutorialStep.FillFuel] = new StepData
                {
                    id = TutorialStep.FillFuel,
                    advisorTitle = "Premier plein",
                    advisorMessage = "Remplissez la cuve pour pouvoir effectuer des livraisons.",
                    highlightTarget = "btn:fill_fuel",
                },
                [TutorialStep.GoToMap] = new StepData
                {
                    id = TutorialStep.GoToMap,
                    advisorTitle = "À l'aventure",
                    advisorMessage = "Tout est prêt ! Allons chercher un premier contrat sur la carte.",
                    highlightTarget = "tab:map",
                },
                [TutorialStep.AcceptFirstContract] = new StepData
                {
                    id = TutorialStep.AcceptFirstContract,
                    advisorTitle = "Premier contrat",
                    advisorMessage = "Choisissez une livraison adaptée à votre véhicule.",
                    highlightTarget = "card:first_contract",
                },
            };
        }
    }
}
