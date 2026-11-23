using System;
using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace OxGFrame.CoreFrame.Editor
{
    public static class CoreFrameCreateScriptEditor
    {
        // SRFrame
        private const string _TPL_SR_SCRIPT_PATH = "TplScripts/SRFrame/TplSR.cs.txt";

        // UIFrame
        private const string _TPL_UI_SCRIPT_PATH = "TplScripts/UIFrame/TplUI.cs.txt";

        // CPFrame
        private const string _TPL_CP_SCRIPT_PATH = "TplScripts/CPFrame/TplCP.cs.txt";

        // find current file path
        private static string _pathFinder
        {
            get
            {
                var g = AssetDatabase.FindAssets("t:Script CoreFrameCreateScriptEditor");
                return AssetDatabase.GUIDToAssetPath(g[0]);
            }
        }

        #region SRFrame Script Create
        [MenuItem(itemName: "Assets/Create/OxGFrame/Core Frame/SR Frame/Template Scripts/Template SR.cs (For Scene Resource Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateScriptTplSR()
        {
            string currentPath = _pathFinder;
            string finalPath = currentPath.Replace("CoreFrameCreateScriptEditor.cs", "") + _TPL_SR_SCRIPT_PATH;

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(finalPath, "NewTplSR.cs");
        }
        #endregion

        #region UIFrame Script Create
        [MenuItem(itemName: "Assets/Create/OxGFrame/Core Frame/UI Frame/Template Scripts/Template UI.cs (For UGUI Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateScriptTplUI()
        {
            string currentPath = _pathFinder;
            string finalPath = currentPath.Replace("CoreFrameCreateScriptEditor.cs", "") + _TPL_UI_SCRIPT_PATH;

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(finalPath, "NewTplUI.cs");
        }
        #endregion

        #region CPFrame Script Create
        [MenuItem(itemName: "Assets/Create/OxGFrame/Core Frame/CP Frame/Template Scripts/Template CP.cs (For Clone Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateScriptTplCP()
        {
            string currentPath = _pathFinder;
            string finalPath = currentPath.Replace("CoreFrameCreateScriptEditor.cs", "") + _TPL_CP_SCRIPT_PATH;

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(finalPath, "NewTplCP.cs");
        }
        #endregion

        #region Automation
        /// <summary>
        /// 可用的 Template Script 名稱
        /// </summary>
        public static readonly string[] TEMPLATE_NAMES = new string[]
        {
            "ui",
            "sr",
            "cp"
        };

        /// <summary>
        /// 於指定路徑建立 Template Script (供 Unity CLI 與自動化流程調用)
        /// <para>執行 Unity 於 Project 視窗命名編輯結束後的建立動作，產出內容與由選單建立時相同，差異僅在於不進入命名編輯。</para>
        /// </summary>
        /// <param name="templateName">Template 名稱 (ui, sr, cp)</param>
        /// <param name="assetPath">腳本的資源路徑 (ex: Assets/Scripts/ShopUI.cs)</param>
        /// <param name="error">失敗原因 (成功時為 null)</param>
        /// <returns></returns>
        public static bool CreateScriptFromTemplate(string templateName, string assetPath, out string error)
        {
            string templatePath;
            switch (templateName)
            {
                case "ui":
                    templatePath = _TPL_UI_SCRIPT_PATH;
                    break;
                case "sr":
                    templatePath = _TPL_SR_SCRIPT_PATH;
                    break;
                case "cp":
                    templatePath = _TPL_CP_SCRIPT_PATH;
                    break;
                default:
                    error = $"Unknown template name: \"{templateName}\" (available: {string.Join(", ", TEMPLATE_NAMES)}).";
                    Debug.LogError(error);
                    return false;
            }

            string currentPath = _pathFinder;
            string finalPath = currentPath.Replace("CoreFrameCreateScriptEditor.cs", "") + templatePath;

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
            if (!AutomationUtility.PrepareCreateAssetPath(ref assetPath, ".cs", out error))
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
        #endregion
    }
}