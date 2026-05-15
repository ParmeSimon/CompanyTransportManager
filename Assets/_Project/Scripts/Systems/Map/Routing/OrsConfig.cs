using UnityEngine;

namespace TransportManager.Systems.Map.Routing
{
    [CreateAssetMenu(fileName = "OrsConfig", menuName = "TransportManager/OpenRouteService Config")]
    public class OrsConfig : ScriptableObject
    {
        [Tooltip("OpenRouteService API key. Get one free at https://openrouteservice.org/dev/")]
        public string apiKey;

        [Tooltip("Base URL of the ORS API. Default = official endpoint.")]
        public string baseUrl = "https://api.openrouteservice.org";

        [Tooltip("Request elevation data (ascent/descent). Disable to save bandwidth.")]
        public bool requestElevation = true;

        [Tooltip("Timeout per HTTP request in seconds.")]
        public int requestTimeoutSeconds = 15;
    }
}
