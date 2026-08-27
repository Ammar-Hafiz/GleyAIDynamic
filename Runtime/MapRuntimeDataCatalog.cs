using System;
using System.Collections.Generic;
using UnityEngine;

namespace Simmac.GleyAIDynamic
{
    [CreateAssetMenu(
        fileName = "MapRuntimeDataCatalog",
        menuName = "Simmac/Gley AI Dynamic/Map Runtime Data Catalog")]
    public sealed class MapRuntimeDataCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable map identifier. By default this is the map GameObject name.")]
            public string mapId;

            [Tooltip("Runtime data prefab instantiated when this map becomes active.")]
            public GameObject runtimeDataPrefab;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        [Tooltip("Optional prefab used when a map has no explicit entry.")]
        [SerializeField]
        private GameObject defaultRuntimeDataPrefab;

        public bool TryGetPrefab(string mapId, out GameObject prefab)
        {
            string normalizedMapId = MapRuntimeId.Normalize(mapId);

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry == null)
                {
                    continue;
                }

                if (string.Equals(
                        MapRuntimeId.Normalize(entry.mapId),
                        normalizedMapId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    prefab = entry.runtimeDataPrefab;
                    return prefab != null;
                }
            }

            prefab = defaultRuntimeDataPrefab;
            return prefab != null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            HashSet<string> knownMapIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry == null)
                {
                    continue;
                }

                string normalizedMapId = MapRuntimeId.Normalize(entry.mapId);

                if (string.IsNullOrEmpty(normalizedMapId))
                {
                    Debug.LogWarning(
                        $"[{nameof(MapRuntimeDataCatalog)}] Entry {i} has an empty map ID.",
                        this);
                    continue;
                }

                if (!knownMapIds.Add(normalizedMapId))
                {
                    Debug.LogWarning(
                        $"[{nameof(MapRuntimeDataCatalog)}] Map ID '{normalizedMapId}' is duplicated.",
                        this);
                }
            }
        }
#endif
    }
}
