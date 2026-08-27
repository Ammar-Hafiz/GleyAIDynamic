using System;
using System.Linq;
using Simmac.GleyAIDynamic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Simmac.GleyAIDynamic.Editor
{
    public sealed class MapRuntimeSetupWindow : EditorWindow
    {
        private const string DefaultCatalogFolder = "Assets/Simmac/GleyAIDynamic";
        private const string DefaultCatalogPath =
            DefaultCatalogFolder + "/MapRuntimeDataCatalog.asset";

        [SerializeField]
        private MonoBehaviour gameManager;

        [SerializeField]
        private MapRuntimeDataCatalog catalog;

        [SerializeField]
        private Transform runtimeDataParent;

        private string validationMessage =
            "Select or find a GameManager and assign a catalog.";

        private MessageType validationMessageType = MessageType.Info;

        [MenuItem("Tools/Simmac/Gley AI Dynamic/Map Runtime Setup")]
        public static void Open()
        {
            GetWindow<MapRuntimeSetupWindow>("Gley AI Dynamic Setup");
        }

        private void OnEnable()
        {
            if (gameManager == null)
            {
                gameManager = FindSceneGameManager();
            }

            if (catalog == null)
            {
                catalog = FindCatalog();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Map Runtime Integration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This installs the package bridge beside the project's existing " +
                "GameManager. Equipment-specific MapOnChangedHandler scripts are not modified.",
                MessageType.Info);

            EditorGUILayout.Space();

            gameManager = (MonoBehaviour)EditorGUILayout.ObjectField(
                "GameManager",
                gameManager,
                typeof(MonoBehaviour),
                true);

            catalog = (MapRuntimeDataCatalog)EditorGUILayout.ObjectField(
                "Runtime Data Catalog",
                catalog,
                typeof(MapRuntimeDataCatalog),
                false);

            runtimeDataParent = (Transform)EditorGUILayout.ObjectField(
                "Runtime Data Parent",
                runtimeDataParent,
                typeof(Transform),
                true);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find GameManager"))
                {
                    gameManager = FindSceneGameManager();
                    SetStatus(
                        gameManager != null
                            ? $"Found GameManager on '{gameManager.gameObject.name}'."
                            : "No scene MonoBehaviour named GameManager was found.",
                        gameManager != null ? MessageType.Info : MessageType.Error);
                }

                if (GUILayout.Button("Find Catalog"))
                {
                    catalog = FindCatalog();
                    SetStatus(
                        catalog != null
                            ? $"Found catalog '{catalog.name}'."
                            : "No MapRuntimeDataCatalog asset was found.",
                        catalog != null ? MessageType.Info : MessageType.Warning);
                }
            }

            if (GUILayout.Button("Create Project Catalog"))
            {
                CreateProjectCatalog();
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(gameManager == null || catalog == null))
            {
                if (GUILayout.Button("Install or Update Integration", GUILayout.Height(28f)))
                {
                    InstallOrUpdate();
                }
            }

            if (GUILayout.Button("Validate Current Setup"))
            {
                ValidateSetup();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(validationMessage, validationMessageType);
        }

        private void CreateProjectCatalog()
        {
            EnsureFolder(DefaultCatalogFolder);

            string path = AssetDatabase.GenerateUniqueAssetPath(DefaultCatalogPath);
            MapRuntimeDataCatalog newCatalog =
                CreateInstance<MapRuntimeDataCatalog>();

            AssetDatabase.CreateAsset(newCatalog, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            catalog = newCatalog;
            Selection.activeObject = newCatalog;
            EditorGUIUtility.PingObject(newCatalog);
            SetStatus(
                $"Created catalog at '{path}'. Add one entry for each map.",
                MessageType.Info);
        }

        private void InstallOrUpdate()
        {
            if (gameManager == null || catalog == null)
            {
                SetStatus(
                    "A GameManager and MapRuntimeDataCatalog are required.",
                    MessageType.Error);
                return;
            }

            GameObject target = gameManager.gameObject;
            MapRuntimeDataLoader loader =
                target.GetComponent<MapRuntimeDataLoader>();

            if (loader == null)
            {
                loader = Undo.AddComponent<MapRuntimeDataLoader>(target);
            }

            MapRuntimeBridge bridge = target.GetComponent<MapRuntimeBridge>();

            if (bridge == null)
            {
                bridge = Undo.AddComponent<MapRuntimeBridge>(target);
            }

            Undo.RecordObject(loader, "Configure map runtime data loader");
            loader.Configure(catalog, runtimeDataParent);

            Undo.RecordObject(bridge, "Configure map runtime bridge");
            bridge.Configure(loader);

            EditorUtility.SetDirty(loader);
            EditorUtility.SetDirty(bridge);

            if (target.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(target.scene);
            }

            ValidateSetup();
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private void ValidateSetup()
        {
            if (gameManager == null)
            {
                SetStatus("No GameManager is assigned.", MessageType.Error);
                return;
            }

            GameObject target = gameManager.gameObject;
            MapRuntimeDataLoader loader =
                target.GetComponent<MapRuntimeDataLoader>();
            MapRuntimeBridge bridge = target.GetComponent<MapRuntimeBridge>();

            if (loader == null || bridge == null)
            {
                SetStatus(
                    "The GameManager needs both MapRuntimeDataLoader and MapRuntimeBridge.",
                    MessageType.Error);
                return;
            }

            if (loader.Catalog == null)
            {
                SetStatus(
                    "MapRuntimeDataLoader does not have a catalog assigned.",
                    MessageType.Error);
                return;
            }

            bool gleyInstalled = TypeCache
                .GetTypesDerivedFrom<MonoBehaviour>()
                .Any(type => type.FullName == "GleyUrbanAssets.CurrentSceneData");

            if (!gleyInstalled)
            {
                SetStatus(
                    "The integration is installed, but Gley CurrentSceneData was not found. " +
                    "Install the compatible Gley Traffic System before using Gley data prefabs.",
                    MessageType.Warning);
                return;
            }

            int activeSceneDataCount = Resources
                .FindObjectsOfTypeAll<MonoBehaviour>()
                .Count(component =>
                    component != null &&
                    component.gameObject.scene.IsValid() &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().FullName == "GleyUrbanAssets.CurrentSceneData");

            if (activeSceneDataCount > 1)
            {
                SetStatus(
                    $"Integration is installed, but {activeSceneDataCount} active Gley " +
                    "CurrentSceneData components exist. Only one should be active.",
                    MessageType.Warning);
                return;
            }

            SetStatus(
                "Integration is installed correctly. Populate the catalog, save the scene, " +
                "and test every map in Play Mode.",
                MessageType.Info);
        }

        private static MonoBehaviour FindSceneGameManager()
        {
            MonoBehaviour[] candidates = Resources
                .FindObjectsOfTypeAll<MonoBehaviour>()
                .Where(component =>
                    component != null &&
                    component.gameObject.scene.IsValid() &&
                    !EditorUtility.IsPersistent(component) &&
                    component.GetType().Name == "GameManager")
                .ToArray();

            return candidates.Length == 1 ? candidates[0] : null;
        }

        private static MapRuntimeDataCatalog FindCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:MapRuntimeDataCatalog");

            if (guids.Length == 0)
            {
                return null;
            }

            string preferredGuid = guids.FirstOrDefault(guid =>
                AssetDatabase.GUIDToAssetPath(guid).StartsWith(
                    "Assets/",
                    StringComparison.Ordinal));

            string selectedGuid = preferredGuid ?? guids[0];
            string path = AssetDatabase.GUIDToAssetPath(selectedGuid);
            return AssetDatabase.LoadAssetAtPath<MapRuntimeDataCatalog>(path);
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }

                currentPath = nextPath;
            }
        }

        private void SetStatus(string message, MessageType messageType)
        {
            validationMessage = message;
            validationMessageType = messageType;
            Repaint();
        }
    }
}
