using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OxGFrame.CoreFrame.Editor
{
    public static class StopEndAutoGenerateEditor
    {
        private static List<GameObject> _listGos = new List<GameObject>();

        #region MenuItem
        [MenuItem("GameObject/OxGFrame/Auto Generate Stop End Symbol (Shift+E) #e", false, 0)]
        public static void Execute()
        {
            if (Selection.gameObjects.Length == 0) return;

            // 檢查選擇物件是不是 Root 節點
            if (!_CheckIsRoot()) return;

            // 遍歷選擇的各 root 節點
            foreach (var root in Selection.gameObjects)
            {
                // 開始向下搜索
                _StartSearchDown(root);
                // 生成 StopEnd
                _GenerateStopEndSymbol();
            }
        }
        #endregion

        #region Automation
        /// <summary>
        /// StopEnd 符號的生成結果 (供 Unity CLI 與自動化流程取得)
        /// </summary>
        public class GenerateResult
        {
            /// <summary>
            /// 是否已執行生成 (找不到 Prefab 或無法開啟 Prefab 內容時為 false)
            /// </summary>
            public bool generated;

            /// <summary>
            /// Prefab 內的節點名稱是否有變更 (有變更時才會儲存 Prefab)
            /// </summary>
            public bool changed;

            /// <summary>
            /// 生成後帶有 StopEnd 符號的節點路徑
            /// </summary>
            public string[] stopEndNodes = new string[] { };

            /// <summary>
            /// 生成期間輸出至 Console 的訊息
            /// </summary>
            public string[] messages = new string[] { };
        }

        /// <summary>
        /// 以指定的 Prefab 資源生成 StopEnd 符號並儲存 (供 Unity CLI 與自動化流程調用)
        /// <para>流程與選取 Prefab 根節點後執行 Auto Generate Stop End Symbol 相同，差異僅在於由 Prefab 資源開啟內容並於變更後自動儲存 (該 Prefab 尚未存檔的變更會先寫入)。</para>
        /// </summary>
        /// <param name="prefabPath">Prefab 的資源路徑 (ex: Assets/Prefabs/ShopUI.prefab)</param>
        /// <returns></returns>
        public static GenerateResult GenerateByPrefabPath(string prefabPath)
        {
            var result = new GenerateResult();

            using (var logCollector = new AutomationUtility.LogCollector())
            {
                GameObject root = null;
                try
                {
                    GameObject prefab = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        Debug.LogError($"Cannot find the prefab asset. Prefab Path: {prefabPath}");
                        return result;
                    }

                    // 先寫入該 Prefab 尚未存檔的變更 (例如其他工具直接修改 Prefab 資源後尚未儲存)，
                    // 否則以下由磁碟開啟內容並儲存時，會將尚未存檔的變更覆蓋
                    AssetDatabase.SaveAssetIfDirty(prefab);

                    // 開啟 Prefab 內容 (不可編輯的 Prefab 會拋出例外，例如模型 Prefab)
                    root = PrefabUtility.LoadPrefabContents(prefabPath);

                    // 開始向下搜索
                    _StartSearchDown(root);

                    // 記錄生成前的節點名稱 (用於判斷是否有變更)
                    string[] lastNames = new string[_listGos.Count];
                    for (int i = 0; i < _listGos.Count; i++)
                        lastNames[i] = _listGos[i].name;

                    // 生成 StopEnd
                    _GenerateStopEndSymbol();

                    var stopEndNodes = new List<string>();
                    for (int i = 0; i < _listGos.Count; i++)
                    {
                        if (_listGos[i].name != lastNames[i])
                            result.changed = true;
                        if (Binder.CheckNodeHasStopEnd(_listGos[i].name))
                            stopEndNodes.Add(_GetNodePath(_listGos[i].transform));
                    }
                    result.stopEndNodes = stopEndNodes.ToArray();

                    // 有變更時才儲存 Prefab
                    if (result.changed)
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                    result.generated = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
                finally
                {
                    _listGos.Clear();
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                    result.messages = logCollector.GetMessages();
                }
            }

            return result;
        }

        /// <summary>
        /// 取得節點的路徑 (由根節點起算)
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private static string _GetNodePath(Transform node)
        {
            string path = node.name;
            while (node.parent != null)
            {
                node = node.parent;
                path = $"{node.name}/{path}";
            }
            return path;
        }
        #endregion

        #region Check
        private static bool _CheckIsRoot()
        {
            foreach (var select in Selection.gameObjects)
            {
                if (select.GetInstanceID() != select.transform.root.gameObject.GetInstanceID())
                {
                    Debug.Log($"<color=#ff2486>Selected is not a root node => Selected: <color=#ffb824>{select.name}</color>, Root: <color=#ffec24>{select.transform.root.name}</color></color>", select.transform.root);
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Search
        private static void _StartSearchDown(GameObject root)
        {
            _listGos.Clear();
            _SearchDown(root);
        }

        private static void _SearchDown(GameObject go)
        {
            _listGos.Add(go);

            // 遞迴蒐集
            foreach (Transform child in go.transform)
            {
                _SearchDown(child.gameObject);
            }
        }
        #endregion

        #region Generate
        internal static void _GenerateStopEndSymbol()
        {
            if (_listGos.Count == 0) return;

            // 清除原有的 StopEnd
            foreach (var go in _listGos)
            {
                if (Binder.CheckNodeHasStopEnd(go.name))
                {
                    // 與新增 StopEnd 相同，改名前先記錄 Undo (否則清除的部分無法復原，也不會被標記為已變更)
                    Undo.RecordObject(go, $"Modified Name with Stop End {go.name}");
                    go.name = go.name.Replace(FrameConfig.BIND_STOP_END, string.Empty);
                }
            }

            // 由最後開始查找到第一個綁定物件後停止
            int lastIdx = -1;
            for (int i = _listGos.Count - 1; i >= 0; i--)
            {
                var lastGo = _listGos[i];
                if (Binder.CheckNodeHasPrefix(lastGo.name))
                {
                    lastIdx = i;
                    break;
                }
            }

            if (lastIdx != -1)
            {
                GameObject endNode = ((lastIdx + 1) == _listGos.Count) ? null : _listGos[lastIdx + 1];

                // 最後節點為綁定物件
                if (endNode == null)
                {
                    endNode = _listGos[lastIdx];
                    Debug.Log($"<color=#fff472>Last node is a bind object, doesn't need to generate stop end symbol: <color=#72e4ff>{endNode.name}</color></color>", endNode);
                    return;
                }

                // 最後綁定物件的下一個節點
                Undo.RecordObject(endNode, $"Modified Name with Stop End {endNode.name}");
                endNode.name = $"{endNode.name}{FrameConfig.BIND_STOP_END}";

                Debug.Log($"<color=#b5ff72>Auto generate stop end symbol ({FrameConfig.BIND_STOP_END}) => EndNode: <color=#72e4ff>{endNode.name}</color></color>", endNode);
            }
            else
            {
                // RectTransform (Prefab)
                if (_listGos.Count > 2 && _listGos[0].name.Contains("(Environment)"))
                {
                    GameObject firstChild = _listGos[2];
                    Undo.RecordObject(firstChild, $"Modified Name with Stop End {firstChild.name}");
                    firstChild.name = $"{firstChild.name}{FrameConfig.BIND_STOP_END}";

                    Debug.Log($"<color=#ff72b3>There are no any bind objects, generate stop end symbol to first child: <color=#72e4ff>{firstChild.name}</color></color>", firstChild);
                }
                // Transform (Prefab)
                else if (_listGos.Count > 1 && !_listGos[0].name.Contains("(Environment)"))
                {
                    GameObject firstChild = _listGos[1];
                    Undo.RecordObject(firstChild, $"Modified Name with Stop End {firstChild.name}");
                    firstChild.name = $"{firstChild.name}{FrameConfig.BIND_STOP_END}";

                    Debug.Log($"<color=#ff72b3>There are no any bind objects, generate stop end symbol to first child: <color=#72e4ff>{firstChild.name}</color></color>", firstChild);
                }
            }
        }
        #endregion
    }
}