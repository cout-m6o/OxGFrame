using System;
using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.GraphicRaycaster;

namespace OxGFrame.CoreFrame.Editor
{
    public static class CoreFrameCreatePrefabEditor
    {
        class DoCreatePrefabAsset : EndNameEditAction
        {
            // Subclass and override this method to create specialised prefab asset creation functions
            protected virtual GameObject CreateGameObject(string name)
            {
                return new GameObject(name);
            }

            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                GameObject go = CreateGameObject(Path.GetFileNameWithoutExtension(pathName));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, pathName);
                GameObject.DestroyImmediate(go);
            }
        }

        class DoCreateUIPrefabAsset : DoCreatePrefabAsset
        {
            protected override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));

                // calibrate RectTransform
                RectTransform objRect = obj.GetComponent<RectTransform>();
                objRect.anchorMin = Vector2.zero;
                objRect.anchorMax = Vector2.one;
                objRect.sizeDelta = Vector2.zero;
                objRect.localScale = Vector3.one;
                objRect.localPosition = Vector3.zero;

                // calibrate Canvas
                Canvas objCanvas = obj.GetComponent<Canvas>();
                objCanvas.overridePixelPerfect = true;
                objCanvas.pixelPerfect = true;
                objCanvas.overrideSorting = false;
                objCanvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

                // calibrate  Graphic Raycaster
                GraphicRaycaster objGraphicRaycaster = obj.GetComponent<GraphicRaycaster>();
                objGraphicRaycaster.ignoreReversedGraphics = false;
                objGraphicRaycaster.blockingObjects = BlockingObjects.None;
                objGraphicRaycaster.blockingMask = 0;

                return obj;
            }
        }

        class DoCreateRectTransformPrefabAsset : DoCreatePrefabAsset
        {
            protected override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));

                // calibrate RectTransform
                RectTransform objRect = obj.GetComponent<RectTransform>();
                objRect.anchorMin = new Vector2(0.5f, 0.5f);
                objRect.anchorMax = new Vector2(0.5f, 0.5f);
                objRect.sizeDelta = new Vector2(128, 128);
                objRect.localScale = Vector3.one;
                objRect.localPosition = Vector3.zero;

                return obj;
            }
        }

        static void CreatePrefabAsset(string name, DoCreatePrefabAsset createAction)
        {
            string directory = GetSelectedAssetDirectory();
            string path = Path.Combine(directory, $"{name}.prefab");
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, createAction, path, EditorGUIUtility.FindTexture("Prefab Icon"), null);
        }

        static string GetSelectedAssetDirectory()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);

            // 未選取任何資產時 (例如由選單列建立) 路徑為空字串，Path.GetDirectoryName("") 會拋出 ArgumentException，此時回退至 Assets 根目錄
            // 結尾保留 "/"，讓 Path.Combine 在 Windows 也組出以 "Assets/" 開頭的路徑 (Unity 以此判斷是否為完整的資產路徑)
            if (string.IsNullOrEmpty(path))
                return "Assets/";

            if (Directory.Exists(path))
                return path;
            else
                return Path.GetDirectoryName(path);
        }

        [MenuItem("Assets/Create/OxGFrame/Core Frame/SR Frame/Template Prefabs/Template SR (Scene Resource Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateTplSR()
        {
            CreatePrefabAsset("NewTplSR", ScriptableObject.CreateInstance<DoCreatePrefabAsset>());
        }

        [MenuItem("Assets/Create/OxGFrame/Core Frame/UI Frame/Template Prefabs/Template UI (UGUI Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateTplUI()
        {
            CreatePrefabAsset("NewTplUI", ScriptableObject.CreateInstance<DoCreateUIPrefabAsset>());
        }

        [MenuItem("Assets/Create/OxGFrame/Core Frame/CP Frame/Template Prefabs/Template CP (Transform Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateTplTransformCP()
        {
            CreatePrefabAsset("NewTplTransformCP", ScriptableObject.CreateInstance<DoCreatePrefabAsset>());
        }

        [MenuItem("Assets/Create/OxGFrame/Core Frame/CP Frame/Template Prefabs/Template CP (RectTransform Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateTplRectTransformCP()
        {
            CreatePrefabAsset("NewTplRectTransformCP", ScriptableObject.CreateInstance<DoCreateRectTransformPrefabAsset>());
        }

        #region Automation
        /// <summary>
        /// 可用的 Template Prefab 名稱
        /// </summary>
        public static readonly string[] TEMPLATE_NAMES = new string[]
        {
            "ui",
            "sr",
            "cp_transform",
            "cp_rect_transform"
        };

        /// <summary>
        /// 於指定路徑建立 Template Prefab (供 Unity CLI 與自動化流程調用)
        /// <para>執行與選單相同的建立動作，差異僅在於不進入 Project 視窗的命名編輯。</para>
        /// </summary>
        /// <param name="templateName">Template 名稱 (ui, sr, cp_transform, cp_rect_transform)</param>
        /// <param name="assetPath">Prefab 的資源路徑 (ex: Assets/Prefabs/ShopUI.prefab)</param>
        /// <param name="error">失敗原因 (成功時為 null)</param>
        /// <returns></returns>
        public static bool CreatePrefabFromTemplate(string templateName, string assetPath, out string error)
        {
            if (Array.IndexOf(TEMPLATE_NAMES, templateName) == -1)
            {
                error = $"Unknown template name: \"{templateName}\" (available: {string.Join(", ", TEMPLATE_NAMES)}).";
                Debug.LogError(error);
                return false;
            }

            if (!AutomationUtility.PrepareCreateAssetPath(ref assetPath, ".prefab", out error))
            {
                Debug.LogError(error);
                return false;
            }

            DoCreatePrefabAsset createAction;
            switch (templateName)
            {
                case "ui":
                    createAction = ScriptableObject.CreateInstance<DoCreateUIPrefabAsset>();
                    break;
                case "cp_rect_transform":
                    createAction = ScriptableObject.CreateInstance<DoCreateRectTransformPrefabAsset>();
                    break;
                default:
                    // sr 與 cp_transform 皆為一般的 Transform Prefab
                    createAction = ScriptableObject.CreateInstance<DoCreatePrefabAsset>();
                    break;
            }

            try
            {
                createAction.Action(0, assetPath, null);
            }
            finally
            {
                // 不經過命名編輯時 createAction 不會被自動清理，使用後必須手動銷毀
                if (createAction != null)
                    ScriptableObject.DestroyImmediate(createAction);
            }

            if (!File.Exists(assetPath))
            {
                error = $"Failed to create the prefab: {assetPath}";
                Debug.LogError(error);
                return false;
            }

            return true;
        }
        #endregion
    }
}