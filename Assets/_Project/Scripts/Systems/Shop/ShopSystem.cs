using System;
using TransportManager.Core;
using TransportManager.Enums;
using TransportManager.Save;
using TransportManager.Systems.Economy;

namespace TransportManager.Systems.Shop
{
    /// <summary>
    /// Économie premium : 1 lingot = 1 000 $ (parité avec coût d'achat véhicule).
    /// Achat de lingots avec $ délibérément coûteux pour pousser à l'IAP.
    /// </summary>
    public class ShopSystem
    {
        private readonly GameSaveData _save;

        // Ratios de conversion
        public const int GoldToDollarsRate = 1000;  // Vendre 1 lingot = 1 000 $ (parité)
        public const int DollarsToGoldRate = 5000;  // Acheter 1 lingot = 5 000 $ (5× le ratio normal)

        // Publicité quotidienne
        public const int AdCooldownMinutes = 360;   // 6h entre 2 pubs
        public const int AdGoldReward      = 2;     // +2 lingots par pub
        public const int MaxAdsPerDay      = 4;     // Cap : 4 pubs/jour = 8 lingots = 8 000 $

        // Durées des packs IAP temps limité
        public const int StarterPackDurationDays  = 14; // Pack de Démarrage : 2 semaines
        public const int BeginnerPackDurationDays = 21; // Pack de Débutant  : 3 semaines

        // Récompenses packs débutant
        public const int StarterPackDollars  = 20000; // Pack Démarrage 4,99€
        public const int StarterPackGold     = 100;
        public const int BeginnerPackDollars = 50000; // Pack Débutant 9,99€
        public const int BeginnerPackGold    = 200;

        public ShopSystem(GameSaveData save)
        {
            _save = save;
            if (_save.shop == null) _save.shop = new ShopState();
        }

        // ── Publicité : cooldown + cap quotidien ──────────────────────────────

        public TimeSpan AdCooldownRemaining
        {
            get
            {
                if (_save.shop.lastAdWatchUtcTicks == 0) return TimeSpan.Zero;
                var next = new DateTime(_save.shop.lastAdWatchUtcTicks, DateTimeKind.Utc)
                           .AddMinutes(AdCooldownMinutes);
                var diff = next - DateTime.UtcNow;
                return diff.TotalSeconds > 0 ? diff : TimeSpan.Zero;
            }
        }

        public int AdsWatchedToday
        {
            get
            {
                RolloverDayIfNeeded();
                return _save.shop.adsWatchedToday;
            }
        }

        public int AdsRemainingToday => Math.Max(0, MaxAdsPerDay - AdsWatchedToday);

        public bool CanWatchAd => AdCooldownRemaining.TotalSeconds <= 0 && AdsRemainingToday > 0;

        public bool TryWatchAdForGold()
        {
            if (!CanWatchAd) return false;
            RolloverDayIfNeeded();
            _save.shop.lastAdWatchUtcTicks = DateTime.UtcNow.Ticks;
            _save.shop.adsWatchedToday++;
            var wallet = ServiceLocator.Get<WalletSystem>();
            wallet?.Add(CurrencyType.GoldIngot, AdGoldReward);
            return true;
        }

        private void RolloverDayIfNeeded()
        {
            var todayStart = DateTime.UtcNow.Date;
            if (_save.shop.adDayStartUtcTicks != todayStart.Ticks)
            {
                _save.shop.adDayStartUtcTicks = todayStart.Ticks;
                _save.shop.adsWatchedToday   = 0;
            }
        }

        // ── Conversions ───────────────────────────────────────────────────────

        public bool TryConvertGoldToDollars(int goldAmount)
        {
            if (goldAmount <= 0) return false;
            var wallet = ServiceLocator.Get<WalletSystem>();
            if (wallet == null || !wallet.TrySpend(CurrencyType.GoldIngot, goldAmount)) return false;
            wallet.Add(CurrencyType.Dollar, goldAmount * GoldToDollarsRate);
            return true;
        }

        public bool TryConvertDollarsToGold(int goldAmount)
        {
            if (goldAmount <= 0) return false;
            int cost = goldAmount * DollarsToGoldRate;
            var wallet = ServiceLocator.Get<WalletSystem>();
            if (wallet == null || !wallet.TrySpend(CurrencyType.Dollar, cost)) return false;
            wallet.Add(CurrencyType.GoldIngot, goldAmount);
            return true;
        }

        // ── Packs (IAP simulés) ───────────────────────────────────────────────

        public void GrantGoldIngots(int amount)
        {
            if (amount <= 0) return;
            var wallet = ServiceLocator.Get<WalletSystem>();
            wallet?.Add(CurrencyType.GoldIngot, amount);
        }

        public bool GrantEnergyDrinks(int count)
        {
            if (count <= 0) return false;
            _save.energyDrinks += count;
            return true;
        }

        public int EnergyDrinkCount => _save.energyDrinks;

        // ── Offres débutant (limitées dans le temps) ──────────────────────────

        public bool     IsStarterPackAvailable  => CheckPackAvailable(StarterPackDurationDays);
        public bool     IsBeginnerPackAvailable => CheckPackAvailable(BeginnerPackDurationDays);
        public TimeSpan StarterPackTimeLeft      => GetPackTimeLeft(StarterPackDurationDays);
        public TimeSpan BeginnerPackTimeLeft     => GetPackTimeLeft(BeginnerPackDurationDays);

        private bool CheckPackAvailable(int days)
        {
            if (_save.installDateUtcTicks == 0) return true;
            var install = new DateTime(_save.installDateUtcTicks, DateTimeKind.Utc);
            return (DateTime.UtcNow - install).TotalDays <= days;
        }

        private TimeSpan GetPackTimeLeft(int days)
        {
            if (_save.installDateUtcTicks == 0) return TimeSpan.FromDays(days);
            var install = new DateTime(_save.installDateUtcTicks, DateTimeKind.Utc);
            var diff    = install.AddDays(days) - DateTime.UtcNow;
            return diff.TotalSeconds > 0 ? diff : TimeSpan.Zero;
        }

        // ── Offres spéciales ──────────────────────────────────────────────────

        public bool IsSpecialOfferClaimed(string offerId)
        {
            return _save.shop.claimedSpecialOfferIds.Contains(offerId);
        }

        /// <summary>
        /// IAP : achat avec argent réel. Le paiement est validé en amont par le store (Google/Apple),
        /// cette méthode ne fait que créditer la récompense et marquer l'offre comme obtenue.
        /// </summary>
        public bool TryClaimSpecialOfferIAP(string offerId, int dollarsReward, int goldReward)
        {
            if (IsSpecialOfferClaimed(offerId)) return false;
            var wallet = ServiceLocator.Get<WalletSystem>();
            if (wallet == null) return false;
            if (dollarsReward > 0) wallet.Add(CurrencyType.Dollar, dollarsReward);
            if (goldReward    > 0) wallet.Add(CurrencyType.GoldIngot, goldReward);
            _save.shop.claimedSpecialOfferIds.Add(offerId);

            switch (offerId)
            {
                case "starter_pack":
                    // TODO: débloquer le camion exclusif starter (VehicleSystem)
                    // TODO: activer -25% temps maintenance pendant 7 jours (MaintenanceSystem)
                    break;

                case "beginner_pack":
                    // TODO: débloquer le camion exclusif beginner (VehicleSystem)
                    // TODO: débloquer le conducteur exclusif (DriverSystem)
                    // TODO: ajouter +1 emplacement de véhicule (FleetSystem)
                    // TODO: activer -50% temps maintenance permanent (MaintenanceSystem)
                    break;
            }

            return true;
        }
    }
}
