using UnityEngine;

namespace Simmac.GleyAIDynamic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MapRuntimeDataLoader))]
    public sealed class MapRuntimeBridge : MonoBehaviour
    {
        [SerializeField]
        private MapRuntimeDataLoader loader;

        public MapRuntimeDataLoader Loader => loader;

        private void Reset()
        {
            ResolveLoader();
        }

        private void Awake()
        {
            ResolveLoader();
        }

        // Called by the equipment projects' existing GameManager reflection notifier.
        public void OnMapChangedEvent()
        {
            ResolveLoader();

            if (loader == null)
            {
                Debug.LogError(
                    $"[{nameof(MapRuntimeBridge)}] A {nameof(MapRuntimeDataLoader)} is required.",
                    this);
                return;
            }

            GameObject[] activeMaps;

            try
            {
                activeMaps = GameObject.FindGameObjectsWithTag("Map");
            }
            catch (UnityException exception)
            {
                Debug.LogError(
                    $"[{nameof(MapRuntimeBridge)}] The project must define the 'Map' tag. " +
                    exception.Message,
                    this);
                return;
            }

            if (activeMaps.Length != 1)
            {
                Debug.LogError(
                    $"[{nameof(MapRuntimeBridge)}] Expected exactly one active GameObject " +
                    $"tagged 'Map', but found {activeMaps.Length}.",
                    this);
                return;
            }

            loader.TryLoad(activeMaps[0].name);
        }

        public void Configure(MapRuntimeDataLoader newLoader)
        {
            loader = newLoader;
        }

        private void ResolveLoader()
        {
            if (loader == null)
            {
                loader = GetComponent<MapRuntimeDataLoader>();
            }
        }
    }
}
