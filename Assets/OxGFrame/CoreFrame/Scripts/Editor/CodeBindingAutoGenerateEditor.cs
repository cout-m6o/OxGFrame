using OxGFrame.CoreFrame.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace OxGFrame.CoreFrame.Editor
{
    public static class CodeBindingAutoGenerateEditor
    {
        private class BindInfo
        {
            public string[] attrNames;
            public string variableAccessModifier;
            public string bindName;
            public string variableName;
            public string componentName;
            public string usingNamespace;
            public CodeBindingSettings.Pluralize pluralize;
            public int count;

            public BindInfo(string[] attrNames, string variableAccessModifier, string bindName, string variableName, string componentName, int count)
            {
                this.attrNames = attrNames;
                this.variableAccessModifier = variableAccessModifier;
                this.bindName = bindName;
                this.variableName = variableName;
                this.componentName = componentName;
                this.count = count;
            }
        }

        private const string _DEF_COMPONENT_NAME = "GameObject";

        /// <summary>
        /// 成員變數定義的表達式
        /// </summary>
        private const string _SEARCH_MEMBER_PATTERN = @"(?:\[\w+\]\s*)*(?:public|protected|private|internal)?\s*(?:static\s*)?\s*\w+(?:\[\])?\s+\w+\s*;";

        /// <summary>
        /// 方法內變數賦值的表達式
        /// </summary>
        private const string _SEARCH_ASSIGNMENT_PATTERN = @"(?:this\.)?_?\w+\s*=\s*(?:this\.)?collector\.GetNode(?:s|Component(?:s)?)?(?:<\w+>)?\(""[^""]+?""\);";

        /// <summary>
        /// 綁定區塊的表達式
        /// </summary>
        private const string _REPLACEMENT_PATTERN = @"#region Binding Components(.*?)#endregion";

        /// <summary>
        /// 綁定區塊 HEAD 的表達式
        /// </summary>
        private const string _REPLACEMENT_PATTERN_HEAD = "#region Binding Components";

        /// <summary>
        /// 綁定區塊 END 的表達式
        /// </summary>
        private const string _REPLACEMENT_PATTERN_END = "#endregion";

        /// <summary>
        /// 檔案標頭單一項目的表達式 (以 \G 逐項銜接比對，遇到不屬於標頭的內容即無法繼續比對)
        /// <para>依序為：空行、行註解、區塊註解、前置處理指示詞、using 指示詞 (可作為插入點)，以及其餘寫法的 using 與 extern alias 指示詞 (僅略過)。</para>
        /// </summary>
        private const string _HEADER_ITEM_PATTERN =
            @"\G(?:" +
            @"[^\S\n]*\n" +
            @"|[^\S\n]*//[^\n]*\n" +
            @"|[^\S\n]*/\*.*?\*/" +
            @"|[^\S\n]*#[^\S\n]*(?<directive>\w*)[^\n]*\n" +
            @"|(?<using>[ \t]*using\s+[^;\r\n]+;[ \t]*\r?\n)" +
            @"|[^\S\n]*(?:global\s+using|using|extern\s+alias)\b[^;]*;[^\n]*\n" +
            @")";

        /// <summary>
        /// 屬性標記內各屬性名稱的表達式 (ex: [HideInInspector]、[Tooltip("Text"), Space]、[field: SerializeField])
        /// </summary>
        private const string _SEARCH_ATTRIBUTE_NAME_PATTERN = @"(?:\[|,)\s*(?:[A-Za-z_]\w*\s*:\s*)?(?<name>[A-Za-z_][\w.]*)";

        private static CodeBindingSettings _settings;
        private static FrameBase _script;
        private static Dictionary<string, BindInfo> _collectBindInfos = new Dictionary<string, BindInfo>();
        private static string _builder = string.Empty;
        private static readonly HashSet<string> _requiredNamespaces = new HashSet<string>();

        /// <summary>
        /// 屬性標記所需 using namespaces 的查找快取 (Key 為屬性標記的內容，腳本重載後自動清空)
        /// </summary>
        private static readonly Dictionary<string, string[]> _attrNamespaceCache = new Dictionary<string, string[]>();

        /// <summary>
        /// 前綴與腳本類型不符的節點名稱 (Runtime 僅綁定與腳本類型相符的前綴，僅用於提醒，不影響生成內容)
        /// </summary>
        private static readonly List<string> _mismatchedPrefixNodeNames = new List<string>();

        /// <summary>
        /// 是否由自動化流程調用 (不開啟剪貼簿視窗，生成內容改由回傳結果提供)
        /// </summary>
        private static bool _isAutomation = false;

        /// <summary>
        /// 本次生成是否已將綁定內容寫入腳本
        /// </summary>
        private static bool _isInserted = false;

        #region MenuItem
        [MenuItem("GameObject/OxGFrame/Auto Generate Bind Codes (Shift+B) #b", false, 0)]
        public static void Execute()
        {
            if (Selection.gameObjects.Length == 0)
                return;

            // 載入配置文件
            _settings = AssetUtility.LoadSettingsData<CodeBindingSettings>();

            // 檢查選擇物件是否包含子節點
            if (_CheckHasChildren())
                return;

            // 開始搜集綁定信息
            _StartCollect();

            // 生成代碼
            _GenerateCodes();
        }
        #endregion

        #region Automation
        /// <summary>
        /// 綁定代碼的生成結果 (供 Unity CLI 與自動化流程取得)
        /// </summary>
        public class GenerateResult
        {
            /// <summary>
            /// 是否已執行生成 (找不到 Prefab 時為 false)
            /// </summary>
            public bool generated;

            /// <summary>
            /// 是否已將綁定內容寫入腳本 (Auto 方式且腳本內具有綁定區塊時為 true)
            /// </summary>
            public bool inserted;

            /// <summary>
            /// 綁定方式 (Auto 或 Manual)
            /// </summary>
            public string methodType;

            /// <summary>
            /// 目標腳本的資源路徑 (Prefab 未掛載 UIBase、SRBase、CPBase 腳本時為 null)
            /// </summary>
            public string scriptPath;

            /// <summary>
            /// 綁定變數的數量
            /// </summary>
            public int bindCount;

            /// <summary>
            /// 生成的綁定區塊內容
            /// </summary>
            public string content;

            /// <summary>
            /// 綁定內容所需的 using namespaces
            /// </summary>
            public string[] usingNamespaces = new string[] { };

            /// <summary>
            /// 生成期間輸出至 Console 的訊息
            /// </summary>
            public string[] messages = new string[] { };
        }

        /// <summary>
        /// 以指定的 Prefab 資源生成綁定代碼 (供 Unity CLI 與自動化流程調用)
        /// <para>流程與選取 Prefab 根節點後執行 Auto Generate Bind Codes 相同，差異僅在於不開啟剪貼簿視窗，生成內容改由回傳結果提供。</para>
        /// </summary>
        /// <param name="prefabPath">Prefab 的資源路徑 (ex: Assets/Prefabs/ShopUI.prefab)</param>
        /// <returns></returns>
        public static GenerateResult GenerateByPrefabPath(string prefabPath)
        {
            var result = new GenerateResult();

            using (var logCollector = new AutomationUtility.LogCollector())
            {
                GameObject prefab = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"Cannot find the prefab asset. Prefab Path: {prefabPath}");
                    result.messages = logCollector.GetMessages();
                    return result;
                }

                // 重置上一次生成的狀態 (避免流程提前返回時取得前次的生成內容)
                _script = null;
                _builder = string.Empty;
                _collectBindInfos.Clear();
                _requiredNamespaces.Clear();
                _isInserted = false;

                // 以選取 Prefab 根節點的方式執行，生成後還原原本的選取
                UnityEngine.Object[] lastSelection = Selection.objects;
                try
                {
                    _isAutomation = true;
                    Selection.objects = new UnityEngine.Object[] { prefab };
                    Execute();

                    result.generated = !string.IsNullOrEmpty(_builder);
                    result.inserted = _isInserted;
                    result.methodType = (_settings != null) ? _settings.methodType.ToString() : null;
                    result.bindCount = _collectBindInfos.Count;
                    result.content = _builder;
                    result.usingNamespaces = _requiredNamespaces.OrderBy(ns => ns, StringComparer.Ordinal).ToArray();
                    if (_script != null)
                    {
                        string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(_script));
                        result.scriptPath = string.IsNullOrEmpty(scriptPath) ? null : scriptPath;
                    }
                }
                finally
                {
                    _isAutomation = false;
                    Selection.objects = lastSelection;
                    result.messages = logCollector.GetMessages();
                }
            }

            return result;
        }
        #endregion

        #region Check
        /// <summary>
        /// 檢查選擇物件是否包含子節點
        /// </summary>
        /// <returns></returns>
        private static bool _CheckHasChildren()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (_HasChild(go, Selection.gameObjects))
                    return true;
            }

            return false;
        }

        private static bool _HasChild(GameObject selected, GameObject[] selections)
        {
            if (selected.transform.childCount > 0)
            {
                foreach (Transform child in selected.transform)
                {
                    foreach (var select in selections)
                    {
                        if (select.GetInstanceID() == child.gameObject.GetInstanceID())
                        {
                            Debug.Log($"<color=#ff2486>Including Child Node => Parent: <color=#ffb824>{selected.name}</color>, Child: <color=#ffec24>{child.name}</color></color>");
                            return true;
                        }
                    }

                    if (_HasChild(child.gameObject, selections)) return true;
                }
            }

            return false;
        }
        #endregion

        #region Collect
        /// <summary>
        /// 開始搜集綁定信息
        /// </summary>
        private static void _StartCollect()
        {
            _script = null;
            _builder = string.Empty;
            _collectBindInfos.Clear();
            _mismatchedPrefixNodeNames.Clear();
            foreach (var go in Selection.gameObjects)
                _CheckRuleAndCollect(go);

            // 提醒前綴與腳本類型不符的節點 (ex: UIBase 底下使用 ~Node@)，這些節點於 Runtime 不會被綁定
            if (_script != null &&
                _mismatchedPrefixNodeNames.Count > 0)
            {
                string expectedPrefix = FrameConfig.BIND_PREFIXES[(_script is OxGFrame.CoreFrame.CPFrame.CPBase) ? 1 : 0];
                string expectedHead = FrameConfig.BIND_COMPONENTS.Keys.FirstOrDefault(head => head.StartsWith(expectedPrefix, StringComparison.Ordinal)) ?? expectedPrefix;
                Debug.LogWarning($"Found {_mismatchedPrefixNodeNames.Count} node(s) whose bind prefix does not match the script type ({_script.GetType().Name} only binds the nodes starting with [{expectedHead}{FrameConfig.BIND_HEAD_SEPARATOR}] at runtime): {string.Join(", ", _mismatchedPrefixNodeNames)}. The members generated for these nodes will stay null. Rename the nodes, or put the Stop End Symbol before them.", _script);
            }
        }

        /// <summary>
        /// 檢查規則並且進行綁定信息的搜集
        /// </summary>
        /// <param name="go"></param>
        /// <returns></returns>
        private static bool _CheckRuleAndCollect(GameObject go)
        {
            // Get script component
            if (_script == null)
                _script = go.GetComponent<FrameBase>();

            string name = go.name;

            // 檢查是否要結束綁定，有檢查到【BIND_END】時，則停止繼續搜尋綁定物件
            if (Binder.CheckNodeHasStopEnd(name))
                return false;

            // 這邊檢查有【BIND_PREFIX】時，則進入判斷
            if (Binder.CheckNodeHasPrefix(name))
            {
                _CollectBindInfo(name);
            }

            // 依序綁定下一個子物件 (遞迴找到符合綁定條件)
            foreach (Transform child in go.transform)
            {
                if (!_CheckRuleAndCollect(child.gameObject))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 搜集物件綁定訊息
        /// </summary>
        /// <param name="name"></param>
        private static void _CollectBindInfo(string name)
        {
            // 綁定開頭檢測
            string[] heads = Binder.GetHeadSplitNameBySeparator(name);

            // 必須先判斷字串陣列是否符合綁定格式資格，再進行取值，避免無效命名造成 IndexOutOfRangeException
            if (heads == null ||
                heads.Length < 2 ||
                !FrameConfig.BIND_COMPONENTS.ContainsKey(heads[0]))
            {
                return;
            }

            string bindType = heads[0]; // 綁定類型(會去查找 dictComponentFinder 裡面有沒有符合的類型)
            string bindInfo = heads[1]; // 要成為取得綁定物件後的 Key

            #region Common with Binder
            // 變數存取權檢測
            string[] bindArgs = Binder.GetAccessModifierSplitNameBySeparator(bindInfo);

            // 綁定名稱僅含分隔符時 (ex: _Node@$)，視為無效命名，避免 IndexOutOfRangeException 中斷整個生成流程
            if (bindArgs == null ||
                bindArgs.Length == 0)
            {
                return;
            }

            // MyObj*Txt$public => ["MyObj*Txt", "public"]
            string fullBindName = bindArgs[0];
            string variableAccessModifier = (bindArgs.Length > 1) ? bindArgs[1] : null;

            // 匹配 Attr []
            string pattern = @"\[(.*?)\]";
            string[] attrNames = new string[] { };
            if (!string.IsNullOrEmpty(variableAccessModifier))
            {
                MatchCollection attrMatches = Regex.Matches(variableAccessModifier, pattern);
                if (attrMatches.Count > 0)
                {
                    // 將匹配的子字符串存入陣列
                    attrNames = attrMatches.Cast<Match>().Select(m => m.Value).ToArray();
                    // 將所有方括號替換為空字串
                    variableAccessModifier = Regex.Replace(variableAccessModifier, pattern, "");
                }
            }
            else
            {
                MatchCollection attrMatches = Regex.Matches(fullBindName, pattern);
                if (attrMatches.Count > 0)
                {
                    // 將匹配的子字符串存入陣列
                    attrNames = attrMatches.Cast<Match>().Select(m => m.Value).ToArray();
                    // 將所有方括號替換為空字串
                    fullBindName = Regex.Replace(fullBindName, pattern, "");
                }
            }
            #endregion

            // 組件結尾檢測 => fullBindName = MyObj*Txt*Img = [MyObj, Txt, Img]
            string[] tails = Binder.GetTailSplitNameBySeparator(fullBindName);

            // 移除 Attr 後名稱為空 (ex: _Node@[hi]) 或僅含分隔符 (ex: _Node@*) 時，視為無效命名，避免異常中斷整個生成流程
            if (tails == null ||
                tails.Length == 0)
            {
                return;
            }

            // 記錄前綴與腳本類型不符的節點 (Runtime 的綁定規則為 Binder.CheckNodeHasPrefix)
            if (_script != null &&
                !Binder.CheckNodeHasPrefix(name, _script))
                _mismatchedPrefixNodeNames.Add(name);

            string variableName;
            List<string> bindNames = new List<string>();
            List<string> componentNames = new List<string>();

            if (tails != null &&
                tails.Length >= 2)
            // Component
            {
                variableName = tails[0];
                // Multi Components
                for (int i = 1; i < tails.Length; i++)
                {
                    // 合併綁定名稱 = MyObj*Txt
                    bindNames.Add($"{tails[0]}*{tails[i]}");
                    componentNames.Add(tails[i]);
                }
            }
            // GameObject
            else
            {
                variableName = tails[0];
                bindNames.Add(tails[0]);
                componentNames.Add(_DEF_COMPONENT_NAME);
            }

            for (int i = 0; i < bindNames.Count; i++)
            {
                string bindName = bindNames[i];
                string componentName = componentNames[i];
                string finalBindName = componentName.Equals(_DEF_COMPONENT_NAME) ? variableName : variableName + componentName;
                string finalVariableName = string.Empty;

                // 修正 Case
                switch (_settings.variableCaseType)
                {
                    case CodeBindingSettings.CaseType.CamelCase:
                        finalVariableName = char.ToLowerInvariant(finalBindName[0]) + finalBindName.Substring(1);
                        break;
                    case CodeBindingSettings.CaseType.PascalCase:
                        finalVariableName = char.ToUpperInvariant(finalBindName[0]) + finalBindName.Substring(1);
                        break;
                }

                // 搜集綁定資訊
                if (_collectBindInfos.ContainsKey(bindName))
                {
                    _collectBindInfos[bindName].count++;
                }
                else
                    _collectBindInfos.Add(bindName, new BindInfo(attrNames, variableAccessModifier, bindName, finalVariableName, componentName, 1));
            }
        }
        #endregion

        #region Generate
        /// <summary>
        /// 生成綁定代碼
        /// </summary>
        private static void _GenerateCodes()
        {
            #region 組件規則檢查
            _requiredNamespaces.Clear();
            foreach (var bindInfo in _collectBindInfos)
            {
                foreach (var tail in _settings.tailRules)
                {
                    if (bindInfo.Value.componentName == tail.Key)
                    {
                        bindInfo.Value.componentName = tail.Value.componentName;
                        bindInfo.Value.usingNamespace = tail.Value.usingNamespace;
                        bindInfo.Value.pluralize = tail.Value.pluralize;
                        break;
                    }
                }

                // 收集本次綁定實際用到的 using namespace (規則未填寫 usingNamespace 表示由使用者自行引用)
                if (bindInfo.Value.componentName == _DEF_COMPONENT_NAME)
                    _requiredNamespaces.Add("UnityEngine");
                else if (!string.IsNullOrEmpty(bindInfo.Value.usingNamespace))
                    _requiredNamespaces.Add(bindInfo.Value.usingNamespace);

                // 收集屬性標記所需的 using namespace (否則腳本未引用該 namespace 時，生成的屬性標記無法編譯)
                foreach (string attrName in bindInfo.Value.attrNames)
                {
                    foreach (string ns in _GetAttrNamespaces(_settings.GetAttrReference(attrName)))
                        _requiredNamespaces.Add(ns);
                }
            }
            #endregion

            // Head of content ↓↓↓
            // 生成內容一律使用相對縮排 (region 與成員為第 0 級), 待寫入腳本時再依插入點縮排自適應對齊
            string space = string.Empty;
            _builder += _REPLACEMENT_PATTERN_HEAD + "\n";

            #region 變數宣告生成
            foreach (var bindInfo in _collectBindInfos)
            {
                // Array
                if (bindInfo.Value.count > 1)
                {
                    // For GameObjects
                    if (bindInfo.Value.componentName == _DEF_COMPONENT_NAME)
                    {
                        // Attrs
                        for (int i = 0; i < bindInfo.Value.attrNames.Length; i++)
                            _builder += (i == 0 ? space : "") + $"{_settings.GetAttrReference(bindInfo.Value.attrNames[i])}";
                        // Access modifier
                        _builder += (bindInfo.Value.attrNames.Length > 0 ? " " : space) + $"{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[0]} ";
                        // Type
                        _builder += $"{bindInfo.Value.componentName}[] ";
                        // Variable name
                        _builder += $"{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[1]}{bindInfo.Value.variableName}s;\n";
                    }
                    else
                    {
                        // Attrs
                        for (int i = 0; i < bindInfo.Value.attrNames.Length; i++)
                            _builder += (i == 0 ? space : "") + $"{_settings.GetAttrReference(bindInfo.Value.attrNames[i])}";
                        // Access modifier
                        _builder += (bindInfo.Value.attrNames.Length > 0 ? " " : space) + $"{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[0]} ";
                        // Type
                        _builder += $"{bindInfo.Value.componentName}[] ";
                        // Variable name
                        int varNameLength = bindInfo.Value.variableName.Length;
                        int endRemoveCount = (bindInfo.Value.pluralize.endRemoveCount > varNameLength) ? 0 : bindInfo.Value.pluralize.endRemoveCount;
                        string varName = bindInfo.Value.variableName.Substring(0, varNameLength - endRemoveCount);
                        string endPluralTxt = bindInfo.Value.pluralize.endPluralTxt;
                        string newVarName = $"{varName}{endPluralTxt}";
                        _builder += $"{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[1]}{newVarName};\n";
                    }
                }
                // Single
                else
                {
                    // Attrs
                    for (int i = 0; i < bindInfo.Value.attrNames.Length; i++)
                        _builder += (i == 0 ? space : "") + $"{_settings.GetAttrReference(bindInfo.Value.attrNames[i])}";
                    // Access modifier
                    _builder += (bindInfo.Value.attrNames.Length > 0 ? " " : space) + $"{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[0]} ";
                    // Type
                    _builder += $"{bindInfo.Value.componentName} ";
                    // Variable name
                    _builder += $"{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[1]}{bindInfo.Value.variableName};\n";
                }
            }
            #endregion

            #region 方法定義生成
            _builder += "\n";
            _builder += space + "/// <summary>\n";
            _builder += space + "/// Auto Binding Section\n";
            _builder += space + "/// </summary>\n";
            _builder += space + $"{_settings.GetMethodAccessModifier()} override void {_settings.GetMethodName()}()\n";
            _builder += space + "{";
            _builder += "\n";
            _builder += space + $"    base.{_settings.GetMethodName()}();\n";

            foreach (var bindInfo in _collectBindInfos)
            {
                // Array
                if (bindInfo.Value.count > 1)
                {
                    // For GameObjects
                    if (bindInfo.Value.componentName == _DEF_COMPONENT_NAME)
                    {
                        // Variable
                        _builder += space + $"    {_settings.GetIndicateModifier()}{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[1]}{bindInfo.Value.variableName}s = ";
                        // Assignment
                        _builder += $"{_settings.GetIndicateModifier()}collector.GetNodes(\"{bindInfo.Value.bindName}\");\n";
                    }
                    else
                    {
                        // Variable
                        _builder += $"    ";
                        int varNameLength = bindInfo.Value.variableName.Length;
                        int endRemoveCount = (bindInfo.Value.pluralize.endRemoveCount > varNameLength) ? 0 : bindInfo.Value.pluralize.endRemoveCount;
                        string varName = bindInfo.Value.variableName.Substring(0, varNameLength - endRemoveCount);
                        string endPluralTxt = bindInfo.Value.pluralize.endPluralTxt;
                        string newVarName = $"{varName}{endPluralTxt}";
                        _builder += space + $"{_settings.GetIndicateModifier()}{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[1]}{newVarName} = ";
                        // Assignment
                        _builder += $"{_settings.GetIndicateModifier()}collector.GetNodeComponents<{bindInfo.Value.componentName}>(\"{bindInfo.Value.bindName}\");\n";
                    }
                }
                // Single
                else
                {
                    // For GameObject
                    if (bindInfo.Value.componentName == _DEF_COMPONENT_NAME)
                    {
                        // Variable
                        _builder += space + $"    {_settings.GetIndicateModifier()}{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[1]}{bindInfo.Value.variableName} = ";
                        // Assignment
                        _builder += $"{_settings.GetIndicateModifier()}collector.GetNode(\"{bindInfo.Value.bindName}\");\n";
                    }
                    else
                    {
                        // Variable
                        _builder += space + $"    {_settings.GetIndicateModifier()}{_settings.GetVariableAccessPairs(bindInfo.Value.variableAccessModifier)[1]}{bindInfo.Value.variableName} = ";
                        // Assignment
                        _builder += $"{_settings.GetIndicateModifier()}collector.GetNodeComponent<{bindInfo.Value.componentName}>(\"{bindInfo.Value.bindName}\");\n";
                    }
                }
            }
            _builder += space + "}";
            #endregion

            _builder += "\n";
            _builder += space + _REPLACEMENT_PATTERN_END;
            // End of content ↑↑↑

            // Compare with parent class
            var scriptPaths = GetInheritedScriptPaths(_script, false);
            for (int i = 0; i < scriptPaths.Length; i++)
            {
                // 取得目標腳本的綁定區塊
                string targetCode = _GetBindingSectionText(scriptPaths[i]);

                // 沒找到綁定區塊，直接略過
                if (string.IsNullOrEmpty(targetCode))
                    continue;

                Debug.Log($"<color=#39ffc2>Try comparing with parent class. ScriptPath: {scriptPaths[i]}</color>");
                _builder = _RemoveDuplicateLines(_builder, targetCode);
            }

            // > 1 表示有進行父類比對，才需要調整格式
            if (scriptPaths.Length > 1)
                _builder = _AdjustMembersFormat(_builder);

            switch (_settings.methodType)
            {
                case CodeBindingSettings.MethodType.Auto:
                    // Save to script
                    _InsertCodes(_builder);
                    break;
                default:
                    // Show on clipboard
                    _ShowClipboard(_builder);
                    break;
            }
        }

        /// <summary>
        /// 取得屬性標記所需的 using namespaces
        /// <para>僅處理未指定 namespace 的屬性名稱，且僅於查找結果唯一時採用 (查無型別或存在多個同名型別時，維持由使用者自行引用)。</para>
        /// </summary>
        /// <param name="attrReference">屬性標記的內容 (ex: [HideInInspector])</param>
        /// <returns></returns>
        private static string[] _GetAttrNamespaces(string attrReference)
        {
            if (string.IsNullOrEmpty(attrReference))
                return new string[] { };

            if (_attrNamespaceCache.TryGetValue(attrReference, out string[] namespaces))
                return namespaces;

            var results = new List<string>();
            foreach (Match match in Regex.Matches(attrReference, _SEARCH_ATTRIBUTE_NAME_PATTERN))
            {
                string attrName = match.Groups["name"].Value;

                // 已指定 namespace 的屬性不需要 using
                if (attrName.Contains("."))
                    continue;

                var candidates = UsingNamespaceFinder.FindAttributeCandidates(attrName);
                if (candidates.Count == 1 &&
                    !string.IsNullOrEmpty(candidates[0].ns) &&
                    !results.Contains(candidates[0].ns))
                    results.Add(candidates[0].ns);
            }

            namespaces = results.ToArray();
            _attrNamespaceCache[attrReference] = namespaces;
            return namespaces;
        }

        /// <summary>
        /// 顯示剪貼簿
        /// </summary>
        /// <param name="content"></param>
        private static void _ShowClipboard(string content)
        {
            // 由自動化流程調用時不開啟剪貼簿視窗 (生成內容改由回傳結果提供)
            if (_isAutomation)
                return;

            // 附上所需的 using namespaces 提示 (由使用者自行貼至腳本頂部)
            if (_requiredNamespaces.Count > 0)
            {
                string usingText = "// Please paste the following usings at the top of the script file.\n";
                foreach (string ns in _requiredNamespaces.OrderBy(ns => ns, StringComparer.Ordinal))
                    usingText += $"using {ns};\n";
                content = usingText + "\n" + content;
            }

            CodeBindingClipboardWindow.ShowWindow(content);
            Debug.Log("<color=#02ff8e>[Manual] Copy binding content to script!!!</color>");
        }

        /// <summary>
        /// 儲存綁定區塊至腳本中
        /// </summary>
        /// <param name="content"></param>
        private static void _InsertCodes(string content)
        {
            // Get script path
            string scriptPath = _GetScriptPath(_script);

            if (string.IsNullOrEmpty(scriptPath))
                return;

            // 非 UTF-8 編碼的腳本 (例如 Big5、GBK、Shift-JIS) 無法被正確解碼，寫回時會永久損毀非 ASCII 字元 (ex: 中文註解)
            // 因此不進行寫入，改以手動方式提供綁定內容，並提示使用者先轉換腳本編碼
            if (!_IsDecodableScript(scriptPath))
            {
                Debug.LogError($"Unable to insert binding content automatically, because the script is not encoded in UTF-8 (writing it back would corrupt non-ASCII characters). Please convert the script to UTF-8 first. Script Path: {scriptPath}", _script);
                _ShowClipboard(content);
                return;
            }

            // 偵測原始編碼是否含 BOM (寫回時保持一致)
            bool hasBom = false;
            try
            {
                using (var fs = System.IO.File.OpenRead(scriptPath))
                {
                    byte[] preamble = new byte[3];
                    if (fs.Read(preamble, 0, 3) == 3)
                        hasBom = preamble[0] == 0xEF && preamble[1] == 0xBB && preamble[2] == 0xBF;
                }
            }
            catch
            {
                /* Nothing to do */
            }

            // Read all script content
            string scriptContent = System.IO.File.ReadAllText(scriptPath);

            // 偵測原始內容的換行符 (寫回時保持一致)
            string lineEnding = _DetectLineEnding(scriptContent);

            // Expression pattern
            string pattern = _REPLACEMENT_PATTERN;
            Regex regex = new Regex(pattern, RegexOptions.Singleline);
            Match match = regex.Match(scriptContent);

            if (match.Success)
            {
                // 量測插入點所在行的縮排，讓生成內容自適應 namespace 與巢狀層級
                string baseIndent = _MeasureBaseIndent(scriptContent, match.Groups[0].Index);

                // 將相對縮排的生成內容依插入點縮排逐行對齊
                string indentedContent = _ApplyBaseIndent(content, baseIndent);

                // Replace binding content into the script
                scriptContent = scriptContent.Remove(match.Groups[0].Index, match.Groups[0].Length);
                scriptContent = scriptContent.Insert(match.Groups[0].Index, indentedContent);

                // 插入缺少的 using namespaces
                scriptContent = _InsertUsingNamespaces(scriptContent, _requiredNamespaces);

                // To Unix style "\n"
                scriptContent = scriptContent.Replace("\r\n", "\n").Replace("\r", "\n");

                // 還原為原始檔案的換行符 (生成內容一律使用 "\n"，先統一後再轉換可避免寫回後換行符混用)
                if (lineEnding != "\n")
                    scriptContent = scriptContent.Replace("\n", lineEnding);

                // Write file
                System.IO.File.WriteAllText(scriptPath, scriptContent, new System.Text.UTF8Encoding(hasBom));
                _isInserted = true;

                AssetDatabase.Refresh();
                EditorUtility.SetDirty(_script);

                Debug.Log("<color=#02ff8e>[Auto] Completed automatically binding content to script!!!</color>", _script);
            }
            else
            {
                Debug.LogWarning($"Unable to find specific replacement string in script. Script Name:{_script.name}", _script);
                Debug.LogWarning($"The pattern is ↓↓↓ (Copy the following into the script) ↓↓↓", _script);
                Debug.LogWarning($"{_REPLACEMENT_PATTERN_HEAD}\n{_REPLACEMENT_PATTERN_END}");
            }
        }

        /// <summary>
        /// 檢查腳本是否可被正確解碼 (帶有 UTF-16 或 UTF-32 的 BOM，或者內容為合法的 UTF-8)
        /// </summary>
        /// <param name="scriptPath"></param>
        /// <returns></returns>
        private static bool _IsDecodableScript(string scriptPath)
        {
            byte[] bytes;
            try
            {
                bytes = System.IO.File.ReadAllBytes(scriptPath);
            }
            catch
            {
                // 無法讀取時交由後續流程處理 (維持原行為)
                return true;
            }

            // 帶有 UTF-16 或 UTF-32 的 BOM 時，File.ReadAllText 會依 BOM 正確解碼
            if (bytes.Length >= 2 &&
                ((bytes[0] == 0xFF && bytes[1] == 0xFE) ||
                 (bytes[0] == 0xFE && bytes[1] == 0xFF)))
                return true;
            if (bytes.Length >= 4 &&
                bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                return true;

            // 略過 UTF-8 BOM
            int offset = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;

            try
            {
                // 以嚴格模式解碼 (遇到無效位元組時拋出例外，而非以取代字元 U+FFFD 代替)
                new System.Text.UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
                return true;
            }
            catch (System.Text.DecoderFallbackException)
            {
                return false;
            }
        }

        /// <summary>
        /// 偵測內容所使用的換行符 (CRLF 多於 LF 時回傳 "\r\n"，其餘回傳 "\n")
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private static string _DetectLineEnding(string content)
        {
            int crlfCount = 0;
            int lfCount = 0;
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] != '\n')
                    continue;

                if (i > 0 && content[i - 1] == '\r')
                    crlfCount++;
                else
                    lfCount++;
            }

            return (crlfCount > lfCount) ? "\r\n" : "\n";
        }

        /// <summary>
        /// 量測插入點所在行的縮排字串 (取行首至插入點之間的空白)
        /// </summary>
        /// <param name="scriptContent"></param>
        /// <param name="insertIndex"></param>
        /// <returns></returns>
        private static string _MeasureBaseIndent(string scriptContent, int insertIndex)
        {
            int lineStart = scriptContent.LastIndexOf('\n', Math.Max(0, insertIndex - 1)) + 1;
            string leading = scriptContent.Substring(lineStart, insertIndex - lineStart);

            // 僅接受純空白的前綴，避免同行前方有其他代碼造成錯誤縮排
            foreach (char c in leading)
            {
                if (c != ' ' && c != '\t')
                    return string.Empty;
            }

            return leading;
        }

        /// <summary>
        /// 將相對縮排的生成內容逐行加上基準縮排 (首行由原插入點自帶縮排，空行不做處理)
        /// </summary>
        /// <param name="content"></param>
        /// <param name="baseIndent"></param>
        /// <returns></returns>
        private static string _ApplyBaseIndent(string content, string baseIndent)
        {
            if (string.IsNullOrEmpty(baseIndent))
                return content;

            string[] lines = content.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length > 0)
                    lines[i] = baseIndent + lines[i];
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// 將缺少的 using namespaces 插入至腳本頂部的 using 區塊 (已存在的不重複插入)
        /// </summary>
        /// <param name="scriptContent"></param>
        /// <param name="requiredNamespaces"></param>
        /// <returns></returns>
        private static string _InsertUsingNamespaces(string scriptContent, HashSet<string> requiredNamespaces)
        {
            if (requiredNamespaces == null ||
                requiredNamespaces.Count == 0)
                return scriptContent;

            // 收集腳本中已存在的 using (寬鬆掃描全檔，排除 alias 與 static 形式)
            HashSet<string> existings = new HashSet<string>();
            foreach (Match m in Regex.Matches(scriptContent, @"^[ \t]*using\s+(?!static\s)([\w.]+)\s*;", RegexOptions.Multiline))
                existings.Add(m.Groups[1].Value);

            // 過濾出缺少的 using 並依字母排序 (讓插入結果穩定)
            List<string> missings = requiredNamespaces.Where(ns => !existings.Contains(ns)).OrderBy(ns => ns, StringComparer.Ordinal).ToList();
            if (missings.Count == 0)
                return scriptContent;

            // 尋找檔案標頭最後一個 using 行的結尾作為插入點 (僅掃描標頭，不會誤判方法內的 using 陳述式)
            int insertIndex = _FindUsingInsertIndex(scriptContent);

            string insertText = string.Empty;
            foreach (string ns in missings)
                insertText += $"using {ns};\n";

            return scriptContent.Insert(insertIndex, insertText);
        }

        /// <summary>
        /// 尋找缺少的 using 應插入的位置 (僅掃描檔案標頭，回傳最後一個 using 行的結尾)
        /// <para>已結束的 #if 區塊內的 using 不作為插入點，否則會被插入至 #if 內；標頭無可用的 using 行時，回傳 #define 與 #undef 之後的位置 (using 位於其前方會造成 CS1032)。</para>
        /// </summary>
        /// <param name="scriptContent"></param>
        /// <returns></returns>
        private static int _FindUsingInsertIndex(string scriptContent)
        {
            // 各層條件編譯區塊內的插入點 (索引 0 為區塊外，-1 表示該層尚無插入點)
            List<int> insertIndexes = new List<int>() { -1 };

            // 最近一次 #define 或 #undef 所在的層級 (-1 表示尚未出現)
            int defineDepth = -1;

            // 逐項比對標頭內容，遇到第一個不屬於標頭的內容 (namespace、型別宣告、Attribute 等) 即停止
            for (Match item = Regex.Match(scriptContent, _HEADER_ITEM_PATTERN, RegexOptions.Singleline); item.Success; item = item.NextMatch())
            {
                int depth = insertIndexes.Count - 1;
                int itemEnd = item.Index + item.Length;

                if (item.Groups["using"].Success)
                {
                    insertIndexes[depth] = itemEnd;
                    continue;
                }

                switch (item.Groups["directive"].Value)
                {
                    case "define":
                    case "undef":
                        insertIndexes[depth] = itemEnd;
                        defineDepth = depth;
                        break;

                    case "if":
                        insertIndexes.Add(-1);
                        break;

                    case "elif":
                    case "else":
                        // 切換分支後，先前分支內的 using 不再適用
                        if (depth > 0)
                            insertIndexes[depth] = (defineDepth == depth) ? itemEnd : -1;
                        break;

                    case "endif":
                        if (depth > 0)
                        {
                            insertIndexes.RemoveAt(depth);

                            // 區塊內含有 #define 或 #undef 時，插入點必須位於該區塊之後
                            if (defineDepth == depth)
                            {
                                insertIndexes[depth - 1] = itemEnd;
                                defineDepth = depth - 1;
                            }
                        }
                        break;
                }
            }

            // 由內而外，取仍在作用範圍內的最後一個插入點
            for (int i = insertIndexes.Count - 1; i >= 0; i--)
            {
                if (insertIndexes[i] >= 0)
                    return insertIndexes[i];
            }

            return 0;
        }

        /// <summary>
        /// 取得腳本的路徑
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        private static string _GetScriptPath(MonoBehaviour script)
        {
            if (script == null)
            {
                Debug.LogWarning("Cannot find script (FrameBase => UIBase, SRBase, CPBase). Please drag the script onto the object!!!");
                return null;
            }

            MonoScript monoScript = MonoScript.FromMonoBehaviour(script);
            string scriptRelativePath = AssetDatabase.GetAssetPath(monoScript);
            string scriptPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", scriptRelativePath));
            return scriptPath;
        }
        #endregion

        /// <summary>
        /// 取得腳本繼承基類的路徑
        /// </summary>
        /// <param name="script">腳本自身</param>
        /// <param name="includeSelf">是否包含腳本自身</param>
        /// <returns></returns>
        public static string[] GetInheritedScriptPaths(MonoBehaviour script, bool includeSelf)
        {
            List<string> scriptPaths = new List<string>();

            if (script == null)
                return scriptPaths.ToArray();

            Type currentType = script.GetType();
            while (currentType != null && currentType != typeof(MonoBehaviour))
            {
                // 跳過抽象類型
                if (!currentType.IsAbstract)
                {
                    // 嘗試取得 MonoScript 路徑
                    MonoScript monoScript = _GetMonoScriptFromType(currentType);

                    if (monoScript != null)
                    {
                        string scriptRelativePath = AssetDatabase.GetAssetPath(monoScript);
                        if (!string.IsNullOrEmpty(scriptRelativePath))
                        {
                            string scriptPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", scriptRelativePath));
                            scriptPaths.Add(scriptPath);
                        }
                    }
                }

                currentType = currentType.BaseType;
            }

            // 讓順序從基類到派生類
            // Reverse will be -> 0 = parent_a, 1 = parent_b, 2 = self
            scriptPaths.Reverse();

            // 移除最後一個元素 (就是腳本自身)
            if (!includeSelf)
                scriptPaths.RemoveAt(scriptPaths.Count - 1);

            return scriptPaths.ToArray();
        }

        /// <summary>
        /// 獲取 MonoScript
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static MonoScript _GetMonoScriptFromType(Type type)
        {
            // 確保傳入的類型是 MonoBehaviour 的子類型
            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                return null;

            // 嘗試通過反射訪問類型並獲取 MonoScript
            GameObject dummyGo = new GameObject("Dummy");
            try
            {
                MonoBehaviour dummyInstance = dummyGo.AddComponent(type) as MonoBehaviour;
                if (dummyInstance != null)
                    return MonoScript.FromMonoBehaviour(dummyInstance);

                return null;
            }
            finally
            {
                // 清理臨時對象 (AddComponent 失敗或過程中拋出例外時也必須銷毀，避免殘留於場景中)
                if (dummyGo != null)
                    UnityEngine.Object.DestroyImmediate(dummyGo);
            }
        }

        /// <summary>
        /// 取得腳本中綁定區塊
        /// </summary>
        /// <param name="scriptPath"></param>
        /// <returns></returns>
        private static string _GetBindingSectionText(string scriptPath)
        {
            // 假設有一個方法來解析每個類的 Binding Components 部分
            string scriptContent = System.IO.File.ReadAllText(scriptPath);

            var match = Regex.Match(scriptContent, @"#region Binding Components(.*?)#endregion", RegexOptions.Singleline);
            if (match.Success)
            {
                string bindingSection = match.Groups[1].Value;
                return bindingSection;
            }

            return string.Empty;
        }

        /// <summary>
        /// 比對兩段程式碼，移除來源中與目標重複的行
        /// </summary>
        /// <param name="sourceSection"></param>
        /// <param name="targetSection"></param>
        /// <returns></returns>
        private static string _RemoveDuplicateLines(string sourceSection, string targetSection)
        {
            // 成員變數定義的表達式
            Regex memberRegex = new Regex(_SEARCH_MEMBER_PATTERN);

            // 方法內變數賦值的表達式
            Regex assignmentRegex = new Regex(_SEARCH_ASSIGNMENT_PATTERN);

            // 儲存目標中的變數名稱
            HashSet<string> targetMembers = new HashSet<string>();
            HashSet<string> targetAssignments = new HashSet<string>();

            // 提取目標內容的變數名稱
            foreach (Match match in memberRegex.Matches(targetSection))
            {
                // 儲存完整變數定義行
                targetMembers.Add(match.Value.Trim());
            }
            foreach (Match match in assignmentRegex.Matches(targetSection))
            {
                // 儲存完整賦值行
                targetAssignments.Add(match.Value.Trim());
            }

            // 移除成員變數
            foreach (var v in targetMembers)
            {
                sourceSection = _RemoveLineContaining(sourceSection, v, false);
                Debug.Log($"<color=#ff7454>Removed the intersecting part of variables: <color=#ffb854>{v}</color></color>");
            }

            // 移除賦值
            foreach (var v in targetAssignments)
            {
                sourceSection = _RemoveLineContaining(sourceSection, v, false);
                Debug.Log($"<color=#ff7454>Removed the intersecting part of assignments: <color=#ffb854>{v}</color></color>");
            }

            return sourceSection;
        }

        /// <summary>
        /// 移除目標時，並且移除不必要斷行
        /// </summary>
        /// <param name="input"></param>
        /// <param name="target"></param>
        /// <param name="preserveNewline"></param>
        /// <returns></returns>
        private static string _RemoveLineContaining(string input, string target, bool preserveNewline)
        {
            Regex regex = new Regex($@"^\s*{Regex.Escape(target)}\s*\r?\n", RegexOptions.Multiline);
            return regex.Replace(input, preserveNewline ? "\n" : "");
        }

        /// <summary>
        /// 調整與父類比對處理後的成員變數格式
        /// </summary>
        /// <param name="sourceSection"></param>
        /// <returns></returns>
        private static string _AdjustMembersFormat(string sourceSection)
        {
            // 成員變數定義的表達式
            Regex memberRegex = new Regex(_SEARCH_MEMBER_PATTERN);

            // 儲存來源中的變數名稱
            HashSet<string> sourceMembers = new HashSet<string>();

            // 提取來源內容的變數名稱
            foreach (Match match in memberRegex.Matches(sourceSection))
            {
                // 儲存完整變數定義行
                sourceMembers.Add(match.Value.Trim());
            }

            // 找到最後一個成員變數進行換行符添加
            int count = 0;
            foreach (var v in sourceMembers)
            {
                if (count == sourceMembers.Count - 1)
                {
                    // 檢查最後一個元素後是否有兩個換行，如果沒有需要再 + 一個
                    if (sourceSection.IndexOf(v + "\n\n") == -1)
                    {
                        sourceSection = sourceSection.Replace(v, v + "\n");
                    }
                }
                count++;
            }

            return sourceSection;
        }
    }
}
