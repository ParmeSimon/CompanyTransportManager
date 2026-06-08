using UnityEngine;
using TransportManager.Entities.Fuel;
using TransportManager.Entities.Map;
using TransportManager.Entities.Vehicles;
using TransportManager.Save;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Fleet;
using TransportManager.Systems.Contracts;
using TransportManager.Systems.Fuel;
using TransportManager.Systems.Hr;
using TransportManager.Systems.Maintenance;
using TransportManager.Systems.Depot;
using TransportManager.Systems.Progression;
using TransportManager.Systems.Time;
using TransportManager.Systems.Shop;
using TransportManager.Systems.Map;
using TransportManager.Systems.Map.Routing;
using TransportManager.Systems.Tutorial;
using TransportManager.Systems.Analytics;
using TransportManager.Systems.Buildings;

namespace TransportManager.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Catalogs")]
        [SerializeField] private VehicleCatalog vehicleCatalog;
        [SerializeField] private CityCatalog cityCatalog;
        [SerializeField] private FuelStationConfig fuelStationConfig;

        [Header("Routing")]
        [SerializeField] private OrsConfig orsConfig;
        [Tooltip("Force the offline euclidean routing provider (dev mode without API key).")]
        [SerializeField] private bool forceFallbackRouting;

        [Header("Debug")]
        [Tooltip("Delete the save file at startup. Tick once, run, then untick.")]
        [SerializeField] private bool resetSaveOnStart;
        [Tooltip("Inject 30 fake dollar snapshots for analytics testing. Tick once, run, then untick.")]
        [SerializeField] private bool seedFakeAnalytics;
        [Tooltip("Add 3 fake vehicles to the fleet for UI testing. Tick once, run, then untick.")]
        [SerializeField] private bool seedFakeFleet;

        public GameSaveData Save { get; private set; }
        public TutorialSystem Tutorial { get; private set; }
        public BuildingService Buildings { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Initialize()
        {
            if (resetSaveOnStart)
            {
                SaveSystem.Delete();
                Debug.Log("[GameManager] Save reset on start.");
            }
            Save = SaveSystem.Load() ?? new GameSaveData();
            if (Save.tutorial == null) Save.tutorial = new TutorialState();
            if (Save.buildingLevels == null) Save.buildingLevels = new BuildingLevels();
            if (Save.snapshots == null) Save.snapshots = new System.Collections.Generic.List<StatSnapshot>();
            if (Save.shop == null) Save.shop = new ShopState();
            if (Save.installDateUtcTicks == 0) Save.installDateUtcTicks = System.DateTime.UtcNow.Ticks;

            var routingProvider = BuildRoutingProvider();
            var mapSystem = new MapSystem(cityCatalog, routingProvider);

            if (vehicleCatalog != null) ServiceLocator.Register(vehicleCatalog);
            var wallet = new WalletSystem(Save);
            ServiceLocator.Register(wallet);
            Buildings = new BuildingService(Save, wallet);
            ServiceLocator.Register(Buildings);
            ServiceLocator.Register(new FleetSystem(Save));
            ServiceLocator.Register(new HrSystem(Save));
            ServiceLocator.Register(new XpSystem(Save));
            ServiceLocator.Register(new SkillTreeSystem(Save));
            ServiceLocator.Register(new ReputationSystem(Save));
            var statTracker = new StatTrackerSystem(Save, ServiceLocator.Get<XpSystem>());
            if (seedFakeAnalytics) { statTracker.SeedFakeMonthData(); Debug.Log("[GameManager] Fake analytics data seeded."); }
            ServiceLocator.Register(statTracker);

            if (seedFakeFleet) { SeedFakeFleetData(); SeedUnlockMultiStop(); Debug.Log("[GameManager] Fake fleet data seeded."); }
            ServiceLocator.Register(new FuelSystem(Save, fuelStationConfig));
            ServiceLocator.Register(new ContractSystem(Save));
            ServiceLocator.Register(new ContractGenerator());
            ServiceLocator.Register(new MaintenanceSystem(Save));
            ServiceLocator.Register(new DepotSystem(Save));
            ServiceLocator.Register(new OfflineTimeService(Save));
            ServiceLocator.Register(mapSystem);
            ServiceLocator.Register(new ShopSystem(Save));

            // Le dépôt = la vraie position de l'entreprise (et non la ville-catalogue la plus
            // proche). On l'enregistre comme entrée « maison » résolvable par tout le jeu.
            EnsureHomeDepot(mapSystem);

            // Défis quotidiens + récompense de connexion (rétention).
            ServiceLocator.Register(new Systems.Daily.DailySystem(Save));

            // Succès / achievements globaux (objectifs long terme — G1).
            ServiceLocator.Register(new Systems.Achievements.AchievementSystem(Save));

            // Cours du carburant réel (identique pour tous) : récupération asynchrone, repli local si indispo.
            Systems.Economy.RealFuelMarket.EnsureFresh();

            ServiceLocator.Get<HrSystem>().EnsureRecruitmentPool();
            ServiceLocator.Get<OfflineTimeService>().ApplyOfflineProgress();

            Tutorial = new TutorialSystem(Save);
            ServiceLocator.Register(Tutorial);
            _tutorialDriver = new TutorialDriver(Tutorial);

            // Couche game feel (confettis, gerbe d'argent, toasts, sons). Créée APRÈS le
            // traitement hors-ligne pour ne pas spammer d'effets au démarrage.
            UI.Juice.JuiceOverlay.Ensure();
        }

        private TutorialDriver _tutorialDriver;

        private IRoutingProvider BuildRoutingProvider()
        {
            if (forceFallbackRouting || orsConfig == null || string.IsNullOrEmpty(orsConfig.apiKey))
            {
                Debug.Log("[GameManager] Using EuclideanFallbackProvider (no ORS key or forced).");
                return new EuclideanFallbackProvider();
            }
            return new OpenRouteServiceProvider(orsConfig);
        }

        // Construit l'entrée « dépôt maison » depuis les coordonnées de l'entreprise.
        // La ville-catalogue la plus proche ne sert qu'à déterminer le pays (portée des contrats).
        private void EnsureHomeDepot(MapSystem map)
        {
            var c = Save?.company;
            if (map?.Catalog == null) return;
            if (c == null || !c.hasLocationCoordinates) { map.Catalog.SetHome(null); return; }

            var nearest = map.GetNearestCity(c.locationLatitude, c.locationLongitude);
            map.Catalog.SetHome(new Entities.Map.CityEntry
            {
                id          = "home_depot",
                displayName = DepotName(c.location),
                country     = nearest?.country ?? string.Empty,
                location    = new Entities.Map.GeoPoint(c.locationLatitude, c.locationLongitude)
            });
        }

        // Nom lisible du dépôt à partir de l'adresse (même logique que le header).
        private static string DepotName(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return "Mon dépôt";
            var parts = location.Split(',');
            string first = parts[0].Trim();
            if (parts.Length > 1 && first.Length > 0 && char.IsDigit(first[0]))
                return first + " " + parts[1].Trim();
            return first;
        }

        // Débloque d'office la chaîne de skills menant aux tournées multi-arrêts (jeu de test).
        private void SeedUnlockMultiStop()
        {
            string[] chain = { "depot_root", "depot_a", "depot_a2", "depot_a2a", "depot_tour" };
            var unlocked = Save.skillTree.unlockedNodeIds;
            foreach (var id in chain)
                if (!unlocked.Contains(id)) unlocked.Add(id);

            // Sans localisation d'entreprise, le dépôt retombe sur une ville aléatoire :
            // on fixe un dépôt déterministe (Paris) pour le jeu de test si aucun n'est défini.
            if (Save.company != null && !Save.company.hasLocationCoordinates)
            {
                Save.company.locationLatitude       = 48.8566;
                Save.company.locationLongitude      = 2.3522;
                Save.company.hasLocationCoordinates = true;
                Save.company.location               = "Paris";
                Debug.Log("[GameManager] Dépôt de test fixé sur Paris.");
            }

            // Purge les contrats déjà listés (générés avant le passage au départ-dépôt) :
            // ils seront régénérés, ancrés sur le dépôt.
            Save.availableContracts.Clear();

            Debug.Log("[GameManager] Skill « Tournées multi-arrêts » débloqué (seed de test).");
        }

        private void SeedFakeFleetData()
        {
            Save.vehicles.Clear();
            Save.activeContracts.RemoveAll(c =>
                c.assignedVehicleInstanceId == "fake_v1" ||
                c.assignedVehicleInstanceId == "fake_v2" ||
                c.assignedVehicleInstanceId == "fake_v3");

            // 1. Camion idle
            Save.vehicles.Add(new TransportManager.Entities.Vehicles.VehicleInstance
            {
                instanceId      = "fake_v1",
                vehicleDataId   = "porteur_distrib_12t",
                status          = TransportManager.Enums.VehicleStatus.Idle,
                totalKilometers = 12450,
                currentFuelLiters = 180f
            });

            // 2. Tracteur en contrat Paris → Lyon (60% fait, 2h restantes)
            var now          = System.DateTime.UtcNow;
            var startTime    = now.AddHours(-3);   // démarré il y a 3h
            var endTime      = now.AddHours(2);    // encore 2h (total 5h)
            var fakeContract = new TransportManager.Entities.Contracts.ContractInstance
            {
                instanceId                  = "fake_c1",
                assignedVehicleInstanceId   = "fake_v2",
                startTimeUtcTicks           = startTime.Ticks,
                completionTimeUtcTicks      = endTime.Ticks,
                status                      = TransportManager.Enums.ContractStatus.InProgress,
                definition = new TransportManager.Entities.Contracts.ContractData
                {
                    id                   = "fake_def1",
                    displayName          = "Paris → Lyon",
                    originCityId         = "paris",
                    destinationCityId    = "lyon",
                    originAddressLabel   = "Entrepôt Nord, Paris",
                    destinationAddressLabel = "Zone Logistique, Lyon",
                    distanceKm           = 465f,
                    baseDurationSeconds  = 18000f,
                    requiredCapacity     = 10,
                    baseReward           = 1800
                }
            };
            Save.activeContracts.Add(fakeContract);
            Save.vehicles.Add(new TransportManager.Entities.Vehicles.VehicleInstance
            {
                instanceId                = "fake_v2",
                vehicleDataId             = "tracteur_standard",
                status                    = TransportManager.Enums.VehicleStatus.OnContract,
                activeContractInstanceId  = "fake_c1",
                totalKilometers           = 87320,
                currentFuelLiters         = 320f
            });

            // 3. Van en maintenance
            Save.vehicles.Add(new TransportManager.Entities.Vehicles.VehicleInstance
            {
                instanceId      = "fake_v3",
                vehicleDataId   = "van_occasion",
                status          = TransportManager.Enums.VehicleStatus.InMaintenance,
                totalKilometers = 3210,
                currentFuelLiters = 40f
            });
        }

        public void SaveNow() => SaveSystem.Save(Save);

        private void OnApplicationPause(bool pause)
        {
            if (pause) SaveNow();
        }

        private void OnApplicationQuit() => SaveNow();
    }
}
