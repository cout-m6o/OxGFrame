using System.Collections.Generic;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace OxGFrame.Hotfixer.Editor.UnityCli
{
    /// <summary>
    /// Hotfixer 的 Unity CLI 指令 (unity command oxgframe.hotfixer [name])
    /// <para>僅於專案安裝 com.unity.pipeline 時編譯；指令本身不含任何生成規則，一律轉呼叫框架既有的編輯器工具。</para>
    /// </summary>
    internal static class HotfixerCliCommands
    {
        private const string _GROUP = "oxgframe.hotfixer";

        [CliGroupCommand(_GROUP, "Export the hotfix manifest (HotfixManifest.dat) to StreamingAssets with the OxGFrame exporter (same as OxGFrame/Hotfixer/Hotfix Config Generator). Overwriting an existing file requires confirm=true; use dry-run to preview. Not undoable via Ctrl+Z.", Name = "export_hotfix_config", Effect = CommandEffect.Destructive)]
        internal static ExitCode ExportHotfixConfig(
            CommandContext cmd,
            [CliArg("hotfix-dlls", "Comma-separated hotfix dll names with the .dll suffix, e.g. MyGame.Hotfix.Runtime.dll", Required = true)] string hotfixDlls,
            [CliArg("aot-dlls", "Comma-separated AOT dll names with the .dll suffix, e.g. mscorlib.dll,UniTask.dll")] string aotDlls = null,
            [CliArg("cipher", "Export the encrypted form (same as Cipher Process of the window). Default: plaintext")] bool cipher = false,
            [CliArg("confirm", "Overwrite the existing file. Without it an existing file is not replaced.")] bool confirm = false,
            [CliArg("dry-run", "Preview the export without writing anything.")] bool dryRun = false)
        {
            List<string> hotfixDllNames = _SplitNames(hotfixDlls);
            List<string> aotDllNames = _SplitNames(aotDlls);
            if (hotfixDllNames.Count == 0)
            {
                cmd.Log.Error("No hotfix dll name was given.");
                return ExitCode.InvalidUsage;
            }

            string file = Path.Combine(Application.streamingAssetsPath, $"{HotfixSettings.settings.hotfixCfgName}{HotfixSettings.settings.hotfixCfgExtension}").Replace('\\', '/');
            bool overwrite = File.Exists(file);

            if (dryRun)
            {
                cmd.Result.Write(new { file, overwrite, dryRun = true, cipher, aotDlls = aotDllNames, hotfixDlls = hotfixDllNames });
                return ExitCode.Success;
            }

            if (overwrite &&
                !confirm)
            {
                cmd.Log.Error($"Refusing to overwrite '{file}'. Pass confirm=true to apply, or dry-run=true to preview.");
                return ExitCode.InvalidUsage;
            }

            HotfixHelper.ExportHotfixDllConfig(aotDllNames, hotfixDllNames, cipher);
            AssetDatabase.Refresh();

            cmd.Result.Write(new { file, overwrite, dryRun = false, cipher, aotDlls = aotDllNames, hotfixDlls = hotfixDllNames });
            return ExitCode.Success;
        }

        [CliGroupCommand(_GROUP, "Compile the hotfix dlls for the active build target and copy the AOT and hotfix dlls into the HotfixCollector folder (same as HybridCLR/OxGFrame With HybridCLR/Compile And Copy To HotfixCollector). Files in the collector folders that are not part of the new output are removed.", Name = "compile_and_copy", Effect = CommandEffect.Destructive)]
        internal static ExitCode CompileAndCopy(
            CommandContext cmd,
            [CliArg("timeout", "Timeout in seconds")] double timeoutInSeconds = 600)
        {
            HotfixHelper.CompileAndCopyToHotfixCollector();

            cmd.Result.Write(new { buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString() });
            return ExitCode.Success;
        }

        /// <summary>
        /// 將以逗號分隔的名稱轉為列表 (去除前後空白與空項目)
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        private static List<string> _SplitNames(string names)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(names))
                return list;

            foreach (string name in names.Split(','))
            {
                string trimmed = name.Trim();
                if (trimmed.Length > 0)
                    list.Add(trimmed);
            }

            return list;
        }
    }
}
