using System;
using System.Collections.Generic;

namespace TransportManager.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
        }

        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        public static void Clear() => _services.Clear();
    }
}
