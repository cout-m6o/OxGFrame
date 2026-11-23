using System;
using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace OxGFrame.CenterFrame.Editor
{
    public static class CenterFrameCreateScriptEditor
    {
        // EventCenter
        private const string _TPL_EVENT_BASE_SCRIPT_PATH = "TplScripts/EventCenter/TplEvent.cs.txt";
        private const string _TPL_EVENT_CENTER_SCRIPT_PATH = "TplScripts/EventCenter/TplEventCenter.cs.txt";

        // APICenter
        private const string _TPL_API_CENTER_SCRIPT_PATH = "TplScripts/APICenter/TplAPICenter.cs.txt";
        private const string _TPL_API_BASE_SCRIPT_PATH = "TplScripts/APICenter/TplAPI.cs.txt";

        // find current file path
        private static string _pathFinder
        {
            get
            {
                var g = AssetDatabase.FindAssets("t:Script CenterFrameCreateScriptEditor");
                return AssetDatabase.GUIDToAssetPath(g[0]);
            }
        }

        #region EventCenter Script Create
        [MenuItem(itemName: "Assets/Create/OxGFrame/Center Frame/Event Center/Template Scripts/Template Event.cs (Event)", isValidateFunction: false, priority: 51)]
        public static void CreateScriptTplEventBase()
        {
            string currentPath = _pathFinder;
            string finalPath = currentPath.Replace("CenterFrameCreateScriptEditor.cs", "") + _TPL_EVENT_BASE_SCRIPT_PATH;

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(finalPath, "NewTplEvent.cs");
        }

        [MenuItem(itemName: "Assets/Create/OxGFrame/Center Frame/Event Center/Template Scripts/Template EventCenter.cs (Event Center Manager)", isValidateFunction: false, priority: 51)]
        public static void CreateScriptTplEventCenter()
        {
            string currentPath = _pathFinder;
            string finalPath = currentPath.Replace("CenterFrameCreateScriptEditor.cs", "") + _TPL_EVENT_CENTER_SCRIPT_PATH;

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(finalPath, "NewTplEventCenter.cs");
        }
        #endregion

        #region APICenter Script Create
        [MenuItem(itemName: "Assets/Create/OxGFrame/Center Frame/API Center/Template Scripts/Template API.cs (API)", isValidateFunction: false, priority: 51)]
        public static void CreateScriptTplAPIBase()
        {
            string currentPath = _pathFinder;
            string finalPath = currentPath.Replace("CenterFrameCreateScriptEditor.cs", "") + _TPL_API_BASE_SCRIPT_PATH;

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(finalPath, "NewTplAPI.cs");
        }

        [MenuItem(itemName: "Assets/Create/OxGFrame/Center Frame/API Center/Template Scripts/Template APICenter.cs (API Center Manager)", isValidateFunction: false, priority: 51)]
        public static void CreateScriptTplAPICenter()
        {
            string currentPath = _pathFinder;
            string finalPath = currentPath.Replace("CenterFrameCreateScriptEditor.cs", "") + _TPL_API_CENTER_SCRIPT_PATH;

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(finalPath, "NewTplAPICenter.cs");
        }
        #endregion

        #region Automation
        /// <summary>
        /// 可用的 Template Script 名稱
        /// </summary>
        public static readonly string[] TEMPLATE_NAMES = new string[]
        {
            "event",
            "event_center",
            "api",
            "api_center"
        };

        /// <summary>
        /// 於指定路徑建立 Template Script (供 Unity CLI 與自動化流程調用)
        /// <para>執行 Unity 於 Project 視窗命名編輯結束後的建立動作，產出內容與由選單建立時相同，差異僅在於不進入命名編輯。</para>
        /// </summary>
        /// <param name="templateName">Template 名稱 (event, event_center, api, api_center)</param>
        /// <param name="assetPath">腳本的資源路徑 (ex: Assets/Scripts/LoginEvent.cs)</param>
        /// <param name="error">失敗原因 (成功時為 null)</param>
        /// <returns></returns>
        public static bool CreateScriptFromTemplate(string templateName, string assetPath, out string error)
        {
            string templatePath;
            switch (templateName)
            {
                case "event":
                    templatePath = _TPL_EVENT_BASE_SCRIPT_PATH;
                    break;
                case "event_center":
                    templatePath = _TPL_EVENT_CENTER_SCRIPT_PATH;
                    break;
                case "api":
                    templatePath = _TPL_API_BASE_SCRIPT_PATH;
                    break;
                case "api_center":
                    templatePath = _TPL_API_CENTER_SCRIPT_PATH;
                    break;
                default:
                    error = $"Unknown template name: \"{templateName}\" (available: {string.Join(", ", TEMPLATE_NAMES)}).";
                    Debug.LogError(error);
                    return false;
            }

            string currentPath = _pathFinder;
            string finalPath = currentPath.Replace("CenterFrameCreateScriptEditor.cs", "") + templatePath;

            if (!_CreateScriptAssetDirectly(finalPath, assetPath, out error))
            {
                Debug.LogError(error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 於指定路徑直接建立腳本 (不經過 Project 視窗的命名編輯)
        /// </summary>
        /// <param name="templatePath"></param>
        /// <param name="assetPath"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        private static bool _CreateScriptAssetDirectly(string templatePath, string assetPath, out string error)
        {
            if (!_PrepareCreateAssetPath(ref assetPath, ".cs", out error))
                return false;

            // 取得 Unity 內建的腳本建立動作 (即命名編輯結束後所執行的動作，確保產出內容與由選單建立時相同)
            Type actionType = typeof(EndNameEditAction).Assembly.GetType("UnityEditor.ProjectWindowCallback.DoCreateScriptAsset");
            if (actionType == null ||
                !typeof(EndNameEditAction).IsAssignableFrom(actionType))
            {
                error = "Cannot find the built-in script creation action in this Unity version. Please create the script from the menu instead (Assets/Create/OxGFrame).";
                return false;
            }

            var createAction = ScriptableObject.CreateInstance(actionType) as EndNameEditAction;
            try
            {
                createAction.Action(0, assetPath, templatePath);
            }
            finally
            {
                // 不經過命名編輯時 createAction 不會被自動清理，使用後必須手動銷毀
                if (createAction != null)
                    ScriptableObject.DestroyImmediate(createAction);
            }

            if (!File.Exists(assetPath))
            {
                error = $"Failed to create the script: {assetPath}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 檢查並準備欲建立資源的路徑
        /// <para>必須位於 Assets 或 Packages 內、副檔名相符且尚未存在；所屬資料夾不存在時自動建立 (僅限 Assets 內)。</para>
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="extension"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        private static bool _PrepareCreateAssetPath(ref string assetPath, string extension, out string error)
        {
            error = null;
            assetPath = string.IsNullOrEmpty(assetPath) ? string.Empty : assetPath.Replace('\\', '/');

            bool isInAssets = assetPath.StartsWith("Assets/", StringComparison.Ordinal);
            bool isInPackages = assetPath.StartsWith("Packages/", StringComparison.Ordinal);
            if ((!isInAssets && !isInPackages) ||
                !assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(assetPath)) ||
                Array.IndexOf(assetPath.Split('/'), "..") != -1)
            {
                error = $"Invalid asset path: \"{assetPath}\" (ex: Assets/Folder/Name{extension}).";
                return false;
            }

            if (File.Exists(assetPath))
            {
                error = $"The asset already exists: {assetPath}";
                return false;
            }

            string directory = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (!Directory.Exists(directory))
            {
                // Packages 內的路徑必須對應至既有的資料夾，避免建立出不屬於任何套件的資料夾
                if (!isInAssets)
                {
                    error = $"Cannot find the directory: {directory}";
                    return false;
                }

                // 建立後需重新整理，讓 Unity 先匯入該資料夾
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            return true;
        }
        #endregion
    }
}