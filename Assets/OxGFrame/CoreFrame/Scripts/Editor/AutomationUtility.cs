using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace OxGFrame.CoreFrame.Editor
{
    /// <summary>
    /// 自動化流程的共用工具 (供 Unity CLI 與自動化流程的路徑式入口使用)
    /// </summary>
    internal static class AutomationUtility
    {
        /// <summary>
        /// 收集執行期間輸出至 Console 的訊息 (讓自動化流程能取得工具本身的提示)
        /// </summary>
        internal sealed class LogCollector : IDisposable
        {
            private readonly List<string> _messages = new List<string>();

            public LogCollector()
            {
                Application.logMessageReceived += this._OnLogMessageReceived;
            }

            public void Dispose()
            {
                Application.logMessageReceived -= this._OnLogMessageReceived;
            }

            /// <summary>
            /// 取得已收集的訊息
            /// </summary>
            /// <returns></returns>
            public string[] GetMessages()
            {
                return this._messages.ToArray();
            }

            private void _OnLogMessageReceived(string condition, string stackTrace, LogType type)
            {
                // 移除 Rich Text 的顏色標籤，僅保留訊息本身
                this._messages.Add($"[{type}] {Regex.Replace(condition ?? string.Empty, "</?color[^>]*>", string.Empty)}");
            }
        }

        /// <summary>
        /// 檢查並準備欲建立資源的路徑
        /// <para>必須位於 Assets 或 Packages 內、副檔名相符且尚未存在；所屬資料夾不存在時自動建立 (僅限 Assets 內)。</para>
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="extension"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        internal static bool PrepareCreateAssetPath(ref string assetPath, string extension, out string error)
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
    }
}
