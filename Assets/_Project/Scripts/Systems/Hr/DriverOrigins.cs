using System.Collections.Generic;
using UnityEngine;
using TransportManager.Entities.Drivers;

namespace TransportManager.Systems.Hr
{
    /// <summary>
    /// Origines des pilotes : chaque pilote a un GENRE ("m" / "f") et une FAMILLE d'origine,
    /// qui fixent ensemble son prénom, son nom, sa nationalité et son portrait
    /// (Resources/UI/Drivers/&lt;famille&gt;_&lt;genre&gt;_&lt;n&gt;.png) → tout reste cohérent.
    ///
    /// Les 5 grandes régions (Europe de l'Ouest / du Nord / de l'Est, Afrique, Asie) sont
    /// découpées en 7 familles pour que noms et visages collent : l'Afrique en Nord
    /// (prénoms arabes, + Moyen-Orient) et subsaharienne, l'Asie en Est et Sud.
    /// Les Amériques et l'Océanie sont rattachées à l'Europe de l'Ouest (prénoms anglais,
    /// espagnols, portugais).
    ///
    /// Le vivier de recrutement privilégie la famille du pays où le joueur a installé son
    /// entreprise : un joueur qui démarre a surtout des pilotes de sa région.
    /// </summary>
    public static class DriverOrigins
    {
        public const string EuWest = "eu_west", EuNorth = "eu_north", EuEast = "eu_east",
                            AfricaNorth = "africa_north", AfricaSub = "africa_sub",
                            AsiaEast = "asia_east", AsiaSouth = "asia_south";

        // Part des candidats issus de la région du joueur (le reste : toutes origines).
        private const float HomeFamilyChance = 0.6f;

        private class Family
        {
            public string label;
            public string[] male, female, last, nationalities;
        }

        private static readonly Dictionary<string, Family> Families = new Dictionary<string, Family>
        {
            [EuWest] = new Family
            {
                label = "Europe de l'Ouest",
                male   = new[] { "Jean", "Pierre", "Marc", "Luc", "Paul", "Hugo", "Théo", "Antoine", "Carlos", "Miguel", "João", "Pedro", "Marco", "Luca", "Thomas", "James", "Liam", "Diego" },
                female = new[] { "Marie", "Sophie", "Camille", "Léa", "Julie", "Claire", "Élise", "Lucía", "Sofia", "Inês", "Giulia", "Chiara", "Emma", "Charlotte", "Ana" },
                last   = new[] { "Martin", "Bernard", "Dubois", "Moreau", "Laurent", "Garcia", "Fernández", "Silva", "Santos", "Rossi", "Bianchi", "Smith", "Murphy", "Peeters", "Lefebvre", "Rodríguez", "Ferreira", "Romano" },
                nationalities = new[] { "Français", "Belge", "Suisse", "Espagnol", "Portugais", "Italien", "Britannique", "Irlandais" },
            },
            [EuNorth] = new Family
            {
                label = "Europe du Nord",
                male   = new[] { "Klaus", "Hans", "Stefan", "Lukas", "Lars", "Erik", "Nils", "Mikkel", "Jonas", "Henrik", "Sven", "Mattias" },
                female = new[] { "Greta", "Anna", "Ingrid", "Astrid", "Freya", "Lena", "Sanna", "Maja", "Hanna" },
                last   = new[] { "Müller", "Schmidt", "Fischer", "Weber", "Hansen", "Johansson", "Andersen", "Nielsen", "Lindqvist", "Virtanen", "de Vries", "Bakker" },
                nationalities = new[] { "Allemand", "Autrichien", "Néerlandais", "Danois", "Suédois", "Norvégien", "Finlandais" },
            },
            [EuEast] = new Family
            {
                label = "Europe de l'Est",
                male   = new[] { "Pavel", "Andrei", "Ivan", "Nikolai", "Boris", "Piotr", "Tomasz", "Dmitri", "Marek", "Vlad", "Bogdan", "Jakub" },
                female = new[] { "Olga", "Natalia", "Katarzyna", "Irina", "Elena", "Anastasia", "Marta", "Ioana", "Svetlana" },
                last   = new[] { "Kowalski", "Nowak", "Ivanov", "Petrov", "Popescu", "Horváth", "Novák", "Kovač", "Shevchenko", "Smirnov", "Dimitrov", "Wiśniewski" },
                nationalities = new[] { "Polonais", "Roumain", "Russe", "Ukrainien", "Tchèque", "Hongrois", "Bulgare", "Serbe", "Croate" },
            },
            [AfricaNorth] = new Family
            {
                label = "Afrique du Nord",
                male   = new[] { "Mohamed", "Karim", "Hakim", "Samir", "Youssef", "Omar", "Rachid", "Bilal", "Mehdi", "Nabil", "Tarek", "Walid" },
                female = new[] { "Yasmine", "Amira", "Fatima", "Leïla", "Nadia", "Salma", "Imane", "Rania", "Samira" },
                last   = new[] { "Benali", "El Amrani", "Haddad", "Mansouri", "Bouazizi", "Khalil", "Saïdi", "Belkacem", "Farouk", "Hamdi", "Nasser", "Zeroual" },
                nationalities = new[] { "Algérien", "Marocain", "Tunisien", "Égyptien", "Libanais", "Jordanien" },
            },
            [AfricaSub] = new Family
            {
                label = "Afrique subsaharienne",
                male   = new[] { "Moussa", "Amadou", "Kwame", "Kofi", "Tunde", "Abdou", "Ibrahim", "Sékou", "Chinedu", "Jabari", "Thabo", "Emeka" },
                female = new[] { "Aminata", "Aïcha", "Ama", "Zainab", "Fatoumata", "Ngozi", "Adwoa", "Mariama", "Thandiwe" },
                last   = new[] { "Traoré", "Diallo", "Koné", "Mensah", "Okafor", "Ndiaye", "Sow", "Keïta", "Mbeki", "Adeyemi", "Kamara", "Mwangi" },
                nationalities = new[] { "Sénégalais", "Malien", "Ivoirien", "Nigérian", "Ghanéen", "Camerounais", "Kényan", "Sud-Africain" },
            },
            [AsiaEast] = new Family
            {
                label = "Asie de l'Est",
                male   = new[] { "Wei", "Hiroshi", "Kenji", "Minh", "Joon", "Takeshi", "Somchai", "Hao", "Jun", "Tuan", "Daisuke", "Min-ho" },
                female = new[] { "Mei", "Yuki", "Sakura", "Linh", "Ji-woo", "Aiko", "Mai", "Xin", "Hana" },
                last   = new[] { "Wang", "Li", "Zhang", "Chen", "Nguyen", "Tanaka", "Sato", "Kim", "Park", "Suzuki", "Tran", "Liu" },
                nationalities = new[] { "Chinois", "Japonais", "Coréen", "Vietnamien", "Thaïlandais", "Philippin" },
            },
            [AsiaSouth] = new Family
            {
                label = "Asie du Sud",
                male   = new[] { "Raj", "Arjun", "Vikram", "Rahul", "Amit", "Imran", "Farhan", "Sanjay", "Ravi", "Anil", "Kiran", "Faisal" },
                female = new[] { "Priya", "Anjali", "Aisha", "Neha", "Kavya", "Sana", "Pooja", "Divya", "Meera" },
                last   = new[] { "Singh", "Patel", "Sharma", "Kumar", "Khan", "Gupta", "Reddy", "Das", "Iyer", "Chowdhury", "Malik", "Joshi" },
                nationalities = new[] { "Indien", "Pakistanais", "Bangladais", "Sri-Lankais", "Népalais" },
            },
        };

        // Pays du catalogue de villes → famille d'origine (inconnu → Europe de l'Ouest).
        private static readonly Dictionary<string, string> CountryFamily = BuildCountryMap();

        private static Dictionary<string, string> BuildCountryMap()
        {
            var m = new Dictionary<string, string>();
            void Add(string family, params string[] countries) { foreach (var c in countries) m[c] = family; }

            Add(EuWest, "France", "Belgique", "Luxembourg", "Suisse", "Espagne", "Portugal", "Italie", "Royaume-Uni", "Irlande", "Malte",
                // Amériques & Océanie : prénoms anglais / espagnols / portugais
                "États-Unis", "Canada", "Mexique", "Guatemala", "Honduras", "Salvador", "Nicaragua", "Costa Rica", "Panama", "Cuba",
                "Haïti", "République Dominicaine", "Jamaïque", "Colombie", "Venezuela", "Équateur", "Pérou", "Bolivie", "Brésil",
                "Paraguay", "Uruguay", "Argentine", "Chili", "Australie", "Nouvelle-Zélande", "Fidji", "Papouasie-N.-G.");
            Add(EuNorth, "Allemagne", "Autriche", "Pays-Bas", "Danemark", "Suède", "Norvège", "Finlande", "Islande");
            Add(EuEast, "Pologne", "République Tchèque", "Slovaquie", "Hongrie", "Roumanie", "Bulgarie", "Grèce", "Chypre", "Croatie",
                "Slovénie", "Serbie", "Bosnie-H.", "Monténégro", "Albanie", "Macédoine du Nord", "Kosovo", "Moldavie", "Ukraine",
                "Biélorussie", "Lituanie", "Lettonie", "Estonie", "Russie", "Géorgie", "Arménie", "Azerbaïdjan", "Turquie");
            Add(AfricaNorth, "Algérie", "Maroc", "Tunisie", "Libye", "Égypte", "Mauritanie", "Soudan",
                // Moyen-Orient : prénoms arabes
                "Arabie Saoudite", "Émirats Arabes Unis", "Qatar", "Koweït", "Bahreïn", "Oman", "Yémen", "Jordanie", "Liban",
                "Syrie", "Irak", "Israël");
            Add(AfricaSub, "Sénégal", "Mali", "Burkina Faso", "Niger", "Tchad", "Guinée", "Sierra Leone", "Liberia", "Côte d'Ivoire",
                "Ghana", "Togo", "Bénin", "Nigeria", "Cameroun", "Gabon", "Congo", "RD Congo", "Angola", "Zambie", "Zimbabwe",
                "Malawi", "Mozambique", "Tanzanie", "Kenya", "Ouganda", "Rwanda", "Éthiopie", "Somalie", "Madagascar", "Maurice",
                "Namibie", "Botswana", "Afrique du Sud");
            Add(AsiaEast, "Chine", "Japon", "Corée du Sud", "Corée du Nord", "Mongolie", "Vietnam", "Laos", "Cambodge", "Thaïlande",
                "Birmanie", "Malaisie", "Singapour", "Indonésie", "Philippines");
            Add(AsiaSouth, "Inde", "Pakistan", "Bangladesh", "Sri Lanka", "Népal", "Afghanistan", "Iran", "Kazakhstan",
                "Ouzbékistan", "Tadjikistan", "Kirghizistan", "Turkménistan");
            return m;
        }

        public static string FamilyForCountry(string country) =>
            !string.IsNullOrEmpty(country) && CountryFamily.TryGetValue(country, out var f) ? f : EuWest;

        public static string Label(string family) =>
            family != null && Families.TryGetValue(family, out var f) ? f.label : "";

        /// <summary>Famille du joueur = celle du pays de la ville la plus proche de son entreprise.</summary>
        public static string HomeFamily()
        {
            // Le dépôt du joueur (entrée « maison » du catalogue) porte le pays de la ville la plus proche.
            var map = Core.ServiceLocator.Get<Map.MapSystem>();
            string country = map?.Catalog?.Home?.country;
            if (string.IsNullOrEmpty(country))
            {
                var c = Core.GameManager.Instance?.Save?.company;
                if (map != null && c != null && c.hasLocationCoordinates)
                    country = map.GetNearestCity(c.locationLatitude, c.locationLongitude)?.country;
            }
            return FamilyForCountry(country);
        }

        /// <summary>Tire une origine + un genre et remplit nom, prénom et nationalité du pilote.</summary>
        public static void AssignIdentity(DriverInstance d)
        {
            string family;
            if (Random.value < HomeFamilyChance) family = HomeFamily();
            else
            {
                var keys = new List<string>(Families.Keys);
                family = keys[Random.Range(0, keys.Count)];
            }
            var f = Families[family];
            d.origin = family;
            d.gender = Random.value < 0.5f ? "m" : "f";
            var first = d.gender == "f" ? f.female : f.male;
            d.firstName   = first[Random.Range(0, first.Length)];
            d.lastName    = f.last[Random.Range(0, f.last.Length)];
            d.nationality = f.nationalities[Random.Range(0, f.nationalities.Length)];
        }

        /// <summary>
        /// Anciennes sauvegardes : les pilotes n'ont ni genre ni origine. On les déduit de
        /// l'ancien prénom (sans changer le nom affiché). Renvoie true si un pilote a été complété.
        /// </summary>
        public static bool EnsureIdentity(DriverInstance d)
        {
            if (d == null || (!string.IsNullOrEmpty(d.origin) && !string.IsNullOrEmpty(d.gender))) return false;
            foreach (var kv in Families)
            {
                if (System.Array.IndexOf(kv.Value.female, d.firstName) >= 0) { d.origin = kv.Key; d.gender = "f"; return true; }
                if (System.Array.IndexOf(kv.Value.male, d.firstName) >= 0)   { d.origin = kv.Key; d.gender = "m"; return true; }
            }
            // Prénoms des anciennes listes absents des nouvelles.
            switch (d.firstName)
            {
                case "Émile": d.origin = EuWest; d.gender = "m"; return true;
                case "Anne":  d.origin = EuWest; d.gender = "f"; return true;
            }
            d.origin = EuWest;
            d.gender = "m";
            return true;
        }
    }
}
