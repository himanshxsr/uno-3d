#nullable enable

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Uno.Presentation.Cards;
using Uno.Presentation.Controllers;
using Uno.Presentation.Hands;
using Uno.Presentation.Input;
using Uno.Presentation.Table;
using Uno.Presentation.CameraControl;
using Uno.UI.HUD;
using Uno.UI.Menus;

namespace Uno.Editor
{
    /// <summary>
    /// Builds and saves a complete playable MainGame scene.
    /// </summary>
    public static class UnoSceneSetupWizard
    {
        private const string MainGamePath = "Assets/MainGame.unity";
        private const string MainMenuPath = "Assets/MainMenu.unity";

        [MenuItem("Tools/UNO 3D/Open Main Game Scene")]
        public static void OpenMainGameScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainGamePath) == null)
            {
                BuildCompletePlayableScene();
                return;
            }

            EditorSceneManager.OpenScene(MainGamePath, OpenSceneMode.Single);
            Debug.Log("[UNO 3D] Opened MainGame scene. Press Play.");
        }

        [MenuItem("Tools/UNO 3D/Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            try
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    EditorApplication.delayCall += BuildMainMenuScene;
                    return;
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                GameObject entry = new GameObject("Main Menu Entry");
                entry.AddComponent<MainMenuEntry>();

                Camera? cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.071f, 0.247f, 0.157f, 1f);
                }

                EditorSceneManager.SaveScene(scene, MainMenuPath);
                EnsureBuildSettings();
                EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
                Debug.Log($"[UNO 3D] Main Menu saved to {MainMenuPath}. Press Play for Offline/Online hub.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UNO 3D] BuildMainMenuScene failed: {ex}");
            }
        }

        [MenuItem("Tools/UNO 3D/Build Complete Playable Scene")]
        public static void BuildCompletePlayableScene()
        {
            SetupTableScene();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, MainGamePath);
            EnsureBuildSettings();
            EditorSceneManager.OpenScene(MainGamePath, OpenSceneMode.Single);
            Debug.Log($"[UNO 3D] Saved playable scene to {MainGamePath}. Press Play.");
        }

        [MenuItem("Tools/UNO 3D/Build Production Scenes (Menu + Game)")]
        public static void BuildProductionScenes()
        {
            BuildCompletePlayableScene();
            BuildMainMenuScene();
            EnsureBuildSettings();
            Debug.Log("[UNO 3D] Production scenes ready. Build settings: MainMenu → MainGame.");
        }

        private static void EnsureBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) != null)
            {
                scenes.Add(new EditorBuildSettingsScene(MainMenuPath, true));
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainGamePath) != null)
            {
                scenes.Add(new EditorBuildSettingsScene(MainGamePath, true));
            }

            if (scenes.Count > 0)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        [MenuItem("Tools/UNO 3D/Setup Table Scene")]
        public static void SetupTableScene()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(camObj, "Create Main Camera");
            }

            mainCam.transform.position = new Vector3(0f, 9.2f, -7.4f);
            mainCam.transform.rotation = Quaternion.Euler(54f, 0f, 0f);
            mainCam.fieldOfView = 58f;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 80f;

            if (mainCam.GetComponent<PlayerCardRaycaster>() == null)
            {
                mainCam.gameObject.AddComponent<PlayerCardRaycaster>();
            }

            if (mainCam.GetComponent<ResponsiveCameraController>() == null)
            {
                mainCam.gameObject.AddComponent<ResponsiveCameraController>();
            }

            Light? sunLight = Object.FindAnyObjectByType<Light>();
            if (sunLight == null || sunLight.type != LightType.Directional)
            {
                GameObject lightObj = new GameObject("Directional Light");
                sunLight = lightObj.AddComponent<Light>();
                sunLight.type = LightType.Directional;
                Undo.RegisterCreatedObjectUndo(lightObj, "Create Directional Light");
            }

            sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sunLight.color = new Color(1f, 0.95f, 0.88f);
            sunLight.intensity = 1.2f;
            sunLight.shadows = LightShadows.Soft;

            GameObject pointLightObj = GameObject.Find("Overhead Table Light");
            if (pointLightObj == null)
            {
                pointLightObj = new GameObject("Overhead Table Light");
                Light pointLight = pointLightObj.AddComponent<Light>();
                pointLight.type = LightType.Point;
                pointLight.color = new Color(1f, 0.85f, 0.6f);
                pointLight.intensity = 2.8f;
                pointLight.range = 14f;
                pointLightObj.transform.position = new Vector3(0f, 4f, 0f);
                Undo.RegisterCreatedObjectUndo(pointLightObj, "Create Overhead Light");
            }

            GameObject tableObj = GameObject.Find("Table Surface");
            if (tableObj == null)
            {
                tableObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tableObj.name = "Table Surface";
                tableObj.transform.position = new Vector3(0f, -0.15f, 0f);
                tableObj.transform.localScale = new Vector3(11f, 0.18f, 8f);

                Shader? tableShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (tableShader != null)
                {
                    Material felt = new Material(tableShader)
                    {
                        color = new Color(0.071f, 0.247f, 0.157f, 1f)
                    };
                    if (felt.HasProperty("_Smoothness"))
                    {
                        felt.SetFloat("_Smoothness", 0.15f);
                    }

                    tableObj.GetComponent<MeshRenderer>().sharedMaterial = felt;
                }

                Undo.RegisterCreatedObjectUndo(tableObj, "Create Table Surface");
            }

            GameObject anchorRoot = GameObject.Find("Table Anchor Manager");
            if (anchorRoot == null)
            {
                anchorRoot = new GameObject("Table Anchor Manager");
                Undo.RegisterCreatedObjectUndo(anchorRoot, "Create Table Anchor Manager");
            }

            TableAnchorManager anchorManager = anchorRoot.GetComponent<TableAnchorManager>() ?? anchorRoot.AddComponent<TableAnchorManager>();

            Transform drawAnchor = CreateOrFindAnchor(anchorRoot.transform, "DrawPileAnchor", new Vector3(-1.1f, 0.08f, 0f));
            Transform discardAnchor = CreateOrFindAnchor(anchorRoot.transform, "DiscardPileAnchor", new Vector3(1.1f, 0.08f, 0f));
            Transform playerHandAnchor = CreateOrFindAnchor(anchorRoot.transform, "PlayerHandAnchor", new Vector3(0f, 0.55f, -3.6f));
            HandLayout3D playerLayout = playerHandAnchor.GetComponent<HandLayout3D>() ?? playerHandAnchor.gameObject.AddComponent<HandLayout3D>();
            playerLayout.UprightPitchAngleDeg = 62f;
            playerLayout.ArcRadius = 3.2f;
            playerLayout.MaxFanAngleDeg = 48f;

            Transform bot1 = CreateOrFindAnchor(anchorRoot.transform, "Bot1Anchor_Left", new Vector3(-3.6f, 0.5f, 0.6f));
            bot1.rotation = Quaternion.Euler(0f, 90f, 0f);
            HandLayout3D bot1Layout = bot1.GetComponent<HandLayout3D>() ?? bot1.gameObject.AddComponent<HandLayout3D>();
            bot1Layout.UprightPitchAngleDeg = 50f;
            bot1Layout.ArcRadius = 1.8f;

            Transform bot2 = CreateOrFindAnchor(anchorRoot.transform, "Bot2Anchor_Top", new Vector3(0f, 0.5f, 3.4f));
            bot2.rotation = Quaternion.Euler(0f, 180f, 0f);
            HandLayout3D bot2Layout = bot2.GetComponent<HandLayout3D>() ?? bot2.gameObject.AddComponent<HandLayout3D>();
            bot2Layout.UprightPitchAngleDeg = 50f;
            bot2Layout.ArcRadius = 1.8f;

            Transform bot3 = CreateOrFindAnchor(anchorRoot.transform, "Bot3Anchor_Right", new Vector3(3.6f, 0.5f, 0.6f));
            bot3.rotation = Quaternion.Euler(0f, -90f, 0f);
            HandLayout3D bot3Layout = bot3.GetComponent<HandLayout3D>() ?? bot3.gameObject.AddComponent<HandLayout3D>();
            bot3Layout.UprightPitchAngleDeg = 50f;
            bot3Layout.ArcRadius = 1.8f;

            SerializedObject serializedManager = new SerializedObject(anchorManager);
            serializedManager.FindProperty("_drawPileAnchor").objectReferenceValue = drawAnchor;
            serializedManager.FindProperty("_discardPileAnchor").objectReferenceValue = discardAnchor;
            serializedManager.FindProperty("_playerHandAnchor").objectReferenceValue = playerHandAnchor;
            serializedManager.FindProperty("_bot1Anchor").objectReferenceValue = bot1;
            serializedManager.FindProperty("_bot2Anchor").objectReferenceValue = bot2;
            serializedManager.FindProperty("_bot3Anchor").objectReferenceValue = bot3;
            serializedManager.ApplyModifiedProperties();

            // Force fresh HUD each setup
            UnoHudView[] oldHud = Object.FindObjectsByType<UnoHudView>(FindObjectsInactive.Include);
            for (int i = 0; i < oldHud.Length; i++)
            {
                Object.DestroyImmediate(oldHud[i].gameObject);
            }

            HudBootstrap.EnsureHud();
            BuildGameController();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[UNO 3D] Table Scene & Screen-Space HUD Canvas setup completed successfully!");
        }

        [MenuItem("Tools/UNO 3D/Build Game Controller")]
        public static void BuildGameController()
        {
            GameObject controllerObj = GameObject.Find("Game Orchestrator");
            if (controllerObj == null)
            {
                controllerObj = new GameObject("Game Orchestrator");
                Undo.RegisterCreatedObjectUndo(controllerObj, "Create Game Orchestrator");
            }

            CardFactory factory = controllerObj.GetComponent<CardFactory>() ?? controllerObj.AddComponent<CardFactory>();
            UnoGameController gameController = controllerObj.GetComponent<UnoGameController>() ?? controllerObj.AddComponent<UnoGameController>();
            PlayerCardRaycaster? raycaster = Object.FindAnyObjectByType<PlayerCardRaycaster>();

            SerializedObject serializedController = new SerializedObject(gameController);
            serializedController.FindProperty("_cardFactory").objectReferenceValue = factory;
            if (raycaster != null)
            {
                serializedController.FindProperty("_playerRaycaster").objectReferenceValue = raycaster;
            }

            serializedController.ApplyModifiedProperties();
            Debug.Log("[UNO 3D] Game Orchestrator and CardFactory successfully built and linked.");
        }

        private static Transform CreateOrFindAnchor(Transform parent, string anchorName, Vector3 localPos)
        {
            Transform? existing = parent.Find(anchorName);
            if (existing != null)
            {
                existing.localPosition = localPos;
                return existing;
            }

            GameObject newObj = new GameObject(anchorName);
            newObj.transform.SetParent(parent, false);
            newObj.transform.localPosition = localPos;
            Undo.RegisterCreatedObjectUndo(newObj, $"Create {anchorName}");
            return newObj.transform;
        }
    }
}
#endif
