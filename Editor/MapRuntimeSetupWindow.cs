using System;
using System.IO;
using System.Linq;
using Simmac.GleyAIDynamic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PackageSample = UnityEditor.PackageManager.UI.Sample;

namespace Simmac.GleyAIDynamic.Editor
{
    public sealed class MapRuntimeSetupWindow : EditorWindow
    {
        private const string PackageName = "com.simmac.gleyaidynamic";
        private const string SampleDisplayName = "Dynamic Map AI Content";
        private const string ContentFolderName = "Dynamic Map AI";
        private const string CatalogFileName = "DefaultMapRuntimeDataCatalog.asset";
        private const string ContentRootPath = "Assets/Simmac/GleyAIDynamic";
        private const string ContentTargetPath =
            ContentRootPath + "/" + ContentFolderName;
        private const string CatalogTargetPath =
            ContentRootPath + "/" + CatalogFileName;
        private const string MissingCatalogHelp =
            "No MapRuntimeDataCatalog was found under Assets. Press \"Import or Update " +
            "Map Content\" to copy the map prefabs and the catalog out of the package " +
            "into " + ContentRootPath + ".";
        private const string ContentOwnershipHelp =
            "Map prefabs are owned by the package: every sync replaces " +
            ContentTargetPath + " with the packaged copy, so do not hand-edit them here. " +
            "The catalog at " + CatalogTargetPath + " is owned by this project and is " +
            "only created when missing, so project-specific map entries are never lost.";

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
                            ? $"Found catalog at '{AssetDatabase.GetAssetPath(catalog)}'."
                            : MissingCatalogHelp,
                        catalog != null ? MessageType.Info : MessageType.Warning);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Map Content", EditorStyles.boldLabel);

            if (catalog == null)
            {
                EditorGUILayout.HelpBox(MissingCatalogHelp, MessageType.Warning);
            }

            if (GUILayout.Button("Import or Update Map Content", GUILayout.Height(24f)))
            {
                SyncMapContent();
            }

            EditorGUILayout.HelpBox(ContentOwnershipHelp, MessageType.Info);

            if (GUILayout.Button("Open Package Manager"))
            {
                UnityEditor.PackageManager.UI.Window.Open(PackageName);
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

        private void SyncMapContent()
        {
            if (!TryGetSampleSourcePath(out string sampleRoot, out string error))
            {
                SetStatus(error, MessageType.Error);
                return;
            }

            string prefabSource = Path.Combine(sampleRoot, ContentFolderName);

            if (!Directory.Exists(prefabSource))
            {
                SetStatus(
                    $"The package sample has no '{ContentFolderName}' folder at " +
                    $"'{prefabSource}'.",
                    MessageType.Error);
                return;
            }

            bool targetExists = Directory.Exists(ContentTargetPath);

            if (targetExists && !EditorUtility.DisplayDialog(
                    "Replace map content?",
                    $"'{ContentTargetPath}' will be deleted and replaced with the copy " +
                    $"shipped in {PackageName}.\n\nLocal edits to those map prefabs will " +
                    "be lost. The catalog is not affected.",
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            Directory.CreateDirectory(ContentRootPath);

            if (AssetDatabase.IsValidFolder(ContentTargetPath))
            {
                AssetDatabase.DeleteAsset(ContentTargetPath);
            }
            else if (targetExists)
            {
                Directory.Delete(ContentTargetPath, true);
            }

            FileUtil.DeleteFileOrDirectory(ContentTargetPath + ".meta");

            CopyDirectory(prefabSource, ContentTargetPath);

            // Carry the folder's own .meta across so its GUID survives the sync.
            CopyFileIfMissingOrForced(
                prefabSource + ".meta",
                ContentTargetPath + ".meta",
                true);

            bool catalogCreated = CopyFileIfMissingOrForced(
                Path.Combine(sampleRoot, CatalogFileName),
                CatalogTargetPath,
                false);

            if (catalogCreated)
            {
                CopyFileIfMissingOrForced(
                    Path.Combine(sampleRoot, CatalogFileName + ".meta"),
                    CatalogTargetPath + ".meta",
                    false);
            }

            AssetDatabase.Refresh();

            if (catalog == null)
            {
                catalog = FindCatalog();
            }

            string catalogNote = catalogCreated
                ? $"Created the project catalog at '{CatalogTargetPath}'."
                : "Left the existing project catalog untouched.";

            SetStatus(
                $"Map prefabs synced into '{ContentTargetPath}'. {catalogNote}",
                MessageType.Info);
        }

        private static bool TryGetSampleSourcePath(out string sampleRoot, out string error)
        {
            sampleRoot = null;

            UnityEditor.PackageManager.PackageInfo packageInfo =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(MapRuntimeDataCatalog).Assembly);

            if (packageInfo == null)
            {
                error =
                    $"'{PackageName}' is not resolved as a UPM package, so its sample " +
                    "content cannot be located.";
                return false;
            }

            PackageSample sample = PackageSample
                .FindByPackage(packageInfo.name, packageInfo.version)
                .FirstOrDefault(candidate => candidate.displayName == SampleDisplayName);

            if (!string.IsNullOrEmpty(sample.resolvedPath) &&
                Directory.Exists(sample.resolvedPath))
            {
                error = null;
                sampleRoot = sample.resolvedPath;
                return true;
            }

            // Embedded and local packages are not always reported through the sample
            // API, so fall back to the on-disk layout.
            string fallbackPath = Path.Combine(
                packageInfo.resolvedPath,
                "Samples~",
                SampleDisplayName);

            if (Directory.Exists(fallbackPath))
            {
                error = null;
                sampleRoot = fallbackPath;
                return true;
            }

            error =
                $"The '{SampleDisplayName}' sample was not found in " +
                $"{packageInfo.name}@{packageInfo.version}. Update the package to a " +
                "version that ships it.";
            return false;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);

            foreach (string filePath in Directory.GetFiles(source))
            {
                File.Copy(
                    filePath,
                    Path.Combine(target, Path.GetFileName(filePath)),
                    true);
            }

            foreach (string directoryPath in Directory.GetDirectories(source))
            {
                CopyDirectory(
                    directoryPath,
                    Path.Combine(target, Path.GetFileName(directoryPath)));
            }
        }

        private static bool CopyFileIfMissingOrForced(
            string source,
            string target,
            bool overwrite)
        {
            if (!File.Exists(source) || (!overwrite && File.Exists(target)))
            {
                return false;
            }

            File.Copy(source, target, overwrite);
            return true;
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

        private void SetStatus(string message, MessageType messageType)
        {
            validationMessage = message;
            validationMessageType = messageType;
            Repaint();
        }
    }
}
