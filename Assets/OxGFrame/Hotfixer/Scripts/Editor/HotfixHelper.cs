#if HYBRIDCLR_9_OR_NEWER
// [HybridCLR v9] 編輯器命名空間調整 (移除 Editor 層級)
using HybridCLR;
using HybridCLR.Commands;
#else
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
#endif
using System;
using System.Collections.Generic;
using System.IO;
#if HYBRIDCLR_9_OR_NEWER
using System.Text.RegularExpressions;
#endif
using UnityEditor;
using UnityEngine;

namespace OxGFrame.Hotfixer.Editor
{
    public static class HotfixHelper
    {
        internal const string MENU_ROOT = "OxGFrame/Hotfixer/";

        internal const string HOTFIX_COLLECTOR_DIR = "HotfixCollector";
        internal const string AOT_DLLS_DIR = "AOTDlls";
        internal const string HOTFIX_DLLS_DIR = "HotfixDlls";

        #region Public Methods
        /// <summary>
        /// To Relative Asset Path
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToRelativeAssetPath(string s)
        {
            return s.Substring(s.IndexOf("Assets/"));
        }

        /// <summary>
        /// Copy AOT Assembly and Hotfix Assembly files to HotfixCollector
        /// </summary>
        public static void CopyAOTDllsAndHotfixDlls()
        {
            CopyAOTAssembliesToDestination();
            CopyHotfixAssembliesToDestination();
        }

        /// <summary>
        /// Copy AOT Assembly files to destination
        /// </summary>
        /// <param name="dstDir"></param>
        public static void CopyAOTAssembliesToDestination(string dstDir = null)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;

            string aotAssembliesSrcDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            string aotAssembliesDstDir = string.IsNullOrEmpty(dstDir) ? Path.Combine(Application.dataPath, HOTFIX_COLLECTOR_DIR, AOT_DLLS_DIR) : dstDir;

            // 取得需補充元數據的 AOT 組件名稱 (不含 .dll 擴展名)
            List<string> aotAssemblyNames = _GetAOTAssemblyNames();

            // 僅清除不屬於本次輸出的文件 (來源不存在的 dll 不會被複製，因此也不保留其舊文件)
            var aotDllBytesFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dll in aotAssemblyNames)
            {
                if (File.Exists($"{aotAssembliesSrcDir}/{dll}.dll"))
                    aotDllBytesFileNames.Add($"{dll}.dll.bytes");
            }
            _CleanDestinationDirectory(aotAssembliesDstDir, aotDllBytesFileNames);

            ulong totalBytesSize = 0;
            foreach (var dll in aotAssemblyNames)
            {
                string dllPath = $"{aotAssembliesSrcDir}/{dll}.dll";
                if (!File.Exists(dllPath))
                {
                    Debug.LogError($"Add Metadata for AOT dll: {dllPath} is failed (File does not exist). The stripped AOT dlls can only be generated during BuildPlayer. Please do generate AOTDlls, after finished and then do copy again.");
                    continue;
                }
                FileInfo fileInfo = new FileInfo(dllPath);
                ulong bytesSize = (ulong)fileInfo.Length;
                totalBytesSize += bytesSize;
                string dllBytesPath = $"{aotAssembliesDstDir}/{dll}.dll.bytes";
                File.Copy(dllPath, dllBytesPath, true);

                Debug.Log($"<color=#a4ff86>[Copy AOTAssemblies To HotfixCollector] (AOT) <color=#ffcbde>{dll}.dll.bytes</color>, <color=#ffb542>Size: {GetBytesToString(bytesSize)}</color>, <color=#42ddff>{dllPath}</color> -> <color=#c686ff>{dllBytesPath}</color></color>");
            }

            Debug.Log($"<color=#ffb542>AOT Assemblies TotalSize: {GetBytesToString(totalBytesSize)}</color>");
        }

        /// <summary>
        /// Copy Hotfix Assembly files to destination
        /// </summary>
        /// <param name="dstDir"></param>
        public static void CopyHotfixAssembliesToDestination(string dstDir = null)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;

            string hotfixDllSrcDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            string hotfixAssembliesDstDir = string.IsNullOrEmpty(dstDir) ? Path.Combine(Application.dataPath, HOTFIX_COLLECTOR_DIR, HOTFIX_DLLS_DIR) : dstDir;

            // 僅清除不屬於本次輸出的文件
            var hotfixDllBytesFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
                hotfixDllBytesFileNames.Add($"{dll}.bytes");
            _CleanDestinationDirectory(hotfixAssembliesDstDir, hotfixDllBytesFileNames);

            ulong totalBytesSize = 0;
            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
            {
                string dllPath = $"{hotfixDllSrcDir}/{dll}";
                FileInfo fileInfo = new FileInfo(dllPath);
                ulong bytesSize = (ulong)fileInfo.Length;
                totalBytesSize += bytesSize;
                string dllBytesPath = $"{hotfixAssembliesDstDir}/{dll}.bytes";
                File.Copy(dllPath, dllBytesPath, true);

                Debug.Log($"<color=#a4ff86>[Copy Hotfix Assemblies To HotfixCollector] (Hotfix) <color=#ffcbde>{dll}.bytes</color>, <color=#ffb542>Size: {GetBytesToString(bytesSize)}</color>, <color=#42ddff>{dllPath}</color> -> <color=#c686ff>{dllBytesPath}</color></color>");
            }

            Debug.Log($"<color=#ffb542>Hotfix Assemblies TotalSize: {GetBytesToString(totalBytesSize)}</color>");
        }

        /// <summary>
        /// 產生 Hotfix Dll 配置文件至輸出路徑 (Export HotfixDllConfig to StreamingAssets [for Built-in])
        /// </summary>
        /// <param name="aotDlls"></param>
        /// <param name="hotfixDlls"></param>
        /// <param name="outputPath"></param>
        public static void ExportHotfixDllConfig(List<string> aotDlls, List<string> hotfixDlls, bool cipher)
        {
            HotfixManifest config = new HotfixManifest(aotDlls, hotfixDlls);

            // 寫入配置文件
            WriteConfig(config, cipher ? ConfigFileType.Bytes : ConfigFileType.Json);

            Debug.Log($"【Export {HotfixSettings.settings.hotfixCfgName}{HotfixSettings.settings.hotfixCfgExtension} Completes】");
        }
        #endregion

        #region Internal Methods
        /// <summary>
        /// 取得需補充元數據的 AOT 組件名稱 (不含 .dll 擴展名)
        /// <para> [HybridCLR v8 以前] 取自 HybridCLRSettings 的 patchAOTAssemblies 設置 </para>
        /// <para> [HybridCLR v9] 該設置已被移除，改為取自 HybridCLR 生成的 AOTGenericReferences 腳本中的 PatchedAOTAssemblyList </para>
        /// </summary>
        /// <returns></returns>
        private static List<string> _GetAOTAssemblyNames()
        {
#if HYBRIDCLR_9_OR_NEWER
            var aotAssemblyNames = new List<string>();

            // ※備註：直接解析生成的腳本內容，於生成後無需等待重新編譯即可取得最新的清單 (ex: 接續於 HybridCLR/Generate/All 之後執行)
            string filePath = $"{Application.dataPath}/{SettingsUtil.HybridCLRSettings.outputAOTGenericReferenceFile}";
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"Cannot find the generated AOTGenericReferences file: {filePath}. No AOT dlls will be copied. Please execute [HybridCLR/Generate/All] first.");
                return aotAssemblyNames;
            }

            string content = File.ReadAllText(filePath);
            Match listMatch = Regex.Match(content, @"PatchedAOTAssemblyList\s*=\s*new\s+List\s*<\s*string\s*>\s*\{(?<names>[^}]*)\}");
            if (!listMatch.Success)
            {
                Debug.LogWarning($"Cannot find the PatchedAOTAssemblyList in the generated AOTGenericReferences file: {filePath}. No AOT dlls will be copied. Please execute [HybridCLR/Generate/All] again.");
                return aotAssemblyNames;
            }

            string fileExtension = ".dll";
            foreach (Match nameMatch in Regex.Matches(listMatch.Groups["names"].Value, "\"(?<name>[^\"]+)\""))
            {
                // 移除 .dll 擴展名 (與 patchAOTAssemblies 的格式一致)
                string name = nameMatch.Groups["name"].Value;
                if (name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - fileExtension.Length);
                if (!aotAssemblyNames.Contains(name))
                    aotAssemblyNames.Add(name);
            }

            return aotAssemblyNames;
#else
            return SettingsUtil.AOTAssemblyNames;
#endif
        }

        /// <summary>
        /// 清理輸出資料夾 (僅刪除不屬於本次輸出的文件與子資料夾)
        /// <para> 保留仍會被覆蓋的文件與其 .meta，避免每次複製皆刪除整個資料夾，導致所有 dll 的 .meta 被重新產生 (GUID 改變會使內容未變更的 dll 也被視為不同的資源) </para>
        /// </summary>
        /// <param name="dstDir"></param>
        /// <param name="keepFileNames"></param>
        private static void _CleanDestinationDirectory(string dstDir, HashSet<string> keepFileNames)
        {
            if (!Directory.Exists(dstDir))
            {
                Directory.CreateDirectory(dstDir);
                return;
            }

            // 刪除所有子資料夾 (輸出資料夾僅會存放 dll 文件)
            foreach (var subDir in Directory.GetDirectories(dstDir))
                Directory.Delete(subDir, true);

            // 刪除不屬於本次輸出的文件 (.meta 則依照其對應的文件決定是否保留)
            string metaExtension = ".meta";
            foreach (var filePath in Directory.GetFiles(dstDir))
            {
                string fileName = Path.GetFileName(filePath);
                string ownerFileName = fileName.EndsWith(metaExtension, StringComparison.OrdinalIgnoreCase) ?
                    fileName.Substring(0, fileName.Length - metaExtension.Length) : fileName;
                if (!keepFileNames.Contains(ownerFileName))
                    File.Delete(filePath);
            }
        }

        /// <summary>
        /// Bytes ToString (KB, MB, GB)
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        internal static string GetBytesToString(ulong bytes)
        {
            if (bytes < (1024 * 1024 * 1f))
            {
                return (bytes / 1024f).ToString("f2") + "KB";
            }
            else if (bytes >= (1024 * 1024 * 1f) && bytes < (1024 * 1024 * 1024 * 1f))
            {
                return (bytes / (1024 * 1024 * 1f)).ToString("f2") + "MB";
            }
            else
            {
                return (bytes / (1024 * 1024 * 1024 * 1f)).ToString("f2") + "GB";
            }
        }

        /// <summary>
        /// 寫入配置文件
        /// </summary>
        /// <param name="hotfixDllConfig"></param>
        /// <param name="configFileType"></param>
        internal static void WriteConfig(HotfixManifest hotfixDllConfig, ConfigFileType configFileType = ConfigFileType.Bytes)
        {
            string fileName = $"{HotfixSettings.settings.hotfixCfgName}{HotfixSettings.settings.hotfixCfgExtension}";
            string savePath = Path.Combine(Application.streamingAssetsPath, fileName);

            // 獲取文件夾路徑
            string directoryPath = Path.GetDirectoryName(savePath);

            // 檢查文件夾是否存在，如果不存在則創建
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Debug.Log($"Created directory: {directoryPath}");
            }

            switch (configFileType)
            {
                // Json 類型
                case ConfigFileType.Json:
                    {
                        // 將配置轉換為 JSON 字符串
                        string json = JsonUtility.ToJson(hotfixDllConfig, true);

                        // 寫入文件
                        File.WriteAllText(savePath, json);
                        AssetDatabase.Refresh();
                        Debug.Log($"Saved Hotfix Dll Config JSON to: {savePath}");
                    }
                    break;

                // Bytes 類型
                case ConfigFileType.Bytes:
                    {
                        // 將配置轉換為 JSON 字符串
                        string json = JsonUtility.ToJson(hotfixDllConfig, false);

                        // Binary
                        var writeBuffer = BinaryHelper.EncryptToBytes(json);

                        // 寫入配置文件
                        File.WriteAllBytes(savePath, writeBuffer);
                        AssetDatabase.Refresh();
                        Debug.Log($"Saved Hotfix Dll Config BYTES to: {savePath}");
                    }
                    break;
            }
        }

        /// <summary>
        /// 寫入文字文件
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="outputPath"></param>
        internal static void WriteTxt(string txt, string outputPath)
        {
            // 寫入配置文件
            var file = File.CreateText(outputPath);
            file.Write(txt);
            file.Close();
        }
        #endregion

        #region MenuItems
        [MenuItem("HybridCLR/OxGFrame With HybridCLR/Compile And Copy To HotfixCollector")]
        public static void CompileAndCopyToHotfixCollector()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            CompileDllCommand.CompileDll(target);
            CopyAOTDllsAndHotfixDlls();
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/OxGFrame/Hotfixer/Convert HotfixManifest.dat (BYTES [Cipher] <-> JSON [Plaintext])", false, -99)]
        private static void _ConvertConfigFile()
        {
            UnityEngine.Object selectedObject = Selection.activeObject;

            if (selectedObject != null)
            {
                // 獲取選中的資源的相對路徑
                string assetPath = AssetDatabase.GetAssetPath(selectedObject);

                // 檢查選中的文件是否位於 StreamingAssets 資料夾內
                if (assetPath.StartsWith("Assets/StreamingAssets"))
                {
                    // Application.dataPath 返回的是 Assets 資料夾的完整路徑
                    string fullPath = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length), assetPath);

                    // 確保文件存在
                    if (File.Exists(fullPath))
                    {
                        string fileName = $"{HotfixSettings.settings.hotfixCfgName}{HotfixSettings.settings.hotfixCfgExtension}";
                        if (fullPath.IndexOf(fileName) == -1)
                        {
                            Debug.LogWarning($"Incorrect file selected. Please select the {fileName} file.");
                            return;
                        }

                        // 讀取文件內容
                        byte[] data = File.ReadAllBytes(fullPath);
                        var info = BinaryHelper.DecryptToString(data);
                        HotfixManifest config = null;
                        bool isJsonConvertible;

                        try
                        {
                            config = JsonUtility.FromJson<HotfixManifest>(info.content);
                            isJsonConvertible = true;
                        }
                        catch (Exception ex)
                        {
                            isJsonConvertible = false;
                            Debug.LogException(new Exception("Convert failed: The content format is not valid JSON or it does not match the expected structure.", ex));
                        }

                        if (isJsonConvertible && config != null)
                        {
                            // 根據文件類型進行轉換
                            switch (info.type)
                            {
                                case ConfigFileType.Json:
                                    // JSON to Bytes
                                    WriteConfig(config, ConfigFileType.Bytes);
                                    break;
                                case ConfigFileType.Bytes:
                                    // Bytes to JSON
                                    WriteConfig(config, ConfigFileType.Json);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError($"File does not exist at path: {fullPath}");
                    }
                }
                else
                {
                    Debug.LogWarning("Selected file is not in StreamingAssets folder.");
                }
            }
            else
            {
                Debug.LogWarning("No file selected.");
            }
        }
        #endregion
    }
}