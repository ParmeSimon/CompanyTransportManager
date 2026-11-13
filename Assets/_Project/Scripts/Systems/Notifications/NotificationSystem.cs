using System;
using System.Collections.Generic;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Vehicles;
using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Systems.Map;

namespace TransportManager.Systems.Notifications
{
    /// <summary>
    /// Journal des événements du jeu (livraisons, accidents, démissions, niveau…) avec l'heure
    /// RÉELLE de l'événement : un contrat terminé pendant la nuit est daté de sa fin, pas de
    /// l'ouverture du jeu. Sauvegardé, plafonné à MaxEntries.
    /// </summary>
    public class NotificationSystem
    {
        public const int MaxEntries = 150;

        /// Levé à chaque ajout / lecture (badge de la cloche, popup ouverte).
        public static event Action Changed;

        /// Heure à utiliser pour les notifications émises pendant le traitement d'un contrat
        /// (fin réelle du trajet / moment de l'accident). 0 = maintenant.
        public static long ContextTicks;

        private readonly GameSaveData _save;

        public NotificationSystem(GameSaveData save)
        {
            _save = save;
            if (_save.notifications == null) _save.notifications = new List<NotificationEntry>();

            GameEvents.OnCompanyLevelUp       += (_, lvl) => Push(NotificationKind.LevelUp, $"Niveau {lvl} atteint !",
                "De nouveaux camions, contrats et compétences sont disponibles.");
            GameEvents.OnMaintenanceDue       += OnMaintenanceDue;
            GameEvents.OnFuelRefillCompleted  += _ => Push(NotificationKind.Fuel, "Cuve remplie",
                "La livraison de carburant est arrivée au dépôt.");
        }

        public IReadOnlyList<NotificationEntry> Entries => _save.notifications;

        public int UnreadCount
        {
            get { int n = 0; foreach (var e in _save.notifications) if (!e.read) n++; return n; }
        }

        public void MarkAllRead()
        {
            bool changed = false;
            foreach (var e in _save.notifications) if (!e.read) { e.read = true; changed = true; }
            if (changed) Changed?.Invoke();
        }

        public void Clear()
        {
            _save.notifications.Clear();
            Changed?.Invoke();
        }

        /// Ajoute une notification (la plus récente en tête de liste).
        public static void Push(string kind, string title, string body, long timeTicks = 0)
        {
            var sys = ServiceLocator.Get<NotificationSystem>();
            if (sys == null) return;
            long t = timeTicks > 0 ? timeTicks : (ContextTicks > 0 ? ContextTicks : DateTime.UtcNow.Ticks);
            var list = sys._save.notifications;

            // Insertion triée par heure (des événements hors-ligne peuvent arriver dans le désordre).
            int i = 0;
            while (i < list.Count && list[i].timeTicks > t) i++;
            list.Insert(i, new NotificationEntry { timeTicks = t, kind = kind, title = title, body = body });
            while (list.Count > MaxEntries) list.RemoveAt(list.Count - 1);
            Changed?.Invoke();
        }

        private void OnMaintenanceDue(VehicleInstance v)
        {
            var data = ServiceLocator.Get<VehicleCatalog>()?.GetById(v?.vehicleDataId);
            string name = data != null ? data.displayName : "Un camion";
            Push(NotificationKind.Maintenance, "Entretien nécessaire",
                 $"{name} a besoin d'un passage au garage avant de repartir.");
        }

        // ── Aides de texte ──────────────────────────────────────────────────────
        public static string CityName(string id)
            => ServiceLocator.Get<MapSystem>()?.Catalog?.GetById(id)?.displayName ?? id;

        public static string Route(ContractData d)
            => d == null ? "" : $"{CityName(d.originCityId)} → {CityName(d.destinationCityId)}";
    }
}
