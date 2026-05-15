using UnityEngine;

namespace TransportManager.Core
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private GameManager gameManagerPrefab;

        private void Awake()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                if (gameManagerPrefab == null)
                {
                    Debug.LogError("[Bootstrap] gameManagerPrefab not assigned.");
                    return;
                }
                gm = Instantiate(gameManagerPrefab);
            }
            gm.Initialize();
        }
    }
}
