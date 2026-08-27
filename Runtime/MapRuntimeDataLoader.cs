using System;
using UnityEngine;
using UnityEngine.Events;

namespace Simmac.GleyAIDynamic
{
    [DisallowMultipleComponent]
    public sealed class MapRuntimeDataLoader : MonoBehaviour
    {
        [Header("Map Runtime Data")]
        [SerializeField]
        private MapRuntimeDataCatalog catalog;

        [Tooltip("Optional parent for instantiated runtime data. Leave empty to use the scene root.")]
        [SerializeField]
        private Transform runtimeDataParent;

        [Header("Events")]
        [SerializeField]
        private UnityEvent mapDataLoaded = new UnityEvent();

        [SerializeField]
        private UnityEvent mapDataLoadFailed = new UnityEvent();

        private GameObject loadedInstance;
        private string loadedMapId = string.Empty;

        public event Action<string, GameObject> DataLoaded;
        public event Action<string> DataLoadFailed;

        public MapRuntimeDataCatalog Catalog => catalog;
        public GameObject LoadedInstance => loadedInstance;
        public string LoadedMapId => loadedMapId;

        public bool TryLoad(string mapId)
        {
            string normalizedMapId = MapRuntimeId.Normalize(mapId);

            if (!Application.isPlaying)
            {
                return Fail(
                    normalizedMapId,
                    "Runtime map data can only be loaded while the application is playing.");
            }

            if (string.IsNullOrEmpty(normalizedMapId))
            {
                return Fail(normalizedMapId, "The map ID is null or empty.");
            }

            if (catalog == null)
            {
                return Fail(normalizedMapId, "No MapRuntimeDataCatalog is assigned.");
            }

            if (loadedInstance != null &&
                string.Equals(
                    loadedMapId,
                    normalizedMapId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!catalog.TryGetPrefab(normalizedMapId, out GameObject prefab))
            {
                UnloadCurrent();
                return Fail(
                    normalizedMapId,
                    $"No runtime data prefab is configured for map '{normalizedMapId}'.");
            }

            // Disable the previous object immediately so systems such as Gley do
            // not observe two active scene-data components before Destroy finishes.
            UnloadCurrent();

            loadedInstance = runtimeDataParent != null
                ? Instantiate(prefab, runtimeDataParent)
                : Instantiate(prefab);

            loadedInstance.name = prefab.name;
            loadedMapId = normalizedMapId;

            Debug.Log(
                $"[{nameof(MapRuntimeDataLoader)}] Loaded '{prefab.name}' for map '{loadedMapId}'.",
                loadedInstance);

            mapDataLoaded.Invoke();
            DataLoaded?.Invoke(loadedMapId, loadedInstance);
            return true;
        }

        public void UnloadCurrent()
        {
            if (loadedInstance != null)
            {
                loadedInstance.SetActive(false);

                if (Application.isPlaying)
                {
                    Destroy(loadedInstance);
                }
                else
                {
                    DestroyImmediate(loadedInstance);
                }
            }

            loadedInstance = null;
            loadedMapId = string.Empty;
        }

        public void Configure(
            MapRuntimeDataCatalog newCatalog,
            Transform newRuntimeDataParent = null)
        {
            catalog = newCatalog;
            runtimeDataParent = newRuntimeDataParent;
        }

        private bool Fail(string mapId, string reason)
        {
            Debug.LogError(
                $"[{nameof(MapRuntimeDataLoader)}] {reason}",
                this);

            mapDataLoadFailed.Invoke();
            DataLoadFailed?.Invoke(mapId);
            return false;
        }
    }
}
