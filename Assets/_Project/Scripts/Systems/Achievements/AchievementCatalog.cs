using System.Collections.Generic;

namespace TransportManager.Systems.Achievements
{
    /// Grandeur suivie par un succès. La valeur courante est résolue par
    /// <see cref="AchievementSystem.CurrentValue"/>.
    public enum AchievementMetric
    {
        ContractsDelivered,
        KmDriven,
        CountriesVisited,
        EarningsTotal,
        ToursCompleted,
        PremiumContracts,
        VehiclesOwned,
        CompanyLevel,
        ReputationTier,
        VehicleCategory,
    }

    /// Famille de succès (sert au regroupement / à l'icône dans l'UI).
    public enum AchievementGroup
    {
        Contracts,
        Distance,
        Countries,
        Earnings,
        Fleet,
        Tours,
        Premium,
        Level,
        Reputation,
        Vehicle,
    }

    public sealed class AchievementDef
    {
        public string id;
        public string title;
        public string description;
        public AchievementGroup group;
        public AchievementMetric metric;
        public long   target;
        public string rewardKind;   // dollars | ingots | skill (cf. DailySystem.Grant)
        public int    rewardAmount;

        public AchievementDef(string id, string title, string description, AchievementGroup group,
            AchievementMetric metric, long target, string rewardKind, int rewardAmount)
        {
            this.id = id; this.title = title; this.description = description; this.group = group;
            this.metric = metric; this.target = target; this.rewardKind = rewardKind; this.rewardAmount = rewardAmount;
        }
    }

    /// <summary>
    /// Catalogue figé des succès (G1). Ajouter à la fin : les ids sont sérialisés
    /// dans la save (listes unlocked/claimed) — ne jamais renommer un id existant.
    /// </summary>
    public static class AchievementCatalog
    {
        private static AchievementDef A(string id, string title, string desc, AchievementGroup g,
            AchievementMetric m, long target, string kind, int amount)
            => new AchievementDef(id, title, desc, g, m, target, kind, amount);

        public static readonly IReadOnlyList<AchievementDef> All = new List<AchievementDef>
        {
            // ── Contrats livrés ──────────────────────────────────────────────────
            A("contracts_1",    "Premier client",        "Livrer ton premier contrat",        AchievementGroup.Contracts, AchievementMetric.ContractsDelivered, 1,    "dollars", 1000),
            A("contracts_10",   "Transporteur",          "Livrer 10 contrats",                AchievementGroup.Contracts, AchievementMetric.ContractsDelivered, 10,   "dollars", 3000),
            A("contracts_50",   "Habitué de la route",   "Livrer 50 contrats",                AchievementGroup.Contracts, AchievementMetric.ContractsDelivered, 50,   "ingots",  3),
            A("contracts_100",  "Centurion du fret",     "Livrer 100 contrats",               AchievementGroup.Contracts, AchievementMetric.ContractsDelivered, 100,  "ingots",  5),
            A("contracts_500",  "Maître logisticien",    "Livrer 500 contrats",               AchievementGroup.Contracts, AchievementMetric.ContractsDelivered, 500,  "skill",   2),
            A("contracts_1000", "Légende de la livraison","Livrer 1 000 contrats",            AchievementGroup.Contracts, AchievementMetric.ContractsDelivered, 1000, "skill",   3),

            // ── Distance cumulée ─────────────────────────────────────────────────
            A("km_1000",     "Sur la route",        "Parcourir 1 000 km",       AchievementGroup.Distance, AchievementMetric.KmDriven, 1000,    "dollars", 2000),
            A("km_10000",    "Grand voyageur",      "Parcourir 10 000 km",      AchievementGroup.Distance, AchievementMetric.KmDriven, 10000,   "ingots",  2),
            A("km_100000",   "Tour du monde",       "Parcourir 100 000 km",     AchievementGroup.Distance, AchievementMetric.KmDriven, 100000,  "ingots",  5),
            A("km_1000000",  "Million de bornes",   "Parcourir 1 000 000 km",   AchievementGroup.Distance, AchievementMetric.KmDriven, 1000000, "skill",   3),

            // ── Pays visités ─────────────────────────────────────────────────────
            A("countries_5",   "Première frontière",  "Livrer dans 5 pays",   AchievementGroup.Countries, AchievementMetric.CountriesVisited, 5,   "dollars", 2500),
            A("countries_25",  "Routier international","Livrer dans 25 pays",  AchievementGroup.Countries, AchievementMetric.CountriesVisited, 25,  "ingots",  3),
            A("countries_50",  "Globe-trotter",       "Livrer dans 50 pays",  AchievementGroup.Countries, AchievementMetric.CountriesVisited, 50,  "ingots",  5),
            A("countries_100", "Citoyen du monde",    "Livrer dans 100 pays", AchievementGroup.Countries, AchievementMetric.CountriesVisited, 100, "skill",   3),

            // ── Gains cumulés ────────────────────────────────────────────────────
            A("earn_100k", "Premiers profits",      "Gagner 100 000 $ au total",    AchievementGroup.Earnings, AchievementMetric.EarningsTotal, 100000,   "dollars", 5000),
            A("earn_1m",   "Millionnaire",          "Gagner 1 000 000 $ au total",  AchievementGroup.Earnings, AchievementMetric.EarningsTotal, 1000000,  "ingots",  5),
            A("earn_10m",  "Magnat du transport",   "Gagner 10 000 000 $ au total", AchievementGroup.Earnings, AchievementMetric.EarningsTotal, 10000000, "skill",   3),

            // ── Flotte ───────────────────────────────────────────────────────────
            A("fleet_5",  "Petite flotte",  "Posséder 5 véhicules",  AchievementGroup.Fleet, AchievementMetric.VehiclesOwned, 5,  "dollars", 3000),
            A("fleet_10", "Grande flotte",  "Posséder 10 véhicules", AchievementGroup.Fleet, AchievementMetric.VehiclesOwned, 10, "ingots",  3),
            A("fleet_25", "Empire roulant", "Posséder 25 véhicules", AchievementGroup.Fleet, AchievementMetric.VehiclesOwned, 25, "skill",   2),

            // ── Tournées multi-arrêts ────────────────────────────────────────────
            A("tour_10", "Tournée organisée", "Livrer 10 tournées à escales", AchievementGroup.Tours, AchievementMetric.ToursCompleted, 10, "ingots", 2),
            A("tour_50", "Roi de la tournée",  "Livrer 50 tournées à escales", AchievementGroup.Tours, AchievementMetric.ToursCompleted, 50, "skill",  2),

            // ── Contrats difficiles ──────────────────────────────────────────────
            A("premium_10", "Amateur de défis", "Livrer 10 contrats difficiles", AchievementGroup.Premium, AchievementMetric.PremiumContracts, 10, "ingots", 2),
            A("premium_50", "Risque-tout",      "Livrer 50 contrats difficiles", AchievementGroup.Premium, AchievementMetric.PremiumContracts, 50, "skill",  2),

            // ── Niveau d'entreprise ──────────────────────────────────────────────
            A("level_5",  "Entreprise en croissance", "Atteindre le niveau 5",  AchievementGroup.Level, AchievementMetric.CompanyLevel, 5,  "dollars", 4000),
            A("level_10", "PME prospère",             "Atteindre le niveau 10", AchievementGroup.Level, AchievementMetric.CompanyLevel, 10, "ingots",  4),
            A("level_25", "Multinationale",           "Atteindre le niveau 25", AchievementGroup.Level, AchievementMetric.CompanyLevel, 25, "skill",   3),

            // ── Réputation ───────────────────────────────────────────────────────
            A("rep_legend", "Légendaire", "Atteindre le palier de réputation Légendaire", AchievementGroup.Reputation, AchievementMetric.ReputationTier, 4, "skill", 3),

            // ── Véhicules d'exception (catégorie possédée) ───────────────────────
            A("cat_train", "Train routier", "Posséder un train routier",        AchievementGroup.Vehicle, AchievementMetric.VehicleCategory, 9,  "ingots", 4),
            A("cat_mega",  "Méga-convoi",   "Posséder un méga-convoi hors-norme", AchievementGroup.Vehicle, AchievementMetric.VehicleCategory, 10, "skill",  3),
        };

        public static AchievementDef GetById(string id)
        {
            foreach (var a in All) if (a.id == id) return a;
            return null;
        }
    }
}
