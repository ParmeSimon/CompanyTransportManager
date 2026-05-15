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

        public GameSaveData Save { get; private set; }

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

            var routingProvider = BuildRoutingProvider();
            var mapSystem = new MapSystem(cityCatalog, routingProvider);

            if (vehicleCatalog != null) ServiceLocator.Register(vehicleCatalog);
            ServiceLocator.Register(new WalletSystem(Save));
            ServiceLocator.Register(new FleetSystem(Save));
            ServiceLocator.Register(new HrSystem(Save));
            ServiceLocator.Register(new XpSystem(Save));
            ServiceLocator.Register(new FuelSystem(Save, fuelStationConfig));
            ServiceLocator.Register(new ContractSystem(Save));
            ServiceLocator.Register(new ContractGenerator());
            ServiceLocator.Register(new MaintenanceSystem(Save));
            ServiceLocator.Register(new DepotSystem(Save));
            ServiceLocator.Register(new OfflineTimeService(Save));
            ServiceLocator.Register(mapSystem);
            ServiceLocator.Register(new ShopSystem(Save));

            ServiceLocator.Get<HrSystem>().EnsureRecruitmentPool();
            ServiceLocator.Get<OfflineTimeService>().ApplyOfflineProgress();
        }

        private IRoutingProvider BuildRoutingProvider()
        {
            if (forceFallbackRouting || orsConfig == null || string.IsNullOrEmpty(orsConfig.apiKey))
            {
                Debug.Log("[GameManager] Using EuclideanFallbackProvider (no ORS key or forced).");
                return new EuclideanFallbackProvider();
            }
            return new OpenRouteServiceProvider(orsConfig);
        }

        public void SaveNow() => SaveSystem.Save(Save);

        private void OnApplicationPause(bool pause)
        {
            if (pause) SaveNow();
        }

        private void OnApplicationQuit() => SaveNow();
    }
}
