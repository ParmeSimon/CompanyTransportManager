using System;
using System.Collections.Generic;
using UnityEngine;

namespace TransportManager.Social
{
    [Serializable]
    public class FriendEntry
    {
        public string uid;
        public string companyName;
        public int    level;
        public int    vehicleCount;
    }

    public static class FriendsData
    {
        private const string PrefsKey = "friends_v1";

        [Serializable]
        private class Wrapper { public List<FriendEntry> items = new List<FriendEntry>(); }

        public static List<FriendEntry> LoadAll()
        {
            var json = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) return new List<FriendEntry>();
            try   { return JsonUtility.FromJson<Wrapper>(json).items; }
            catch { return new List<FriendEntry>(); }
        }

        public static void SaveAll(List<FriendEntry> friends)
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(new Wrapper { items = friends }));
            PlayerPrefs.Save();
        }

        public static void AddFriend(FriendEntry f)
        {
            var list = LoadAll();
            if (list.Exists(x => x.uid == f.uid)) return;
            list.Add(f);
            SaveAll(list);
        }

        public static void RemoveFriend(string uid)
        {
            var list = LoadAll();
            list.RemoveAll(x => x.uid == uid);
            SaveAll(list);
        }

        // Deep link format — swap host/scheme once backend is ready
        public static string GenerateInviteLink()
        {
            var deviceId = SystemInfo.deviceUniqueIdentifier;
            return $"transportmanager://invite?ref={deviceId}";
        }
    }
}
