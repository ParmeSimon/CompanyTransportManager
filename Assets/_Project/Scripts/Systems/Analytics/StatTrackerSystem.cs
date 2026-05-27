using System;
using System.Collections.Generic;
using TransportManager.Save;
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Analytics
{
    public class StatTrackerSystem
    {
        private readonly GameSaveData _save;
        private readonly XpSystem     _xp;

        private const int MaxSnapshots = 90;

        public StatTrackerSystem(GameSaveData save, XpSystem xp)
        {
            _save = save;
            _xp   = xp;
            if (_save.snapshots == null)
                _save.snapshots = new List<StatSnapshot>();
            TryRecordSnapshot();
        }

        private void TryRecordSnapshot()
        {
            var now   = DateTime.UtcNow;
            var today = now.Date;

            if (_save.snapshots.Count > 0)
            {
                var lastDate = new DateTime(_save.snapshots[_save.snapshots.Count - 1].utcTicks,
                                            DateTimeKind.Utc).Date;
                if (lastDate >= today) return;
            }

            _save.snapshots.Add(new StatSnapshot
            {
                utcTicks           = now.Ticks,
                dollars            = _save.dollars,
                goldIngots         = _save.goldIngots,
                companyXp          = _xp?.CompanyXp ?? 0,
                contractsCompleted = CountTotalContracts(),
                vehicleCount       = _save.vehicles?.Count ?? 0
            });

            while (_save.snapshots.Count > MaxSnapshots)
                _save.snapshots.RemoveAt(0);
        }

        private int CountTotalContracts()
        {
            if (_save.hiredDrivers == null) return 0;
            int total = 0;
            foreach (var d in _save.hiredDrivers)
                total += d.contractsCompleted;
            return total;
        }

        public IReadOnlyList<StatSnapshot> Snapshots => _save.snapshots;

        public void SeedFakeMonthData()
        {
            _save.snapshots.Clear();
            var rng     = new Random(42);
            var nowUtc  = DateTime.UtcNow;
            int dollars = 8000;

            for (int day = 29; day >= 0; day--)
            {
                // Simulate daily revenue minus expenses: net +200 to +1200 with some loss days
                int delta = rng.Next(-300, 1400);
                dollars = Math.Max(1000, dollars + delta);

                _save.snapshots.Add(new StatSnapshot
                {
                    utcTicks           = nowUtc.AddDays(-day).Ticks,
                    dollars            = dollars,
                    goldIngots         = _save.goldIngots,
                    companyXp          = _xp?.CompanyXp ?? 0,
                    contractsCompleted = 0,
                    vehicleCount       = _save.vehicles?.Count ?? 0
                });
            }
        }
    }
}
