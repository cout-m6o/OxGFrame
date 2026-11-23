using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OxGFrame.AssetLoader.Bundle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OxGFrame.AssetLoader.Editor
{
    public class BundleUrlConfigGeneratorWindow : EditorWindow
    {
        private static BundleUrlConfigGeneratorWindow _instance = null;
        internal static BundleUrlConfigGeneratorWindow GetInstance()
        {
            if (_instance == null)
                _instance = GetWindow<BundleUrlConfigGeneratorWindow>();
            return _instance;
        }

        [SerializeField]
        public string bundleIp;
        [SerializeField]
        public string bundleFallbackIp;
        [SerializeField]
        public string storeLink;
        [SerializeField]
        public bool autoReveal;

        // Preset Plan
        public List<BundleUrlPlan> bundleUrlPlans = new List<BundleUrlPlan>();
        private int _choicePlanIndex = 0;

        private Vector2 _scrollview;

        private SerializedObject _serObj;
        private SerializedProperty _bundleUrlPlansPty;

        /// <summary>
        /// 背景樣式緩存 (Key 為色碼，所建立的貼圖於 OnDisable 時銷毀)
        /// </summary>
        private readonly Dictionary<string, GUIStyle> _backgroundStyles = new Dictionary<string, GUIStyle>();

        internal static string projectPath;
        internal static string keySaver;

        private static Vector2 _windowSize = new Vector2(800f, 150f);

        [MenuItem(BundleHelper.MENU_ROOT + "Bundle Url Config Generator (CdnConfig.dat)", false, 899)]
        public static void ShowWindow()
        {
            projectPath = Application.dataPath;
            keySaver = $"{projectPath}_{nameof(BundleUrlConfigGeneratorWindow)}";

            _instance = null;
            GetInstance().titleContent = new GUIContent("Bundle Url Config Generator");
            GetInstance().Show();
            GetInstance().minSize = _windowSize;
        }

        private void OnEnable()
        {
            // Domain Reload (重新編譯、進入 Play Mode) 或 Layout 還原後，靜態欄位會被清空，需補回儲存鍵值 (與 ShowWindow 相同)
            if (string.IsNullOrEmpty(keySaver))
            {
                projectPath = Application.dataPath;
                keySaver = $"{projectPath}_{nameof(BundleUrlConfigGeneratorWindow)}";
            }

            this._serObj = new SerializedObject(this);
            this._bundleUrlPlansPty = this._serObj.FindProperty("bundleUrlPlans");

            this.bundleIp = EditorStorage.GetData(keySaver, "bundleIp", "http://127.0.0.1");
            this.bundleFallbackIp = EditorStorage.GetData(keySaver, "bundleFallbackIp", "http://127.0.0.1");
            this.storeLink = EditorStorage.GetData(keySaver, "storeLink", "http://");

            this.autoReveal = Convert.ToBoolean(EditorStorage.GetData(keySaver, "autoReveal", "true"));

            // Preset Bundle Url Plans
            string jsonBundleUrlPlans = EditorStorage.GetData(keySaver, "bundleUrlPlans", string.Empty);
            // 內容為 null 的 Json 會反序列化為 null，需以空列表取代 (避免後續 OnGUI 拋出 NullReferenceException)
            if (!string.IsNullOrEmpty(jsonBundleUrlPlans)) this.bundleUrlPlans = JsonConvert.DeserializeObject<List<BundleUrlPlan>>(jsonBundleUrlPlans) ?? new List<BundleUrlPlan>();
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
            this._DrawExportBundleUrlConfigToStreamingAssetsView();
            this._DrawBundleUrlPlansView();
        }

        private void _DrawExportBundleUrlConfigToStreamingAssetsView()
        {
            EditorGUILayout.Space();

            GUIStyle style = this._GetBackgroundStyle("#1e3836");
            EditorGUILayout.BeginVertical(style);
            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent("Export BundleUrlConfig To StreamingAssets"), centeredStyle);
            EditorGUILayout.Space();

            // draw here
            this._DrawBundleIPTextFieldView();
            this._DrawBundleFallbackIPTextFieldView();
            this._DrawStoreLinkTextFieldView();
            this._DrawProcessButtonView();

            EditorGUILayout.EndVertical();
        }

        private void _DrawBundleIPTextFieldView()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 180;
            this.bundleIp = EditorGUILayout.TextField("Bundle IP or Domain", this.bundleIp);
            EditorGUIUtility.labelWidth = labelWidth;
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "bundleIp", this.bundleIp);
            EditorGUILayout.EndHorizontal();
        }

        private void _DrawBundleFallbackIPTextFieldView()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 180;
            this.bundleFallbackIp = EditorGUILayout.TextField("Bundle Fallback IP or Domain", this.bundleFallbackIp);
            EditorGUIUtility.labelWidth = labelWidth;
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "bundleFallbackIp", this.bundleFallbackIp);
            EditorGUILayout.EndHorizontal();
        }

        private void _DrawStoreLinkTextFieldView()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 180;
            this.storeLink = EditorGUILayout.TextField("Store Link", this.storeLink);
            EditorGUIUtility.labelWidth = labelWidth;
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "storeLink", this.storeLink);
            EditorGUILayout.EndHorizontal();
        }

        private void _DrawProcessButtonView()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            // auto reveal toggle
            EditorGUI.BeginChangeCheck();
            this.autoReveal = GUILayout.Toggle(this.autoReveal, new GUIContent("Auto Reveal", "If checked, after process will reveal destination folder."));
            if (EditorGUI.EndChangeCheck()) EditorStorage.SaveData(keySaver, "autoReveal", this.autoReveal.ToString());

            // process button
            Color bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 185, 83, 255);
            if (GUILayout.Button("Cipher Process", GUILayout.MaxWidth(110f)))
            {
                string outputPath = Application.streamingAssetsPath;
                BundleHelper.ExportBundleUrlConfig(this.bundleIp, this.bundleFallbackIp, this.storeLink, outputPath, true);
                EditorUtility.DisplayDialog("Process Message", "Export [Cipher] BundleUrlConfig To StreamingAssets.", "OK");
                AssetDatabase.Refresh();
                string bundleUrlFileName = $"{PatchSettings.settings.bundleUrlCfgName}{PatchSettings.settings.bundleUrlCfgExtension}";
                if (this.autoReveal) EditorUtility.RevealInFinder($"{outputPath}/{bundleUrlFileName}");
            }
            GUI.backgroundColor = bc;
            GUI.backgroundColor = new Color32(255, 185, 83, 255);
            if (GUILayout.Button("Plaintext Process", GUILayout.MaxWidth(125f)))
            {
                string outputPath = Application.streamingAssetsPath;
                BundleHelper.ExportBundleUrlConfig(this.bundleIp, this.bundleFallbackIp, this.storeLink, outputPath, false);
                EditorUtility.DisplayDialog("Process Message", "Export [Plaintext] BundleUrlConfig To StreamingAssets.", "OK");
                AssetDatabase.Refresh();
                string bundleUrlFileName = $"{PatchSettings.settings.bundleUrlCfgName}{PatchSettings.settings.bundleUrlCfgExtension}";
                if (this.autoReveal) EditorUtility.RevealInFinder($"{outputPath}/{bundleUrlFileName}");
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();
        }

        #region Preset Bundle Plans
        private void _DrawBundleUrlPlansView()
        {
            this._scrollview = EditorGUILayout.BeginScrollView(this._scrollview, true, true);

            EditorGUILayout.Space();

            GUIStyle style = this._GetBackgroundStyle("#263840");
            EditorGUILayout.BeginVertical(style);

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label(new GUIContent("Preset Bundle Url Plans"), centeredStyle);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            // Add popup selection
            List<string> planNames = new List<string>();
            if (this.bundleUrlPlans.Count > 0)
            {
                foreach (var bundleUrlPlan in this.bundleUrlPlans)
                {
                    planNames.Add(bundleUrlPlan.planName);
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
                    $"The plan selection is [{this.bundleUrlPlans[this._choicePlanIndex].planName}]\nDo you want to copy current all values?",
                    "copy current and override",
                    "cancel"
                );

                if (confirmation) this._CopyCurrentToBundleUrlPlan();
            }
            GUI.backgroundColor = bc;
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(0, 255, 128, 255);
            if (GUILayout.Button("Load Plan", GUILayout.MaxWidth(100f)))
            {
                this._LoadBundleUrlPlanToCurrent();
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            this._serObj.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(this._bundleUrlPlansPty, true);
            if (EditorGUI.EndChangeCheck())
            {
                this._serObj.ApplyModifiedProperties();
                string json = JsonConvert.SerializeObject(this.bundleUrlPlans);
                EditorStorage.SaveData(keySaver, "bundleUrlPlans", json);
            }

            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(255, 151, 240, 255);
            if (GUILayout.Button("Reset", GUILayout.MaxWidth(100f)))
            {
                bool confirmation = EditorUtility.DisplayDialog
                (
                    $"Reset Bundle Url Plans Notification",
                    $"Do you want to reset bundle url plans?",
                    "reset",
                    "cancel"
                );

                if (confirmation)
                {
                    // Reset bundle plans
                    this.bundleUrlPlans = new List<BundleUrlPlan>() { new BundleUrlPlan() };
                    string json = JsonConvert.SerializeObject(this.bundleUrlPlans);
                    EditorStorage.SaveData(keySaver, "bundleUrlPlans", json);

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
                this._ExportBundleUrlPlans();
            }
            GUI.backgroundColor = bc;
            // Load File
            bc = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(0, 249, 255, 255);
            if (GUILayout.Button("Load File", GUILayout.MaxWidth(100f)))
            {
                this._ImportBundleUrlPlans();
            }
            GUI.backgroundColor = bc;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 將 Plan 索引修正至有效範圍內 (Plans 被移除後，索引可能殘留於範圍外)，無任何 Plan 時返回 false
        /// </summary>
        /// <returns></returns>
        private bool _ClampChoicePlanIndex()
        {
            if (this.bundleUrlPlans.Count == 0) return false;

            int choicePlanIndex = Mathf.Clamp(this._choicePlanIndex, 0, this.bundleUrlPlans.Count - 1);
            if (choicePlanIndex != this._choicePlanIndex)
            {
                this._choicePlanIndex = choicePlanIndex;
                EditorStorage.SaveData(keySaver, "_choicePlanIndex", this._choicePlanIndex.ToString());
            }

            return true;
        }

        private void _LoadBundleUrlPlanToCurrent()
        {
            if (!this._ClampChoicePlanIndex()) return;

            var bundleUrlPlan = this.bundleUrlPlans[this._choicePlanIndex];

            this.bundleIp = bundleUrlPlan.bundleIp;
            this.bundleFallbackIp = bundleUrlPlan.bundleFallbackIp;
            this.storeLink = bundleUrlPlan.storeLink;

            // Save
            EditorStorage.SaveData(keySaver, "bundleIp", this.bundleIp);
            EditorStorage.SaveData(keySaver, "bundleFallbackIp", this.bundleFallbackIp);
            EditorStorage.SaveData(keySaver, "storeLink", this.storeLink);
        }

        private void _CopyCurrentToBundleUrlPlan()
        {
            if (!this._ClampChoicePlanIndex()) return;

            var bundleUrlPlan = this.bundleUrlPlans[this._choicePlanIndex];

            // Copy
            bundleUrlPlan.bundleIp = this.bundleIp;
            bundleUrlPlan.bundleFallbackIp = this.bundleFallbackIp;
            bundleUrlPlan.storeLink = this.storeLink;

            this.bundleUrlPlans[this._choicePlanIndex] = bundleUrlPlan;

            // Save
            string json = JsonConvert.SerializeObject(this.bundleUrlPlans);
            EditorStorage.SaveData(keySaver, "bundleUrlPlans", json);
        }

        private void _ExportBundleUrlPlans()
        {
            string savePath = EditorStorage.GetData(keySaver, $"bundleUrlPlanFilePath", Application.dataPath);
            var filePath = EditorUtility.SaveFilePanel("Save Bundle Url Plan Json File", savePath, "BundleUrlPlan", "json");

            if (!string.IsNullOrEmpty(filePath))
            {
                EditorStorage.SaveData(keySaver, $"bundleUrlPlanFilePath", Path.GetDirectoryName(filePath));
                string json = JsonConvert.SerializeObject(this.bundleUrlPlans, Formatting.Indented);
                BundleHelper.WriteTxt(json, filePath);
                AssetDatabase.Refresh();
            }
        }

        private void _ImportBundleUrlPlans()
        {
            string loadPath = EditorStorage.GetData(keySaver, $"bundleUrlPlanFilePath", Application.dataPath);
            string filePath = EditorUtility.OpenFilePanel("Select Bundle Url Plan Json File", !string.IsNullOrEmpty(loadPath) ? loadPath : Application.dataPath, "json");

            if (!string.IsNullOrEmpty(filePath))
            {
                EditorStorage.SaveData(keySaver, $"bundleUrlPlanFilePath", Path.GetDirectoryName(filePath));
                string json = File.ReadAllText(filePath);
                // 空檔案或內容為 null 的 Json 會反序列化為 null，需以空列表取代 (避免後續 OnGUI 拋出 NullReferenceException)
                this.bundleUrlPlans = JsonConvert.DeserializeObject<List<BundleUrlPlan>>(json) ?? new List<BundleUrlPlan>();

                // Resave bundle plans without format
                json = JsonConvert.SerializeObject(this.bundleUrlPlans);
                EditorStorage.SaveData(keySaver, "bundleUrlPlans", json);

                // Reset index
                this._choicePlanIndex = 0;
                EditorStorage.SaveData(keySaver, "_choicePlanIndex", this._choicePlanIndex.ToString());
            }
        }
        #endregion
    }
}