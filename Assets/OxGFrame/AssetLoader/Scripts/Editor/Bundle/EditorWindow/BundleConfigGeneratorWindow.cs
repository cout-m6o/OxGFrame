using Newtonsoft.Json;
using OxGFrame.AssetLoader.Bundle;
using OxGFrame.AssetLoader.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;
using static OxGFrame.AssetLoader.Bundle.AppConfig;
#if YOOASSET_3
// [YooAsset v3] 類名更名轉接
using AssetBundleBuilderHelper = YooAsset.Editor.BundleBuilderHelper;
using EditorTools = YooAsset.Editor.EditorPathUtility;
#endif

namespace OxGFrame.AssetLoader.Editor
{
    public class BundleConfigGeneratorWindow : EditorWindow
    {
        public enum OperationType
        {
            ExportAppConfigToStreamingAssets = 0,
            ExportConfigsAndAppBundlesForCDN = 1,
            ExportAppBundlesWithoutConfigsForCDN = 2,
            ExportIndividualDLCBundlesForCDN = 3
        }

        private static BundleConfigGeneratorWindow _instance = null;
        internal static BundleConfigGeneratorWindow GetInstance()
        {
            if (_instance == null)
                _instance = GetWindow<BundleConfigGeneratorWindow>();
            return _instance;
        }

        [SerializeField]
        public OperationType operationType;
        [SerializeField]
        public string[] sourceFolder = new string[Enum.GetNames(typeof(OperationType)).Length];
        [SerializeField]
        public string[] exportFolder = new string[Enum.GetNames(typeof(OperationType)).Length];
        [SerializeField]
        public BuildTarget buildTarget;
        [SerializeField]
        public bool activeBuildTarget;
        [SerializeField]
        public string productName;
        [SerializeField]
        public string appVersion;
        [SerializeField]
        public SemanticRule semanticRule;
        [SerializeField]
        public List<string> exportAppPackages = new List<string>() { "DefaultPackage" };
        [SerializeField]
        public List<DlcInfo> exportIndividualPackages = new List<DlcInfo>();
        [SerializeField]
        public string groupInfoArgs;
        [SerializeField]
        public List<GroupInfo> groupInfos = new List<GroupInfo>();
        [SerializeField]
        public bool autoReveal;

        // Preset Plan
        public List<BundlePlan> bundlePlans = new List<BundlePlan>();
        private int _choicePlanIndex = 0;

        private Vector2 _scrollview1;
        private Vector2 _scrollview2;

        private SerializedObject _serObj;
        private SerializedProperty _groupInfosPty;
        private SerializedProperty _exportAppPackagesPty;
        private SerializedProperty _exportIndividualPackagesPty;
        private SerializedProperty _bundlePlansPty;

        /// <summary>
        /// 背景樣式緩存 (Key 為色碼，所建立的貼圖於 OnDisable 時銷毀)
        /// </summary>
        private readonly Dictionary<string, GUIStyle> _backgroundStyles = new Dictionary<string, GUIStyle>();

        internal static string projectPath;
        internal static string keySaver;

        private static Vector2 _windowSize = new Vector2(800f, 400f);

        [MenuItem(BundleHelper.MENU_ROOT + "Export Bundle And Config Generator", false, 889)]
        public static void ShowWindow()
        {
            projectPath = Application.dataPath;
            keySaver = $"{projectPath}_{nameof(BundleConfigGeneratorWindow)}";

            _instance = null;
            GetInstance().titleContent = new GUIContent("Export Bundle And Config Generator");
            GetInstance().Show();
            GetInstance().minSize = _windowSize;
        }

        private void OnEnable()
        {
            // Domain Reload (重新編譯、進入 Play Mode) 或 Layout 還原後，靜態欄位會被清空，需補回儲存鍵值 (與 ShowWindow 相同)
            if (string.IsNullOrEmpty(keySaver))
            {
                projectPath = Application.dataPath;
                keySaver = $"{projectPath}_{nameof(BundleConfigGeneratorWindow)}";
            }

            this._serObj = new SerializedObject(this);
            this._groupInfosPty = this._serObj.FindProperty("groupInfos");
            this._exportAppPackagesPty = this._serObj.FindProperty("exportAppPackages");
            this._exportIndividualPackagesPty = this._serObj.FindProperty("exportIndividualPackages");
            this._bundlePlansPty = this._serObj.FindProperty("bundlePlans");

            int operationTypeCount = Enum.GetNames(typeof(OperationType)).Length;
            for (int i = 0; i < operationTypeCount; i++)
            {
                this.sourceFolder[i] = EditorStorage.GetData(keySaver, $"sourceFolder{i}", Path.Combine($"{Application.dataPath}/", AssetBundleBuilderHelper.GetDefaultBuildOutputRoot()));
                this.exportFolder[i] = EditorStorage.GetData(keySaver, $"exportFolder{i}", Path.Combine($"{Application.dataPath}/", $"{EditorTools.GetProjectPath()}/ExportBundles"));
            }

            this.operationType = (OperationType)Convert.ToInt32(EditorStorage.GetData(keySaver, "operationType", "0"));
            this.productName = EditorStorage.GetData(keySaver, "productName", Application.productName);
            this.appVersion = EditorStorage.GetData(keySaver, "appVersion", Application.version);
            string jsonExportAppPackages = EditorStorage.GetData(keySaver, "exportAppPackages", string.Empty);
            if (!string.IsNullOrEmpty(jsonExportAppPackages)) this.exportAppPackages = JsonConvert.DeserializeObject<List<string>>(jsonExportAppPackages);
            string jsonExportIndividualPackages = EditorStorage.GetData(keySaver, "exportIndividualPackages", string.Empty);
            if (!string.IsNullOrEmpty(jsonExportIndividualPackages)) this.exportIndividualPackages = JsonConvert.DeserializeObject<List<DlcInfo>>(jsonExportIndividualPackages);
            this.groupInfoArgs = EditorStorage.GetData(keySaver, "groupInfoArgs", string.Empty);
            string jsonGroupInfos = EditorStorage.GetData(keySaver, "groupInfos", string.Empty);
            if (!string.IsNullOrEmpty(jsonGroupInfos)) this.groupInfos = JsonConvert.DeserializeObject<List<GroupInfo>>(jsonGroupInfos);

            this.buildTarget = (BuildTarget)Convert.ToInt32(EditorStorage.GetData(keySaver, "buildTarget", $"{(int)BuildTarget.StandaloneWindows64}"));
            // 儲存值若非有效的 BuildTarget (舊版曾誤存為 operationType 的值)，則回退至預設值
            if (!Enum.IsDefined(typeof(BuildTarget), this.buildTarget)) this.buildTarget = BuildTarget.StandaloneWindows64;
            this.activeBuildTarget = Convert.ToBoolean(EditorStorage.GetData(keySaver, "activeBuildTarget", "true"));

            this.autoReveal = Convert.ToBoolean(EditorStorage.GetData(keySaver, "autoReveal", "true"));

            this.semanticRule.MAJOR = true;
            this.semanticRule.MINOR = true;
            this.semanticRule.PATCH = Convert.ToBoolean(EditorStorage.GetData(keySaver, "semanticRule.PATCH", "false"));

            // Preset Bundle Plans
            string jsonBundlePlans = EditorStorage.GetData(keySaver, "bundlePlans", string.Empty);
            // 內容為 null 的 Json 會反序列化為 null，需以空列表取代 (避免後續 OnGUI 拋出 NullReferenceException)
            if (!string.IsNullOrEmpty(jsonBundlePlans)) this.bundlePlans = JsonConvert.DeserializeObject<List<BundlePlan>>(jsonBundlePlans) ?? new List<BundlePlan>();
            this._choicePlanIndex = Convert.ToInt32(EditorStorage.GetData(keySaver, "_choicePlanIndex", "0"));
        }

        private void OnDisable()
        {
            // 銷毀背景樣式緩存所建立的貼圖 (HideAndDontSave 的貼圖需自行銷毀)
            foreach (var style in this._backgroundStyles.Values)
            {
                if (style.normal.background != null) DestroyImmediate(style.normal.background);
            }
            this._backgroundStyles.Clear();
        }

        /// <summary>
        /// 取得純色背景樣式 (依色碼緩存 1x1 貼圖的樣式，避免每次 OnGUI 事件重複建立)
        /// </summary>
        /// <param name="htmlColor"></param>
        /// <returns></returns>
        private GUIStyle _GetBackgroundStyle(string htmlColor)
        {
            // 緩存的貼圖失效時 (例如已被銷毀)，會重新建立
            if (this._backgroundStyles.TryGetValue(htmlColor, out GUIStyle style) && style.normal.background != null) return style;

            style = new GUIStyle();
            var bg = new Texture2D(1, 1);
            bg.hideFlags = HideFlags.HideAndDontSave;
            ColorUtility.TryParseHtmlString(htmlColor, out Color color);
            bg.SetPixel(0, 0, color);
            bg.Apply();
            style.normal.background = bg;
            this._backgroundStyles[htmlColor] = style;

            return style;
        }

        private void OnGUI()
        {
            // operation type area
            EditorGUI.BeginChangeCheck();
            this.operationType = (OperationType)EditorGUILayout.EnumPopup("Operation Type", this.operationType);
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "operationType", ((int)this.operationType).ToString());
            this._OperationType(this.operationType);
        }

        private OperationType _lastOperationType;
        private void _OperationType(OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.ExportAppConfigToStreamingAssets:
                    this._DrawExportAppConfigToStreamingAssetsView();
                    if (this._lastOperationType != OperationType.ExportAppConfigToStreamingAssets)
                    {
                        float minHeight = _windowSize.y - 120f;
                        this.minSize = new Vector2(_windowSize.x, minHeight);
                        // window 由大變小需要設置 postion
                        this.position = new Rect(this.position.position.x, this.position.position.y, _windowSize.x, minHeight);
                    }
                    break;
                case OperationType.ExportConfigsAndAppBundlesForCDN:
                    this._DrawExportConfigsAndAppBundlesForCDNView();
                    if (this._lastOperationType != OperationType.ExportConfigsAndAppBundlesForCDN)
                    {
                        this.minSize = new Vector2(_windowSize.x, _windowSize.y);
                    }
                    break;
                case OperationType.ExportAppBundlesWithoutConfigsForCDN:
                    this._DrawExportAppBundlesWithoutConfigsForCDNView();
                    if (this._lastOperationType != OperationType.ExportAppBundlesWithoutConfigsForCDN)
                    {
                        this.minSize = new Vector2(_windowSize.x, _windowSize.y);
                    }
                    break;
                case OperationType.ExportIndividualDLCBundlesForCDN:
                    this._DrawExportIndividualDlcBundlesForCDNView();
                    if (this._lastOperationType != OperationType.ExportIndividualDLCBundlesForCDN)
                    {
                        this.minSize = new Vector2(_windowSize.x, _windowSize.y);
                    }
                    break;
            }

            this._lastOperationType = operationType;
        }

        private void _DrawExportAppConfigToStreamingAssetsView()
        {
            this._scrollview1 = EditorGUILayout.BeginScrollView(this._scrollview1, true, true);

            EditorGUILayout.Space();

            GUIStyle style = this._GetBackgroundStyle("#1e3836");
            EditorGUILayout.BeginVertical(style);

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent("Export AppConfig To StreamingAssets"), centeredStyle);
            EditorGUILayout.Space();

            // draw here
            this._DrawBuildTargetView();
            this._DrawProductNameTextFieldView();
            this._DrawAppVersionTextFieldView();
            this._DrawProcessButtonView(this.operationType);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        private void _DrawExportConfigsAndAppBundlesForCDNView()
        {
            this._scrollview1 = EditorGUILayout.BeginScrollView(this._scrollview1, true, true);

            EditorGUILayout.Space();

            GUIStyle style = this._GetBackgroundStyle("#1e3836");
            EditorGUILayout.BeginVertical(style);

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent("Export Configs And App Bundles For CDN"), centeredStyle);
            EditorGUILayout.Space();

            // draw here
            this._DrawBuildTargetView();
            this._DrawSourceFolderView();
            this._DrawProductNameTextFieldView();
            this._DrawAppVersionTextFieldView();
            this._DrawSemanticRuleView();
            this._DrawExportFolderView();
            this._DrawExportAppPackagesView();
            this._DrawExportIndividualPackagesView();
            this._DrawGroupInfosView();
            this._DrawProcessButtonView(this.operationType);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            this._DrawBundlePlansView();
        }

        private void _DrawExportAppBundlesWithoutConfigsForCDNView()
        {
            this._scrollview1 = EditorGUILayout.BeginScrollView(this._scrollview1, true, true);

            EditorGUILayout.Space();

            GUIStyle style = this._GetBackgroundStyle("#1e3836");
            EditorGUILayout.BeginVertical(style);

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent("Export App Bundles Without Configs For CDN"), centeredStyle);
            EditorGUILayout.Space();

            // draw here
            this._DrawBuildTargetView();
            this._DrawSourceFolderView();
            this._DrawProductNameTextFieldView();
            this._DrawAppVersionTextFieldView();
            this._DrawSemanticRuleView();
            this._DrawExportFolderView();
            this._DrawExportAppPackagesView();
            this._DrawExportIndividualPackagesView();
            this._DrawProcessButtonView(this.operationType);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            this._DrawBundlePlansView();
        }

        private void _DrawExportIndividualDlcBundlesForCDNView()
        {
            this._scrollview1 = EditorGUILayout.BeginScrollView(this._scrollview1, true, true);

            EditorGUILayout.Space();

            GUIStyle style = this._GetBackgroundStyle("#1e3836");
            EditorGUILayout.BeginVertical(style);

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent("Export Individual DLC Bundles For CDN"), centeredStyle);
            EditorGUILayout.Space();

            // draw here
            this._DrawBuildTargetView();
            this._DrawSourceFolderView();
            this._DrawProductNameTextFieldView();
            this._DrawExportFolderView();
            this._DrawExportIndividualPackagesView();
            this._DrawProcessButtonView(this.operationType);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            this._DrawBundlePlansView();
        }

        private void _DrawSourceFolderView()
        {
            EditorGUILayout.Space();

            // source folder area
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            this.sourceFolder[(int)this.operationType] = EditorGUILayout.TextField("Source Folder (YooAsset)", this.sourceFolder[(int)this.operationType]);
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, $"sourceFolder{(int)this.operationType}", this.sourceFolder[(int)this.operationType]);
            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(0, 255, 128, 255);
            if (GUILayout.Button("Open", GUILayout.MaxWidth(100f))) BundleUtility.OpenFolder(this.sourceFolder[(int)this.operationType], true);
            GUI.backgroundColor = bc;
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(83, 152, 255, 255);
            if (GUILayout.Button("Browse", GUILayout.MaxWidth(100f))) this._OpenSourceFolder();
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();
        }

        private void _DrawExportFolderView()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            this.exportFolder[(int)this.operationType] = EditorGUILayout.TextField("Export Folder", this.exportFolder[(int)this.operationType]);
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, $"exportFolder{(int)this.operationType}", this.exportFolder[(int)this.operationType]);
            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(0, 255, 128, 255);
            if (GUILayout.Button("Open", GUILayout.MaxWidth(100f))) BundleUtility.OpenFolder(this.exportFolder[(int)this.operationType], true);
            GUI.backgroundColor = bc;
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(83, 152, 255, 255);
            if (GUILayout.Button("Browse", GUILayout.MaxWidth(100f))) this._OpenExportFolder();
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();
        }

        private void _DrawBuildTargetView()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (!this.activeBuildTarget)
            {
                // build target type
                EditorGUI.BeginChangeCheck();
                this.buildTarget = (BuildTarget)EditorGUILayout.EnumPopup("Build Target", this.buildTarget);
                if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "buildTarget", ((int)this.buildTarget).ToString());
            }

            // active build target toggle
            EditorGUI.BeginChangeCheck();
            this.activeBuildTarget = GUILayout.Toggle(this.activeBuildTarget, new GUIContent("Use Active Build Target", "If checked, will use active build target."));
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "activeBuildTarget", this.activeBuildTarget.ToString());

            EditorGUILayout.EndHorizontal();
        }

        private void _DrawProductNameTextFieldView()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            this.productName = EditorGUILayout.TextField("Product Name", this.productName);
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "productName", this.productName);

            // Sync button
            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 238, 36, 255);
            if (GUILayout.Button("Sync to Player Settings", GUILayout.MaxWidth(202f)))
            {
                bool confirmation = EditorUtility.DisplayDialog
                (
                    $"Sync Product Name Notification",
                    $"Do you want to sync Product Name: {this.productName} to Player Settings?",
                    "yes",
                    "cancel"
                );

                if (confirmation) PlayerSettings.productName = this.productName;
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();
        }

        private void _DrawAppVersionTextFieldView()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            this.appVersion = EditorGUILayout.TextField("App Version", this.appVersion);
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "appVersion", this.appVersion);

            // Sync button
            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 238, 36, 255);
            if (GUILayout.Button("Sync to Player Settings", GUILayout.MaxWidth(202f)))
            {
                bool confirmation = EditorUtility.DisplayDialog
                (
                    $"Sync App Version Notification",
                    $"Do you want to sync App Version: {this.appVersion} to Player Settings?",
                    "yes",
                    "cancel"
                );

                if (confirmation) PlayerSettings.bundleVersion = this.appVersion;
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();
        }

        private void _DrawSemanticRuleView()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(new GUIContent("Semantic Rule", "[This option applies only to the AppConfig on the Host Server] If the Patch option is checked, the folder will be output with the full version number (to prevent overwriting resources from different versions)."), GUILayout.MaxWidth(147.5f));

            EditorGUI.BeginDisabledGroup(true);
            this.semanticRule.MAJOR = GUILayout.Toggle(this.semanticRule.MAJOR, new GUIContent("Major", ""), GUILayout.MaxWidth(75f));
            this.semanticRule.MINOR = GUILayout.Toggle(this.semanticRule.MINOR, new GUIContent("Minor", ""), GUILayout.MaxWidth(75f));
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginChangeCheck();
            this.semanticRule.PATCH = GUILayout.Toggle(this.semanticRule.PATCH, new GUIContent("Patch", ""), GUILayout.MaxWidth(75f));
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "semanticRule.PATCH", this.semanticRule.PATCH.ToString());

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void _DrawExportAppPackagesView(string title = "Export App Packages")
        {
            EditorGUILayout.Space();

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent(title), centeredStyle);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            this._serObj.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(this._exportAppPackagesPty, true);
            if (EditorGUI.EndChangeCheck())
            {
                this._serObj.ApplyModifiedProperties();
                string json = JsonConvert.SerializeObject(this.exportAppPackages);
                EditorStorage.SaveData(keySaver, "exportAppPackages", json);
            }

            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 151, 240, 255);
            if (GUILayout.Button("Reset", GUILayout.MaxWidth(100f)))
            {
                this.exportAppPackages = new List<string>() { "DefaultPackage" };
                string json = JsonConvert.SerializeObject(this.exportAppPackages);
                EditorStorage.SaveData(keySaver, "exportAppPackages", json);
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void _DrawExportIndividualPackagesView(string title = "Export Individual DLC Packages")
        {
            EditorGUILayout.Space();

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent(title), centeredStyle);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            this._serObj.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(this._exportIndividualPackagesPty, true);
            if (EditorGUI.EndChangeCheck())
            {
                this._serObj.ApplyModifiedProperties();
                string json = JsonConvert.SerializeObject(this.exportIndividualPackages);
                EditorStorage.SaveData(keySaver, "exportIndividualPackages", json);
            }

            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 80, 106, 255);
            if (GUILayout.Button("Clear", GUILayout.MaxWidth(100f)))
            {
                this.exportIndividualPackages = new List<DlcInfo>();
                string json = JsonConvert.SerializeObject(this.exportIndividualPackages);
                EditorStorage.SaveData(keySaver, "exportIndividualPackages", json);
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private bool _isParsingDirty = false;
        private bool _isConvertDirty = false;
        private void _DrawGroupInfosView()
        {
            EditorGUILayout.Space();

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent("Group Tags For Preset Packages (Main Download)"), centeredStyle);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            string placeholder = "g1,t1#g2,t1,t2#g3,t1,t2,t3";
            this.groupInfoArgs = EditorGUILayout.TextField(new GUIContent("Group Info Args", $"Group split by '#' and Args split by ','\n{placeholder}"), this.groupInfoArgs);
            if (EditorGUI.EndChangeCheck())
            {
                this._isParsingDirty = true;
                EditorStorage.SaveData(keySaver, "groupInfoArgs", this.groupInfoArgs);
            }
            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 151, 240, 255);
            if (GUILayout.Button("Reset", GUILayout.MaxWidth(100f)))
            {
                this._isParsingDirty = true;
                this.groupInfoArgs = string.Empty;
                EditorStorage.SaveData(keySaver, "groupInfoArgs", this.groupInfoArgs);
            }
            GUI.backgroundColor = bc;
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(0, 255, 128, 255);
            EditorGUI.BeginDisabledGroup(!this._isParsingDirty);
            if (GUILayout.Button("Parsing", GUILayout.MaxWidth(100f)))
            {
                this.groupInfos = BundleHelper.ParsingGroupInfosByArgs(this.groupInfoArgs);
                string json = JsonConvert.SerializeObject(this.groupInfos);
                EditorStorage.SaveData(keySaver, "groupInfos", json);
                this._isParsingDirty = false;
                this._isConvertDirty = false;
            }
            GUI.backgroundColor = bc;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            this._serObj.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(this._groupInfosPty, true);
            if (EditorGUI.EndChangeCheck())
            {
                this._isConvertDirty = true;
                this._serObj.ApplyModifiedProperties();
                string json = JsonConvert.SerializeObject(this.groupInfos);
                EditorStorage.SaveData(keySaver, "groupInfos", json);
            }
            GUI.backgroundColor = bc;
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 80, 106, 255);
            if (GUILayout.Button("Clear", GUILayout.MaxWidth(100f)))
            {
                this.groupInfos = new List<GroupInfo>();
                this._isParsingDirty = true;
                string json = JsonConvert.SerializeObject(this.groupInfos);
                EditorStorage.SaveData(keySaver, "groupInfos", json);
            }
            GUI.backgroundColor = bc;

            EditorGUI.BeginDisabledGroup(!this._isConvertDirty);
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(0, 255, 128, 255);
            if (GUILayout.Button("Convert", GUILayout.MaxWidth(100f)))
            {
                this.groupInfoArgs = BundleHelper.ConvertGroupInfosToArgs(this.groupInfos);
                EditorStorage.SaveData(keySaver, "groupInfoArgs", this.groupInfoArgs);
                this._isConvertDirty = false;
            }
            GUI.backgroundColor = bc;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void _DrawProcessButtonView(OperationType operationType)
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            // auto reveal toggle
            EditorGUI.BeginChangeCheck();
            this.autoReveal = GUILayout.Toggle(this.autoReveal, new GUIContent("Auto Reveal", "If checked, after process will reveal folder."));
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "autoReveal", this.autoReveal.ToString());

            string inputPath;
            string outputPath;

            // process button
            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 185, 83, 255);
            if (GUILayout.Button("Process", GUILayout.MaxWidth(100f)))
            {
                switch (operationType)
                {
                    case OperationType.ExportAppConfigToStreamingAssets:
                        outputPath = Application.streamingAssetsPath;
                        BundleHelper.ExportAppConfig(this.productName, this.appVersion, outputPath, this.activeBuildTarget, this.buildTarget);
                        EditorUtility.DisplayDialog("Process Message", "Export AppConfig To StreamingAssets.", "OK");
                        AssetDatabase.Refresh();
                        string appCfgFileName = $"{PatchSettings.settings.appCfgName}{PatchSettings.settings.appCfgExtension}";
                        if (this.autoReveal) EditorUtility.RevealInFinder($"{outputPath}/{appCfgFileName}");
                        break;
                    case OperationType.ExportConfigsAndAppBundlesForCDN:
                        // 來源或輸出資料夾為空時不進行處理 (避免以空路徑組合出磁碟根目錄下的輸出路徑，導致清空非預期的資料夾)
                        if (!this._CheckSourceAndExportFolder()) break;
                        inputPath = this.sourceFolder[(int)this.operationType];
                        outputPath = $"{this.exportFolder[(int)this.operationType]}/{PatchSettings.settings.rootFolderName}";
                        List<string> exportDlcPackages = new List<string>();
                        foreach (var dlcPackage in this.exportIndividualPackages) exportDlcPackages.Add(dlcPackage.packageName);
                        string[] packageInfos = this.exportAppPackages.Union(exportDlcPackages).ToArray();
                        BundleHelper.ExportConfigsAndAppBundles(inputPath, outputPath, this.productName, this.semanticRule, this.appVersion, this.exportAppPackages.ToArray(), this.groupInfos, packageInfos, this.activeBuildTarget, this.buildTarget, true);
                        BundleHelper.ExportIndividualDlcBundles(inputPath, outputPath, this.productName, this.exportIndividualPackages, this.activeBuildTarget, this.buildTarget, false);
                        EditorUtility.DisplayDialog("Process Message", "Export Configs And App Bundles For CDN.", "OK");
                        if (this.autoReveal) EditorUtility.RevealInFinder(outputPath);
                        break;
                    case OperationType.ExportAppBundlesWithoutConfigsForCDN:
                        // 來源或輸出資料夾為空時不進行處理 (避免以空路徑組合出磁碟根目錄下的輸出路徑，導致清空非預期的資料夾)
                        if (!this._CheckSourceAndExportFolder()) break;
                        inputPath = this.sourceFolder[(int)this.operationType];
                        outputPath = $"{this.exportFolder[(int)this.operationType]}/{PatchSettings.settings.rootFolderName}";
                        BundleHelper.ExportAppBundles(inputPath, outputPath, this.productName, this.semanticRule, this.appVersion, this.exportAppPackages.ToArray(), this.activeBuildTarget, this.buildTarget, true);
                        BundleHelper.ExportIndividualDlcBundles(inputPath, outputPath, this.productName, this.exportIndividualPackages, this.activeBuildTarget, this.buildTarget, false);
                        EditorUtility.DisplayDialog("Process Message", "Export App Bundles For CDN Without Configs.", "OK");
                        if (this.autoReveal) EditorUtility.RevealInFinder(outputPath);
                        break;
                    case OperationType.ExportIndividualDLCBundlesForCDN:
                        // 來源或輸出資料夾為空時不進行處理 (避免以空路徑組合出磁碟根目錄下的輸出路徑，導致清空非預期的資料夾)
                        if (!this._CheckSourceAndExportFolder()) break;
                        inputPath = this.sourceFolder[(int)this.operationType];
                        outputPath = $"{this.exportFolder[(int)this.operationType]}/{PatchSettings.settings.rootFolderName}";
                        BundleHelper.ExportIndividualDlcBundles(inputPath, outputPath, this.productName, this.exportIndividualPackages, this.activeBuildTarget, this.buildTarget, true);
                        EditorUtility.DisplayDialog("Process Message", "Export Individual DLC Bundles For CDN.", "OK");
                        if (this.autoReveal) EditorUtility.RevealInFinder(outputPath);
                        break;
                }
            }
            GUI.backgroundColor = bc;

            EditorGUILayout.EndHorizontal();
        }

        #region Preset Bundle Plans
        private void _DrawBundlePlansView()
        {
            this._scrollview2 = EditorGUILayout.BeginScrollView(this._scrollview2, true, true);

            EditorGUILayout.Space();

            GUIStyle style = this._GetBackgroundStyle("#263840");
            EditorGUILayout.BeginVertical(style);

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent("Preset Bundle Plans"), centeredStyle);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            // Add popup selection
            List<string> planNames = new List<string>();
            if (this.bundlePlans.Count > 0)
            {
                foreach (var bundlePlan in this.bundlePlans)
                {
                    planNames.Add(bundlePlan.planName);
                }
            }
            EditorGUI.BeginChangeCheck();
            this._choicePlanIndex = EditorGUILayout.Popup("Plan Selection", this._choicePlanIndex, planNames.ToArray());
            if (this._choicePlanIndex < 0) this._choicePlanIndex = 0;
            if (EditorGUI.EndChangeCheck())
            {
                EditorStorage.SaveData(keySaver, "_choicePlanIndex", this._choicePlanIndex.ToString());
            }

            // Load selection button
            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(83, 152, 255, 255);
            // 無任何 Plan 時不處理，並先將索引修正至有效範圍內 (避免以超出範圍的索引存取 Plans)
            if (GUILayout.Button("Copy Current", GUILayout.MaxWidth(100f)) && this._ClampChoicePlanIndex())
            {
                bool confirmation = EditorUtility.DisplayDialog
                (
                    $"Copy Current Notification",
                    $"The plan selection is [{this.bundlePlans[this._choicePlanIndex].planName}]\nDo you want to copy current all values?",
                    "copy current and override",
                    "cancel"
                );

                if (confirmation) this._CopyCurrentToBundlePlan();
            }
            GUI.backgroundColor = bc;
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(0, 255, 128, 255);
            if (GUILayout.Button("Load Plan", GUILayout.MaxWidth(100f)))
            {
                this._LoadBundlePlanToCurrent();
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            this._serObj.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(this._bundlePlansPty, true);
            if (EditorGUI.EndChangeCheck())
            {
                this._serObj.ApplyModifiedProperties();
                string json = JsonConvert.SerializeObject(this.bundlePlans);
                EditorStorage.SaveData(keySaver, "bundlePlans", json);
            }

            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 151, 240, 255);
            if (GUILayout.Button("Reset", GUILayout.MaxWidth(100f)))
            {
                bool confirmation = EditorUtility.DisplayDialog
                (
                    $"Reset Bundle Plans Notification",
                    $"Do you want to reset bundle plans?",
                    "reset",
                    "cancel"
                );

                if (confirmation)
                {
                    // Reset bundle plans
                    this.bundlePlans = new List<BundlePlan>() { new BundlePlan() };
                    string json = JsonConvert.SerializeObject(this.bundlePlans);
                    EditorStorage.SaveData(keySaver, "bundlePlans", json);

                    // Reset index
                    this._choicePlanIndex = 0;
                    EditorStorage.SaveData(keySaver, "_choicePlanIndex", this._choicePlanIndex.ToString());
                }
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            // Save File
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 220, 0, 255);
            if (GUILayout.Button("Save File", GUILayout.MaxWidth(100f)))
            {
                this._ExportBundlePlans();
            }
            GUI.backgroundColor = bc;
            // Load File
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(0, 249, 255, 255);
            if (GUILayout.Button("Load File", GUILayout.MaxWidth(100f)))
            {
                this._ImportBundlePlans();
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }
        #endregion

        private void _OpenSourceFolder()
        {
            string folderPath = EditorStorage.GetData(keySaver, $"sourceFolder{(int)this.operationType}", Application.dataPath);
            // 取消選取時會返回空字串，此時保留原有路徑 (避免後續處理以空路徑為目標)
            string selectedPath = EditorUtility.OpenFolderPanel("Open Source Folder", folderPath, string.Empty);
            if (string.IsNullOrEmpty(selectedPath)) return;
            this.sourceFolder[(int)this.operationType] = selectedPath;
            EditorStorage.SaveData(keySaver, $"sourceFolder{(int)this.operationType}", this.sourceFolder[(int)this.operationType]);
        }

        private void _OpenExportFolder()
        {
            string folderPath = EditorStorage.GetData(keySaver, $"exportFolder{(int)this.operationType}", Application.dataPath);
            // 取消選取時會返回空字串，此時保留原有路徑 (避免後續處理以空路徑為目標)
            string selectedPath = EditorUtility.OpenFolderPanel("Open Export Folder", folderPath, string.Empty);
            if (string.IsNullOrEmpty(selectedPath)) return;
            this.exportFolder[(int)this.operationType] = selectedPath;
            EditorStorage.SaveData(keySaver, $"exportFolder{(int)this.operationType}", this.exportFolder[(int)this.operationType]);
        }

        /// <summary>
        /// 檢查當前操作類型的來源與輸出資料夾是否皆已指定，未指定時會提示並返回 false
        /// </summary>
        /// <returns></returns>
        private bool _CheckSourceAndExportFolder()
        {
            if (string.IsNullOrWhiteSpace(this.sourceFolder[(int)this.operationType]) ||
                string.IsNullOrWhiteSpace(this.exportFolder[(int)this.operationType]))
            {
                EditorUtility.DisplayDialog("Process Message", "Please specify both the Source Folder and the Export Folder first.", "OK");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 將 Plan 索引修正至有效範圍內 (Plans 被移除後，索引可能殘留於範圍外)，無任何 Plan 時返回 false
        /// </summary>
        /// <returns></returns>
        private bool _ClampChoicePlanIndex()
        {
            if (this.bundlePlans.Count == 0) return false;

            int choicePlanIndex = Mathf.Clamp(this._choicePlanIndex, 0, this.bundlePlans.Count - 1);
            if (choicePlanIndex != this._choicePlanIndex)
            {
                this._choicePlanIndex = choicePlanIndex;
                EditorStorage.SaveData(keySaver, "_choicePlanIndex", this._choicePlanIndex.ToString());
            }

            return true;
        }

        private void _LoadBundlePlanToCurrent()
        {
            if (!this._ClampChoicePlanIndex()) return;

            var bundlePlan = this.bundlePlans[this._choicePlanIndex];

            this.exportAppPackages = bundlePlan.appPackages.ToList();
            this.groupInfoArgs = bundlePlan.groupInfoArgs;
            this.groupInfos = bundlePlan.groupInfos.ToList();
            this.exportIndividualPackages = bundlePlan.individualPackages.ToList();

            // Save
            string json = JsonConvert.SerializeObject(this.exportAppPackages);
            EditorStorage.SaveData(keySaver, "exportAppPackages", json);
            EditorStorage.SaveData(keySaver, "groupInfoArgs", this.groupInfoArgs);
            json = JsonConvert.SerializeObject(this.groupInfos);
            EditorStorage.SaveData(keySaver, "groupInfos", json);
            json = JsonConvert.SerializeObject(this.exportIndividualPackages);
            EditorStorage.SaveData(keySaver, "exportIndividualPackages", json);
        }

        private void _CopyCurrentToBundlePlan()
        {
            if (!this._ClampChoicePlanIndex()) return;

            var bundlePlan = this.bundlePlans[this._choicePlanIndex];

            // Copy
            bundlePlan.appPackages = this.exportAppPackages.ToList();
            bundlePlan.groupInfoArgs = this.groupInfoArgs;
            bundlePlan.groupInfos = this.groupInfos.ToList();
            bundlePlan.individualPackages = this.exportIndividualPackages.ToList();

            this.bundlePlans[this._choicePlanIndex] = bundlePlan;

            // Save
            string json = JsonConvert.SerializeObject(this.bundlePlans);
            EditorStorage.SaveData(keySaver, "bundlePlans", json);
        }

        private void _ExportBundlePlans()
        {
            string savePath = EditorStorage.GetData(keySaver, $"bundlePlanFilePath", Application.dataPath);
            var filePath = EditorUtility.SaveFilePanel("Save Bundle Plan Json File", savePath, "BundlePlan", "json");

            if (!string.IsNullOrEmpty(filePath))
            {
                EditorStorage.SaveData(keySaver, $"bundlePlanFilePath", Path.GetDirectoryName(filePath));
                string json = JsonConvert.SerializeObject(this.bundlePlans, Formatting.Indented);
                BundleHelper.WriteTxt(json, filePath);
                AssetDatabase.Refresh();
            }
        }

        private void _ImportBundlePlans()
        {
            string loadPath = EditorStorage.GetData(keySaver, $"bundlePlanFilePath", Application.dataPath);
            string filePath = EditorUtility.OpenFilePanel("Select Bundle Plan Json File", !string.IsNullOrEmpty(loadPath) ? loadPath : Application.dataPath, "json");

            if (!string.IsNullOrEmpty(filePath))
            {
                EditorStorage.SaveData(keySaver, $"bundlePlanFilePath", Path.GetDirectoryName(filePath));
                string json = File.ReadAllText(filePath);
                // 空檔案或內容為 null 的 Json 會反序列化為 null，需以空列表取代 (避免後續 OnGUI 拋出 NullReferenceException)
                this.bundlePlans = JsonConvert.DeserializeObject<List<BundlePlan>>(json) ?? new List<BundlePlan>();

                // Resave bundle plans without format
                json = JsonConvert.SerializeObject(this.bundlePlans);
                EditorStorage.SaveData(keySaver, "bundlePlans", json);

                // Reset index
                this._choicePlanIndex = 0;
                EditorStorage.SaveData(keySaver, "_choicePlanIndex", this._choicePlanIndex.ToString());
            }
        }
    }
}