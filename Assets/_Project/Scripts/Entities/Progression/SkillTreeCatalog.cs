using System.Collections.Generic;
using TransportManager.Enums;

namespace TransportManager.Entities.Progression
{
    /// <summary>
    /// Catalogue statique de l'arbre de compétences.
    ///
    /// L'arbre part d'un tronc unique puis se sépare en 3 branches (Dépôt / RH / Essence).
    /// Chaque branche est ici une chaîne linéaire de 4 augments (tier 1 → 4) : pour en
    /// débloquer un, il faut avoir débloqué son prérequis et disposer d'assez de points.
    ///
    /// Tout est conçu pour être édité facilement : ajoute/retire des nœuds, change les
    /// coûts (cost), les effets (effect) ou les magnitudes (fraction pour les %).
    /// </summary>
    public static class SkillTreeCatalog
    {
        // Combien de points de compétence sont accordés à chaque niveau d'entreprise gagné.
        public const int PointsPerCompanyLevel = 1;

        private static List<SkillNodeDefinition> _all;

        public static IReadOnlyList<SkillNodeDefinition> All
        {
            get
            {
                if (_all == null) _all = Build();
                return _all;
            }
        }

        public static SkillNodeDefinition GetById(string id)
        {
            foreach (var n in All) if (n.id == id) return n;
            return null;
        }

        public static IEnumerable<SkillNodeDefinition> InBranch(SkillBranch branch)
        {
            foreach (var n in All) if (n.branch == branch) yield return n;
        }

        private static List<SkillNodeDefinition> Build()
        {
            var list = new List<SkillNodeDefinition>();
            list.AddRange(BuildDepot());
            list.AddRange(BuildHr());
            list.AddRange(BuildFuel());
            return list;
        }

        // Coût croissant par profondeur dans la branche.
        private static int CostForTier(int tier) => 1 + (tier - 1) / 2; // 1,1,2,2,3,3,4,4,5,5

        private static SkillNodeDefinition Node(string id, SkillBranch branch, string title, string desc,
            string prereq, SkillEffectType effect, float magnitude, int tier)
            => new SkillNodeDefinition(id, branch, title, desc, CostForTier(tier), prereq, effect, magnitude, tier);

        // ── Branche DÉPÔT (logistique) ────────────────────────────────────────────
        // Arbre ramifié : racine → 2 → (2 / 3) → … Les augments les plus forts sont
        // au plus profond (les plus durs à atteindre).
        private static List<SkillNodeDefinition> BuildDepot()
        {
            const SkillBranch B = SkillBranch.Depot;
            var E = SkillEffectType.ExtraVehicleSlots;
            var U = SkillEffectType.DepotUpgradeCostReduction;
            var R = SkillEffectType.RepairCostReduction;
            var W = SkillEffectType.ContractRewardBonus;
            return new List<SkillNodeDefinition>
            {
                Node("depot_root", B, "Quai supplémentaire", "+1 emplacement de véhicule au dépôt.",          null,        E, 1f,    1),
                // split 2
                Node("depot_a",    B, "Négociation BTP",     "-8 % sur le coût d'agrandissement du dépôt.",   "depot_root",U, 0.08f, 2),
                Node("depot_b",    B, "Atelier interne",     "-8 % sur le coût de réparation des véhicules.", "depot_root",R, 0.08f, 2),
                // depot_a split 2
                Node("depot_a1",   B, "Centrale d'achat",    "-10 % supplémentaires sur l'agrandissement.",   "depot_a",   U, 0.10f, 3),
                Node("depot_a2",   B, "Contrats premium",    "+4 % sur la récompense des contrats.",          "depot_a",   W, 0.04f, 3),
                // depot_b split 3
                Node("depot_b1",   B, "Pièces d'occasion",   "-10 % supplémentaires sur la réparation.",      "depot_b",   R, 0.10f, 3),
                Node("depot_b2",   B, "Second quai",         "+1 emplacement de véhicule supplémentaire.",    "depot_b",   E, 1f,    3),
                Node("depot_b3",   B, "Primes de livraison", "+4 % sur la récompense des contrats.",          "depot_b",   W, 0.04f, 3),
                // depth 4
                Node("depot_a1a",  B, "Achats groupés",      "-12 % supplémentaires sur l'agrandissement.",   "depot_a1",  U, 0.12f, 4),
                Node("depot_a2a",  B, "Clients fidèles",     "+6 % sur la récompense des contrats.",          "depot_a2",  W, 0.06f, 4),
                Node("depot_b1a",  B, "Mécanos chevronnés",  "-12 % supplémentaires sur la réparation.",      "depot_b1",  R, 0.12f, 4),
                Node("depot_b2a",  B, "Troisième quai",      "+1 emplacement de véhicule supplémentaire.",    "depot_b2",  E, 1f,    4),
                Node("depot_b2b",  B, "Logistique fluide",   "+5 % sur la récompense des contrats.",          "depot_b2",  W, 0.05f, 4),
                // depth 5 (les plus forts)
                Node("depot_a1a1", B, "Empire immobilier",   "-18 % supplémentaires sur l'agrandissement.",   "depot_a1a", U, 0.18f, 5),
                Node("depot_a2a1", B, "Contrats en or",      "+12 % sur la récompense des contrats.",         "depot_a2a", W, 0.12f, 5),
                Node("depot_b2a1", B, "Hangar étendu",       "+2 emplacements de véhicule supplémentaires.",  "depot_b2a", E, 2f,    5),
            };
        }

        // ── Branche RH ──────────────────────────────────────────────────────────────
        private static List<SkillNodeDefinition> BuildHr()
        {
            const SkillBranch B = SkillBranch.Hr;
            var Wg = SkillEffectType.DriverWageReduction;
            var Xp = SkillEffectType.DriverXpGainBonus;
            var Fa = SkillEffectType.FatigueReduction;
            var Rc = SkillEffectType.RecruitmentLevelBonus;
            return new List<SkillNodeDefinition>
            {
                Node("hr_root", B, "Contrats avantageux",   "-8 % sur les salaires des conducteurs.",        null,     Wg, 0.08f, 1),
                // split 2
                Node("hr_a",    B, "Formation continue",    "+20 % d'XP gagnée par les conducteurs.",        "hr_root",Xp, 0.20f, 2),
                Node("hr_b",    B, "Pauses régulières",     "-15 % de fatigue accumulée sur les trajets.",   "hr_root",Fa, 0.15f, 2),
                // hr_a split 3
                Node("hr_a1",   B, "Académie interne",      "+25 % supplémentaires d'XP conducteurs.",       "hr_a",   Xp, 0.25f, 3),
                Node("hr_a2",   B, "Accords syndicaux",     "-8 % supplémentaires sur les salaires.",        "hr_a",   Wg, 0.08f, 3),
                Node("hr_a3",   B, "Cabinet de recrutement","Candidats du vivier : niveau minimum +1.",      "hr_a",   Rc, 1f,    3),
                // hr_b split 2
                Node("hr_b1",   B, "Sièges ergonomiques",   "-20 % supplémentaires de fatigue.",             "hr_b",   Fa, 0.20f, 3),
                Node("hr_b2",   B, "Intéressement",         "-8 % supplémentaires sur les salaires.",        "hr_b",   Wg, 0.08f, 3),
                // depth 4
                Node("hr_a1a",  B, "Mentorat",              "+30 % supplémentaires d'XP conducteurs.",       "hr_a1",  Xp, 0.30f, 4),
                Node("hr_a3a",  B, "Chasseur de têtes",     "Candidats du vivier : niveau minimum +1.",      "hr_a3",  Rc, 1f,    4),
                Node("hr_b1a",  B, "Cabines confort",       "-25 % supplémentaires de fatigue.",             "hr_b1",  Fa, 0.25f, 4),
                Node("hr_b2a",  B, "Participation",         "-10 % supplémentaires sur les salaires.",       "hr_b2",  Wg, 0.10f, 4),
                Node("hr_b2b",  B, "Rotation des équipes",  "-15 % supplémentaires de fatigue.",             "hr_b2",  Fa, 0.15f, 4),
                // depth 5 (les plus forts)
                Node("hr_a1a1", B, "Culture d'élite",       "+40 % supplémentaires d'XP conducteurs.",       "hr_a1a", Xp, 0.40f, 5),
                Node("hr_a3a1", B, "Réseau d'élite",        "Candidats du vivier : niveau minimum +2.",      "hr_a3a", Rc, 2f,    5),
                Node("hr_b2a1", B, "Convention dorée",      "-12 % supplémentaires sur les salaires.",       "hr_b2a", Wg, 0.12f, 5),
            };
        }

        // ── Branche ESSENCE ───────────────────────────────────────────────────────
        private static List<SkillNodeDefinition> BuildFuel()
        {
            const SkillBranch B = SkillBranch.Fuel;
            var Pr = SkillEffectType.FuelPriceReduction;
            var Co = SkillEffectType.FuelConsumptionReduction;
            var Ca = SkillEffectType.StationCapacityBonus;
            var Re = SkillEffectType.RefillSpeedBonus;
            var Sp = SkillEffectType.TripSpeedBonus;
            return new List<SkillNodeDefinition>
            {
                Node("fuel_root", B, "Carte carburant pro", "-10 % sur le prix d'achat du carburant.",       null,      Pr, 0.10f, 1),
                // split 2
                Node("fuel_a",    B, "Éco-conduite",        "-6 % de consommation de carburant.",            "fuel_root",Co,0.06f, 2),
                Node("fuel_b",    B, "Cuve renforcée",      "+20 % de capacité de stockage de la station.",  "fuel_root",Ca,0.20f, 2),
                // fuel_a split 3
                Node("fuel_a1",   B, "Conduite souple",     "-8 % supplémentaires de consommation.",         "fuel_a",  Co, 0.08f, 3),
                Node("fuel_a2",   B, "Moteurs optimisés",   "+5 % de vitesse sur les trajets.",              "fuel_a",  Sp, 0.05f, 3),
                Node("fuel_a3",   B, "Contrat grossiste",   "-10 % supplémentaires sur le prix.",            "fuel_a",  Pr, 0.10f, 3),
                // fuel_b split 2
                Node("fuel_b1",   B, "Double cuve",         "+25 % supplémentaires de capacité.",            "fuel_b",  Ca, 0.25f, 3),
                Node("fuel_b2",   B, "Pompe rapide",        "+20 % de vitesse de remplissage.",              "fuel_b",  Re, 0.20f, 3),
                // depth 4
                Node("fuel_a1a",  B, "Aérodynamisme",       "-10 % supplémentaires de consommation.",        "fuel_a1", Co, 0.10f, 4),
                Node("fuel_a2a",  B, "Flotte sport",        "+8 % de vitesse sur les trajets.",              "fuel_a2", Sp, 0.08f, 4),
                Node("fuel_a3a",  B, "Raffinerie partenaire","-10 % supplémentaires sur le prix.",           "fuel_a3", Pr, 0.10f, 4),
                Node("fuel_b1a",  B, "Réservoir géant",     "+30 % supplémentaires de capacité.",            "fuel_b1", Ca, 0.30f, 4),
                Node("fuel_b2a",  B, "Livraison express",   "+25 % supplémentaires de remplissage.",         "fuel_b2", Re, 0.25f, 4),
                // depth 5 (les plus forts)
                Node("fuel_a1a1", B, "Hybridation",         "-12 % supplémentaires de consommation.",        "fuel_a1a",Co, 0.12f, 5),
                Node("fuel_a2a1", B, "Pilotes pro",         "+10 % de vitesse sur les trajets.",             "fuel_a2a",Sp, 0.10f, 5),
                Node("fuel_b2a1", B, "Pipeline privé",      "+30 % supplémentaires de remplissage.",         "fuel_b2a",Re, 0.30f, 5),
            };
        }
    }
}
