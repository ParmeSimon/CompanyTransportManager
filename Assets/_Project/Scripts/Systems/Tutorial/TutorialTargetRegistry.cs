using System.Collections.Generic;
using UnityEngine;

namespace TransportManager.Systems.Tutorial
{
    public static class TutorialTargetRegistry
    {
        private static readonly Dictionary<string, RectTransform> _targets = new Dictionary<string, RectTransform>();

        public static void Register(string targetId, RectTransform rt)
        {
            if (string.IsNullOrEmpty(targetId) || rt == null) return;
            _targets[targetId] = rt;
        }

        public static void Unregister(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return;
            _targets.Remove(targetId);
        }

        public static RectTransform Get(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return null;
            _targets.TryGetValue(targetId, out var rt);
            return rt;
        }
    }
}
