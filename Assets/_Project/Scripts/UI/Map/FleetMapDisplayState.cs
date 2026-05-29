using System;
using System.Collections.Generic;

namespace TransportManager.UI.Map
{
    /// État global d'affichage des trajets actifs sur la carte.
    /// Par défaut tout est visible ; l'œil de la liste de flotte masque/réaffiche
    /// le tracé + l'icône camion d'un contrat précis.
    public static class FleetMapDisplayState
    {
        private static readonly HashSet<string> _hidden = new HashSet<string>();

        /// Notifie l'overlay carte qu'un contrat a été masqué/réaffiché.
        public static event Action OnChanged;

        public static bool IsVisible(string contractInstanceId)
            => !string.IsNullOrEmpty(contractInstanceId) && !_hidden.Contains(contractInstanceId);

        public static void SetVisible(string contractInstanceId, bool visible)
        {
            if (string.IsNullOrEmpty(contractInstanceId)) return;
            bool changed = visible ? _hidden.Remove(contractInstanceId) : _hidden.Add(contractInstanceId);
            if (changed) OnChanged?.Invoke();
        }

        /// Bascule la visibilité et renvoie le nouvel état (true = visible).
        public static bool Toggle(string contractInstanceId)
        {
            bool now = !IsVisible(contractInstanceId);
            SetVisible(contractInstanceId, now);
            return now;
        }
    }
}
