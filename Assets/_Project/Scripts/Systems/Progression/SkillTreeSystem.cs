using UnityEngine;
using TransportManager.Entities.Progression;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Save;

namespace TransportManager.Systems.Progression
{
    /// <summary>
    /// Gère les points de compétence (gagnés en montant de niveau d'entreprise) et le
    /// déblocage des nœuds de l'arbre. Expose des accesseurs de modificateurs agrégés
    /// que les autres systèmes interrogent (salaires, carburant, dépôt, etc.).
    /// </summary>
    public class SkillTreeSystem
    {
        private readonly GameSaveData _save;

        public SkillTreeSystem(GameSaveData save)
        {
            _save = save;
            if (_save.skillTree == null) _save.skillTree = new SkillTreeState();
            SyncEarnedPoints();
        }

        private SkillTreeState State => _save.skillTree;

        public int AvailablePoints => State.availablePoints;
        public int SpentPoints
        {
            get
            {
                int spent = 0;
                foreach (var id in State.unlockedNodeIds)
                {
                    var def = SkillTreeCatalog.GetById(id);
                    if (def != null) spent += def.cost;
                }
                return spent;
            }
        }

        public bool IsUnlocked(string nodeId) => State.unlockedNodeIds.Contains(nodeId);

        // ── Déblocage ────────────────────────────────────────────────────────────

        public bool CanUnlock(string nodeId, out string reason)
        {
            reason = null;
            var def = SkillTreeCatalog.GetById(nodeId);
            if (def == null) { reason = "Nœud inconnu."; return false; }
            if (IsUnlocked(nodeId)) { reason = "Déjà débloqué."; return false; }
            if (!def.IsBranchRoot && !IsUnlocked(def.prerequisiteId))
            {
                reason = "Prérequis non débloqué.";
                return false;
            }
            if (State.availablePoints < def.cost)
            {
                reason = "Points de compétence insuffisants.";
                return false;
            }
            return true;
        }

        public bool TryUnlock(string nodeId)
        {
            if (!CanUnlock(nodeId, out _)) return false;
            var def = SkillTreeCatalog.GetById(nodeId);
            State.availablePoints -= def.cost;
            State.unlockedNodeIds.Add(nodeId);
            GameEvents.RaiseSkillNodeUnlocked(nodeId);
            GameEvents.RaiseSkillPointsChanged(State.availablePoints);
            return true;
        }

        // ── Octroi de points ───────────────────────────────────────────────────────

        public void GrantPoints(int amount)
        {
            if (amount <= 0) return;
            State.availablePoints += amount;
            State.totalEarnedPoints += amount;
            GameEvents.RaiseSkillPointsChanged(State.availablePoints);
        }

        /// <summary>Appelé lors d'une montée de niveau d'entreprise.</summary>
        public void OnCompanyLevelChanged(int oldLevel, int newLevel)
        {
            if (newLevel > oldLevel)
                GrantPoints((newLevel - oldLevel) * SkillTreeCatalog.PointsPerCompanyLevel);
        }

        /// <summary>
        /// Crédite rétroactivement les points correspondant au niveau actuel (sans
        /// double-compter), utile pour les sauvegardes antérieures à la fonctionnalité.
        /// </summary>
        public void SyncEarnedPoints()
        {
            int level = XpCurve.CompanyLevelFromXp(_save.companyXp);
            int expected = Mathf.Max(0, level - 1) * SkillTreeCatalog.PointsPerCompanyLevel;
            if (State.totalEarnedPoints < expected)
            {
                int diff = expected - State.totalEarnedPoints;
                State.totalEarnedPoints = expected;
                State.availablePoints += diff;
            }
        }

        // ── Modificateurs agrégés ───────────────────────────────────────────────────

        /// <summary>Somme des magnitudes (fraction) de tous les nœuds débloqués d'un effet donné.</summary>
        public float Pct(SkillEffectType effect)
        {
            float total = 0f;
            foreach (var id in State.unlockedNodeIds)
            {
                var def = SkillTreeCatalog.GetById(id);
                if (def != null && def.effect == effect) total += def.magnitude;
            }
            return total;
        }

        /// <summary>Somme entière des magnitudes (effets « flat ») de tous les nœuds débloqués.</summary>
        public int Flat(SkillEffectType effect) => Mathf.RoundToInt(Pct(effect));
    }
}
