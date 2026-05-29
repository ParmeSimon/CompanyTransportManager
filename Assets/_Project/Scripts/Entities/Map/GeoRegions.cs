using System.Collections.Generic;

namespace TransportManager.Entities.Map
{
    /// <summary>
    /// Données géographiques statiques pour le déblocage progressif des contrats :
    /// continent de chaque pays + pays limitrophes (frontières terrestres, complétées
    /// de quelques voisins maritimes proches pour les îles et détroits).
    ///
    /// Les noms de pays doivent correspondre EXACTEMENT au champ `country` du
    /// CityCatalog. Pour étendre le jeu à d'autres continents (Asie, Amérique…),
    /// ajoute simplement les pays ici avec leur continent et leurs voisins.
    /// </summary>
    public static class GeoRegions
    {
        private const string Europe = "Europe";

        // Pays → continent. Tous les pays actuels du catalogue sont européens.
        private static readonly Dictionary<string, string> _continent = new Dictionary<string, string>
        {
            { "France", Europe }, { "Belgique", Europe }, { "Pays-Bas", Europe },
            { "Allemagne", Europe }, { "Italie", Europe }, { "Espagne", Europe },
            { "Portugal", Europe }, { "Suisse", Europe }, { "Autriche", Europe },
            { "Luxembourg", Europe }, { "Royaume-Uni", Europe }, { "Irlande", Europe },
            { "Danemark", Europe }, { "Suède", Europe }, { "Norvège", Europe },
            { "Finlande", Europe }, { "Pologne", Europe }, { "République Tchèque", Europe },
            { "Slovaquie", Europe }, { "Hongrie", Europe }, { "Roumanie", Europe },
            { "Bulgarie", Europe }, { "Grèce", Europe }, { "Croatie", Europe },
            { "Slovénie", Europe }, { "Serbie", Europe }, { "Bosnie-H.", Europe },
            { "Monténégro", Europe }, { "Albanie", Europe }, { "Macédoine du Nord", Europe },
            { "Kosovo", Europe }, { "Moldavie", Europe }, { "Ukraine", Europe },
            { "Biélorussie", Europe }, { "Lituanie", Europe }, { "Lettonie", Europe },
            { "Estonie", Europe }, { "Islande", Europe }, { "Malte", Europe }, { "Chypre", Europe },
        };

        // Pays → pays limitrophes (terre + voisins maritimes proches pour les îles).
        private static readonly Dictionary<string, string[]> _borders = new Dictionary<string, string[]>
        {
            { "France",             new[] { "Belgique", "Luxembourg", "Allemagne", "Suisse", "Italie", "Espagne", "Royaume-Uni" } },
            { "Belgique",           new[] { "France", "Pays-Bas", "Allemagne", "Luxembourg", "Royaume-Uni" } },
            { "Pays-Bas",           new[] { "Belgique", "Allemagne", "Royaume-Uni" } },
            { "Allemagne",          new[] { "France", "Belgique", "Pays-Bas", "Luxembourg", "Danemark", "Pologne", "République Tchèque", "Autriche", "Suisse" } },
            { "Italie",             new[] { "France", "Suisse", "Autriche", "Slovénie", "Malte" } },
            { "Espagne",            new[] { "France", "Portugal" } },
            { "Portugal",           new[] { "Espagne" } },
            { "Suisse",             new[] { "France", "Allemagne", "Italie", "Autriche" } },
            { "Autriche",           new[] { "Allemagne", "République Tchèque", "Slovaquie", "Hongrie", "Slovénie", "Italie", "Suisse" } },
            { "Luxembourg",         new[] { "France", "Belgique", "Allemagne" } },
            { "Royaume-Uni",        new[] { "Irlande", "France", "Belgique", "Pays-Bas" } },
            { "Irlande",            new[] { "Royaume-Uni" } },
            { "Danemark",           new[] { "Allemagne", "Suède", "Norvège" } },
            { "Suède",              new[] { "Norvège", "Finlande", "Danemark" } },
            { "Norvège",            new[] { "Suède", "Finlande", "Danemark" } },
            { "Finlande",           new[] { "Suède", "Norvège", "Estonie" } },
            { "Pologne",            new[] { "Allemagne", "République Tchèque", "Slovaquie", "Ukraine", "Biélorussie", "Lituanie" } },
            { "République Tchèque", new[] { "Allemagne", "Pologne", "Slovaquie", "Autriche" } },
            { "Slovaquie",          new[] { "République Tchèque", "Pologne", "Hongrie", "Autriche", "Ukraine" } },
            { "Hongrie",            new[] { "Autriche", "Slovaquie", "Ukraine", "Roumanie", "Serbie", "Croatie", "Slovénie" } },
            { "Roumanie",           new[] { "Hongrie", "Ukraine", "Moldavie", "Bulgarie", "Serbie" } },
            { "Bulgarie",           new[] { "Roumanie", "Serbie", "Macédoine du Nord", "Grèce" } },
            { "Grèce",              new[] { "Albanie", "Macédoine du Nord", "Bulgarie", "Chypre", "Italie" } },
            { "Croatie",            new[] { "Slovénie", "Hongrie", "Serbie", "Bosnie-H.", "Monténégro" } },
            { "Slovénie",           new[] { "Italie", "Autriche", "Hongrie", "Croatie" } },
            { "Serbie",             new[] { "Hongrie", "Roumanie", "Bulgarie", "Macédoine du Nord", "Kosovo", "Monténégro", "Bosnie-H.", "Croatie" } },
            { "Bosnie-H.",          new[] { "Croatie", "Serbie", "Monténégro" } },
            { "Monténégro",         new[] { "Croatie", "Bosnie-H.", "Serbie", "Kosovo", "Albanie" } },
            { "Albanie",            new[] { "Monténégro", "Kosovo", "Macédoine du Nord", "Grèce" } },
            { "Macédoine du Nord",  new[] { "Kosovo", "Serbie", "Bulgarie", "Grèce", "Albanie" } },
            { "Kosovo",             new[] { "Serbie", "Monténégro", "Albanie", "Macédoine du Nord" } },
            { "Moldavie",           new[] { "Roumanie", "Ukraine" } },
            { "Ukraine",            new[] { "Pologne", "Slovaquie", "Hongrie", "Roumanie", "Moldavie", "Biélorussie" } },
            { "Biélorussie",        new[] { "Pologne", "Lituanie", "Lettonie", "Ukraine" } },
            { "Lituanie",           new[] { "Lettonie", "Biélorussie", "Pologne" } },
            { "Lettonie",           new[] { "Estonie", "Lituanie", "Biélorussie" } },
            { "Estonie",            new[] { "Lettonie", "Finlande" } },
            { "Islande",            new[] { "Norvège", "Royaume-Uni", "Irlande" } },
            { "Malte",              new[] { "Italie" } },
            { "Chypre",             new[] { "Grèce" } },
        };

        public static string ContinentOf(string country)
            => country != null && _continent.TryGetValue(country, out var c) ? c : null;

        public static IEnumerable<string> BordersOf(string country)
            => country != null && _borders.TryGetValue(country, out var b) ? b : System.Array.Empty<string>();

        /// <summary>Pays autorisés selon le pays d'attache et la portée débloquée.</summary>
        /// <param name="reach">0 = pays d'attache · 1 = + limitrophes · 2 = + continent · 3 = monde.</param>
        /// <returns>L'ensemble des pays autorisés, ou <c>null</c> pour « monde entier » (aucune restriction).</returns>
        public static HashSet<string> AllowedCountries(string homeCountry, int reach)
        {
            if (string.IsNullOrEmpty(homeCountry) || reach >= 3) return null; // monde / pas d'attache connue

            var set = new HashSet<string> { homeCountry };
            if (reach >= 1)
                foreach (var b in BordersOf(homeCountry)) set.Add(b);

            if (reach >= 2)
            {
                var cont = ContinentOf(homeCountry);
                if (cont == null) return null; // continent inconnu → ne pas bloquer
                foreach (var kv in _continent)
                    if (kv.Value == cont) set.Add(kv.Key);
            }

            return set;
        }
    }
}
