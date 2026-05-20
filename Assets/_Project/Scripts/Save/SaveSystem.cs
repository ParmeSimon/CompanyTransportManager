using System;
using System.IO;
using UnityEngine;

namespace TransportManager.Save
{
    public static class SaveSystem
    {
        private const string FileName = "savegame.json";
        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(GameSaveData data)
        {
            if (data == null) return;
            data.lastSaveUtcTicks = DateTime.UtcNow.Ticks;
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
        }

        public static GameSaveData Load()
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
                return null;
            }
        }

        public static bool HasSave() => File.Exists(FilePath);

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
