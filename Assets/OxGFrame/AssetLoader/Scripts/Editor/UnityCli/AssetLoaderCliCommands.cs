using OxGFrame.AssetLoader.Bundle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace OxGFrame.AssetLoader.Editor.UnityCli
{
    /// <summary>
    /// AssetLoader 的 Unity CLI 指令 (unity command oxgframe.assetloader [name])
    /// <para>僅於專案安裝 com.unity.pipeline 時編譯；指令本身不含任何生成規則，一律轉呼叫框架既有的編輯器工具。</para>
    /// </summary>
    internal static class AssetLoaderCliCommands
    {
        /// <summary>
        /// CDN 輸出的操作類型 (對應 Export Bundle And Config Generator 視窗的三種 CDN 操作)
        /// </summary>
        private enum CdnExportOperation
        {
            ConfigsAndAppBundles,
            AppBundlesWithoutConfigs,
            IndividualDlcBundles
        }

        private const string _GROUP = "oxgframe.assetloader";

        /// <summary>
        /// CDN 輸出指令共用的說明 (來源、輸出路徑的清空規則與確認方式)
        /// </summary>
        private const string _CDN_EXPORT_RULES = "The bundles must already be built for the active build target; the newest built version of each package is exported and nothing is written when a package has no built version. The output folder is <export-folder>/<Root Folder Name of the PatchSettings> and it is cleared first unless keep-output=true. An existing output folder requires confirm=true; use dry-run to preview. Not undoable via Ctrl+Z.";

        [CliGroupCommand(_GROUP, "Export the app config (AppInfo.json) with the OxGFrame exporter (same as the export app config operation of OxGFrame/AssetLoader/Export Bundle And Config Generator). Overwriting an existing file requires confirm=true; use dry-run to preview. Not undoable via Ctrl+Z.", Name = "export_app_config", Effect = CommandEffect.Destructive)]
        internal static ExitCode ExportAppConfig(
            CommandContext cmd,
            [CliArg("product-name", "Product name. Default: the Product Name of the project")] string productName = null,
            [CliArg("app-version", "App version, e.g. 1.0.0. Default: the Version of the project")] string appVersion = null,
            [CliArg("build-target", "Build target name, e.g. Android or StandaloneWindows64. Default: the active build target")] string buildTarget = null,
            [CliArg("output-path", "Output folder. Default: the StreamingAssets folder")] string outputPath = null,
            [CliArg("confirm", "Overwrite the existing file. Without it an existing file is not replaced.")] bool confirm = false,
            [CliArg("dry-run", "Preview the export without writing anything.")] bool dryRun = false)
        {
            // 未指定時使用當前的 Build Target (與視窗勾選 Use Active Build Target 相同)
            bool activeBuildTarget = string.IsNullOrEmpty(buildTarget);
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (!activeBuildTarget &&
                (!Enum.TryParse(buildTarget, true, out target) || !Enum.IsDefined(typeof(BuildTarget), target)))
            {
                cmd.Log.Error($"Unknown build target '{buildTarget}'.");
                return ExitCode.InvalidUsage;
            }

            if (string.IsNullOrEmpty(productName))
                productName = Application.productName;
            if (string.IsNullOrEmpty(appVersion))
                appVersion = Application.version;
            if (string.IsNullOrEmpty(outputPath))
                outputPath = Application.streamingAssetsPath;

            string file = Path.Combine(outputPath, $"{PatchSettings.settings.appCfgName}{PatchSettings.settings.appCfgExtension}").Replace('\\', '/');
            bool overwrite = File.Exists(file);
            string platform = target.ToString();

            if (dryRun)
            {
                cmd.Result.Write(new { file, overwrite, dryRun = true, productName, appVersion, platform });
                return ExitCode.Success;
            }

            if (overwrite &&
                !confirm)
            {
                cmd.Log.Error($"Refusing to overwrite '{file}'. Pass confirm=true to apply, or dry-run=true to preview.");
                return ExitCode.InvalidUsage;
            }

            BundleHelper.ExportAppConfig(productName, appVersion, outputPath, activeBuildTarget, target);
            AssetDatabase.Refresh();

            cmd.Result.Write(new { file, overwrite, dryRun = false, productName, appVersion, platform });
            return ExitCode.Success;
        }

        [CliGroupCommand(_GROUP, "Export the bundle url config (CdnConfig.dat) with the OxGFrame exporter (same as OxGFrame/AssetLoader/Bundle Url Config Generator). Overwriting an existing file requires confirm=true; use dry-run to preview. Not undoable via Ctrl+Z.", Name = "export_bundle_url_config", Effect = CommandEffect.Destructive)]
        internal static ExitCode ExportBundleUrlConfig(
            CommandContext cmd,
            [CliArg("bundle-ip", "First CDN server IP or domain (plan A). Default: http://127.0.0.1")] string bundleIp = null,
            [CliArg("bundle-fallback-ip", "Second CDN server IP or domain (plan B). Default: http://127.0.0.1")] string bundleFallbackIp = null,
            [CliArg("store-link", "Store link of the app. Default: http://")] string storeLink = null,
            [CliArg("cipher", "Export the encrypted form (same as Cipher Process of the window). Default: plaintext")] bool cipher = false,
            [CliArg("output-path", "Output folder. Default: the StreamingAssets folder")] string outputPath = null,
            [CliArg("confirm", "Overwrite the existing file. Without it an existing file is not replaced.")] bool confirm = false,
            [CliArg("dry-run", "Preview the export without writing anything.")] bool dryRun = false)
        {
            if (string.IsNullOrEmpty(outputPath))
                outputPath = Application.streamingAssetsPath;

            string file = Path.Combine(outputPath, $"{PatchSettings.settings.bundleUrlCfgName}{PatchSettings.settings.bundleUrlCfgExtension}").Replace('\\', '/');
            bool overwrite = File.Exists(file);

            if (dryRun)
            {
                cmd.Result.Write(new { file, overwrite, dryRun = true, cipher });
                return ExitCode.Success;
            }

            if (overwrite &&
                !confirm)
            {
                cmd.Log.Error($"Refusing to overwrite '{file}'. Pass confirm=true to apply, or dry-run=true to preview.");
                return ExitCode.InvalidUsage;
            }

            BundleHelper.ExportBundleUrlConfig(bundleIp, bundleFallbackIp, storeLink, outputPath, cipher);
            AssetDatabase.Refresh();

            cmd.Result.Write(new { file, overwrite, dryRun = false, cipher });
            return ExitCode.Success;
        }

        [CliGroupCommand(_GROUP, "Export the newest built bundles of the app packages and the DLC packages with the app config and the patch config for the CDN server (same as the Export Configs And App Bundles For CDN operation of OxGFrame/AssetLoader/Export Bundle And Config Generator). " + _CDN_EXPORT_RULES, Name = "export_configs_and_app_bundles", Effect = CommandEffect.Destructive)]
        internal static ExitCode ExportConfigsAndAppBundles(
            CommandContext cmd,
            [CliArg("app-packages", "Comma-separated app package names, e.g. DefaultPackage,HotfixPackage", Required = true)] string appPackages,
            [CliArg("dlc-packages", "DLC packages as name,version joined by '#' without spaces, e.g. Dlc1,1.0#Dlc2, (keep the comma; an empty version names the DLC folder by the newest package version). Default: none")] string dlcPackages = null,
            [CliArg("dlc-without-platform", "Comma-separated names of the DLC packages that are exported without the platform folder (same as Without Platform of the window). Default: none")] string dlcWithoutPlatform = null,
            [CliArg("group-infos", "Group tags of the patch config as group,tag,... joined by '#' without spaces, e.g. g1,t1#g2,t1,t2 (same as Group Info Args of the window). Default: none")] string groupInfos = null,
            [CliArg("product-name", "Product name. Default: the Product Name of the project")] string productName = null,
            [CliArg("app-version", "App version, e.g. 1.0.0. Default: the Version of the project")] string appVersion = null,
            [CliArg("semantic-patch", "Name the version folder by major.minor.patch (same as the Patch toggle of the window). Default: major.minor")] bool semanticPatch = false,
            [CliArg("build-target", "Platform name of the output folder and the app config, e.g. Android. Default: the active build target. The bundles are always read from the folder of the active build target.")] string buildTarget = null,
            [CliArg("source-folder", "Build output root of the bundles. Default: the default build output root of YooAsset")] string sourceFolder = null,
            [CliArg("export-folder", "Export folder. Default: the ExportBundles folder of the project")] string exportFolder = null,
            [CliArg("keep-output", "Do not clear the output folder first; files with the same name are replaced.")] bool keepOutput = false,
            [CliArg("confirm", "Allow an existing output folder to be cleared or written into.")] bool confirm = false,
            [CliArg("dry-run", "Preview the export without writing anything.")] bool dryRun = false,
            [CliArg("timeout", "Timeout in seconds")] double timeoutInSeconds = 600)
        {
            return _ExportForCdn(cmd, CdnExportOperation.ConfigsAndAppBundles, appPackages, dlcPackages, dlcWithoutPlatform, groupInfos, productName, appVersion, semanticPatch, buildTarget, sourceFolder, exportFolder, keepOutput, confirm, dryRun);
        }

        [CliGroupCommand(_GROUP, "Export the newest built bundles of the app packages and the DLC packages for the CDN server without the configs (same as the Export App Bundles For CDN Without Configs operation of OxGFrame/AssetLoader/Export Bundle And Config Generator). " + _CDN_EXPORT_RULES, Name = "export_app_bundles", Effect = CommandEffect.Destructive)]
        internal static ExitCode ExportAppBundles(
            CommandContext cmd,
            [CliArg("app-packages", "Comma-separated app package names, e.g. DefaultPackage,HotfixPackage", Required = true)] string appPackages,
            [CliArg("dlc-packages", "DLC packages as name,version joined by '#' without spaces, e.g. Dlc1,1.0#Dlc2, (keep the comma; an empty version names the DLC folder by the newest package version). Default: none")] string dlcPackages = null,
            [CliArg("dlc-without-platform", "Comma-separated names of the DLC packages that are exported without the platform folder (same as Without Platform of the window). Default: none")] string dlcWithoutPlatform = null,
            [CliArg("product-name", "Product name. Default: the Product Name of the project")] string productName = null,
            [CliArg("app-version", "App version, e.g. 1.0.0. Default: the Version of the project")] string appVersion = null,
            [CliArg("semantic-patch", "Name the version folder by major.minor.patch (same as the Patch toggle of the window). Default: major.minor")] bool semanticPatch = false,
            [CliArg("build-target", "Platform name of the output folder, e.g. Android. Default: the active build target. The bundles are always read from the folder of the active build target.")] string buildTarget = null,
            [CliArg("source-folder", "Build output root of the bundles. Default: the default build output root of YooAsset")] string sourceFolder = null,
            [CliArg("export-folder", "Export folder. Default: the ExportBundles folder of the project")] string exportFolder = null,
            [CliArg("keep-output", "Do not clear the output folder first; files with the same name are replaced.")] bool keepOutput = false,
            [CliArg("confirm", "Allow an existing output folder to be cleared or written into.")] bool confirm = false,
            [CliArg("dry-run", "Preview the export without writing anything.")] bool dryRun = false,
            [CliArg("timeout", "Timeout in seconds")] double timeoutInSeconds = 600)
        {
            return _ExportForCdn(cmd, CdnExportOperation.AppBundlesWithoutConfigs, appPackages, dlcPackages, dlcWithoutPlatform, null, productName, appVersion, semanticPatch, buildTarget, sourceFolder, exportFolder, keepOutput, confirm, dryRun);
        }

        [CliGroupCommand(_GROUP, "Export the newest built bundles of the DLC packages for the CDN server (same as the Export Individual DLC Bundles For CDN operation of OxGFrame/AssetLoader/Export Bundle And Config Generator). " + _CDN_EXPORT_RULES, Name = "export_individual_dlc_bundles", Effect = CommandEffect.Destructive)]
        internal static ExitCode ExportIndividualDlcBundles(
            CommandContext cmd,
            [CliArg("dlc-packages", "DLC packages as name,version joined by '#' without spaces, e.g. Dlc1,1.0#Dlc2, (keep the comma; an empty version names the DLC folder by the newest package version)", Required = true)] string dlcPackages,
            [CliArg("dlc-without-platform", "Comma-separated names of the DLC packages that are exported without the platform folder (same as Without Platform of the window). Default: none")] string dlcWithoutPlatform = null,
            [CliArg("product-name", "Product name. Default: the Product Name of the project")] string productName = null,
            [CliArg("build-target", "Platform name of the output folder, e.g. Android. Default: the active build target. The bundles are always read from the folder of the active build target.")] string buildTarget = null,
            [CliArg("source-folder", "Build output root of the bundles. Default: the default build output root of YooAsset")] string sourceFolder = null,
            [CliArg("export-folder", "Export folder. Default: the ExportBundles folder of the project")] string exportFolder = null,
            [CliArg("keep-output", "Do not clear the output folder first; files with the same name are replaced.")] bool keepOutput = false,
            [CliArg("confirm", "Allow an existing output folder to be cleared or written into.")] bool confirm = false,
            [CliArg("dry-run", "Preview the export without writing anything.")] bool dryRun = false,
            [CliArg("timeout", "Timeout in seconds")] double timeoutInSeconds = 600)
        {
            return _ExportForCdn(cmd, CdnExportOperation.IndividualDlcBundles, null, dlcPackages, dlcWithoutPlatform, null, productName, null, false, buildTarget, sourceFolder, exportFolder, keepOutput, confirm, dryRun);
        }

        /// <summary>
        /// 執行 CDN 輸出 (三個 CDN 輸出指令共用，輸出路徑的組合方式與調用順序皆與 Export Bundle And Config Generator 視窗的 Process 相同)
        /// <para>所有檢查皆於輸出前完成，檢查未通過時不會清空或寫入任何資料夾。</para>
        /// </summary>
        /// <returns></returns>
        private static ExitCode _ExportForCdn(
            CommandContext cmd,
            CdnExportOperation operation,
            string appPackages,
            string dlcPackages,
            string dlcWithoutPlatform,
            string groupInfos,
            string productName,
            string appVersion,
            bool semanticPatch,
            string buildTarget,
            string sourceFolder,
            string exportFolder,
            bool keepOutput,
            bool confirm,
            bool dryRun)
        {
            bool withAppPackages = operation != CdnExportOperation.IndividualDlcBundles;

            // 未指定時使用當前的 Build Target (與視窗勾選 Use Active Build Target 相同)
            bool activeBuildTarget = string.IsNullOrEmpty(buildTarget);
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (!activeBuildTarget &&
                (!Enum.TryParse(buildTarget, true, out target) || !Enum.IsDefined(typeof(BuildTarget), target)))
            {
                cmd.Log.Error($"Unknown build target '{buildTarget}'.");
                return ExitCode.InvalidUsage;
            }

            // App Packages
            List<string> appPackageNames = withAppPackages ? _SplitNames(appPackages) : new List<string>();
            if (withAppPackages &&
                appPackageNames.Count == 0)
            {
                cmd.Log.Error("No app package name was given.");
                return ExitCode.InvalidUsage;
            }

            // DLC Packages (參數格式與 BundleHelper.ParsingDlcInfosByArgs 相同，先行檢查格式以避免輸出帶有空白的資料夾名稱)
            List<DlcInfo> dlcInfos = new List<DlcInfo>();
            if (!string.IsNullOrEmpty(dlcPackages))
            {
                foreach (string dlc in dlcPackages.Split('#'))
                {
                    string[] args = dlc.Split(',');
                    if (args.Length != 2 ||
                        args[0].Length == 0 ||
                        args.Any(arg => arg != arg.Trim()))
                    {
                        cmd.Log.Error($"Cannot parse dlc-packages '{dlcPackages}'. Expected name,version joined by '#' without spaces, e.g. Dlc1,1.0#Dlc2, (keep the comma; an empty version names the DLC folder by the newest package version).");
                        return ExitCode.InvalidUsage;
                    }
                }

                dlcInfos = BundleHelper.ParsingDlcInfosByArgs(dlcPackages);
            }

            if (!withAppPackages &&
                dlcInfos.Count == 0)
            {
                cmd.Log.Error("No DLC package was given.");
                return ExitCode.InvalidUsage;
            }

            // DLC Packages 是否不區分平台 (與視窗的 Without Platform 相同)
            foreach (string name in _SplitNames(dlcWithoutPlatform))
            {
                DlcInfo dlcInfo = dlcInfos.Find(info => info.packageName == name);
                if (dlcInfo == null)
                {
                    cmd.Log.Error($"'{name}' of dlc-without-platform is not listed in dlc-packages.");
                    return ExitCode.InvalidUsage;
                }

                dlcInfo.withoutPlatform = true;
            }

            // Group Infos (參數格式與視窗的 Group Info Args 及 BundleHelper.ParsingGroupInfosByArgs 相同)
            List<GroupInfo> groups = new List<GroupInfo>();
            if (!string.IsNullOrEmpty(groupInfos))
            {
                foreach (string group in groupInfos.Split('#'))
                {
                    if (group.Split(',').Any(arg => arg.Length == 0 || arg != arg.Trim()))
                    {
                        cmd.Log.Error($"Cannot parse group-infos '{groupInfos}'. Expected group,tag,... joined by '#' without spaces, e.g. g1,t1#g2,t1,t2.");
                        return ExitCode.InvalidUsage;
                    }
                }

                groups = BundleHelper.ParsingGroupInfosByArgs(groupInfos);
            }

            if (string.IsNullOrEmpty(productName))
                productName = Application.productName;
            if (string.IsNullOrEmpty(appVersion))
                appVersion = Application.version;

            // App 版號的段數需足以組合出版號資料夾 (major.minor 或 major.minor.patch)，否則輸出途中才會發生錯誤
            int versionPartCount = semanticPatch ? 3 : 2;
            string[] versionParts = appVersion.Split('.');
            if (withAppPackages &&
                (versionParts.Length < versionPartCount || versionParts.Take(versionPartCount).Any(part => part.Trim().Length == 0)))
            {
                cmd.Log.Error($"The app version '{appVersion}' needs at least {versionPartCount} parts, e.g. {(semanticPatch ? "1.0.0" : "1.0")}.");
                return ExitCode.InvalidUsage;
            }

            // 輸出路徑的組合方式與視窗相同 (Export Folder + Root Folder Name)
            if (string.IsNullOrEmpty(exportFolder))
                exportFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ExportBundles");
            string folder = _GetFullFolderPath($"{exportFolder}/{PatchSettings.settings.rootFolderName}");
            bool overwrite = Directory.Exists(folder);

            // 查詢各 Package 的最新版本 (查詢規則與輸出時相同)
            List<string> packageNames = appPackageNames.Union(dlcInfos.Select(dlcInfo => dlcInfo.packageName)).ToList();
            Dictionary<string, string> newestVersions = BundleHelper.FindNewestPackageVersions(sourceFolder, packageNames, out string sourcePlatformFolder);
            sourcePlatformFolder = _GetFullFolderPath(sourcePlatformFolder);

            // 輸出路徑不可為專案資料夾、Assets 資料夾、Bundle 來源資料夾或其上層資料夾 (避免清空非預期的資料夾)
            string projectFolder = _GetFullFolderPath(Path.GetDirectoryName(Application.dataPath));
            foreach (string protectedFolder in new string[] { projectFolder, $"{projectFolder}/Assets", sourcePlatformFolder })
            {
                if ($"{protectedFolder}/".StartsWith($"{folder.TrimEnd('/')}/", StringComparison.OrdinalIgnoreCase))
                {
                    cmd.Log.Error($"Refusing to use '{folder}' as the output folder because it is or contains '{protectedFolder}'. Pass another export-folder.");
                    return ExitCode.InvalidUsage;
                }
            }

            if (!Directory.Exists(sourcePlatformFolder))
            {
                cmd.Log.Error($"No built bundles at '{sourcePlatformFolder}'. Build the bundles for the active build target first, or pass source-folder.");
                return ExitCode.NotFound;
            }

            List<string> missingPackageNames = packageNames.Where(name => !newestVersions.ContainsKey(name)).ToList();
            if (missingPackageNames.Count > 0)
            {
                cmd.Log.Error($"No built version of the package(s) {string.Join(", ", missingPackageNames)} at '{sourcePlatformFolder}'. Nothing was exported.");
                return ExitCode.NotFound;
            }

            string platform = target.ToString();
            var packages = appPackageNames
                .Select(name => new { name, type = "app", version = newestVersions[name], dlcVersion = (string)null, withoutPlatform = false })
                .Concat(dlcInfos.Select(dlcInfo => new { name = dlcInfo.packageName, type = "dlc", version = newestVersions[dlcInfo.packageName], dlcVersion = dlcInfo.dlcVersion, withoutPlatform = dlcInfo.withoutPlatform }))
                .ToArray();

            if (!dryRun)
            {
                if (overwrite &&
                    !confirm)
                {
                    cmd.Log.Error($"Refusing to {(keepOutput ? "write into" : "clear")} '{folder}'. Pass confirm=true to apply, or dry-run=true to preview.");
                    return ExitCode.InvalidUsage;
                }

                try
                {
                    var semanticRule = new AppConfig.SemanticRule() { PATCH = semanticPatch };
                    switch (operation)
                    {
                        case CdnExportOperation.ConfigsAndAppBundles:
                            BundleHelper.ExportConfigsAndAppBundles(sourceFolder, folder, productName, semanticRule, appVersion, appPackageNames.ToArray(), groups, packageNames.ToArray(), activeBuildTarget, target, !keepOutput);
                            BundleHelper.ExportIndividualDlcBundles(sourceFolder, folder, productName, dlcInfos, activeBuildTarget, target, false);
                            break;
                        case CdnExportOperation.AppBundlesWithoutConfigs:
                            BundleHelper.ExportAppBundles(sourceFolder, folder, productName, semanticRule, appVersion, appPackageNames.ToArray(), activeBuildTarget, target, !keepOutput);
                            BundleHelper.ExportIndividualDlcBundles(sourceFolder, folder, productName, dlcInfos, activeBuildTarget, target, false);
                            break;
                        case CdnExportOperation.IndividualDlcBundles:
                            BundleHelper.ExportIndividualDlcBundles(sourceFolder, folder, productName, dlcInfos, activeBuildTarget, target, !keepOutput);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    cmd.Log.Error($"The export failed and '{folder}' may be incomplete: {ex.Message}");
                    return ExitCode.Failed;
                }
            }

            switch (operation)
            {
                case CdnExportOperation.ConfigsAndAppBundles:
                    cmd.Result.Write(new { folder, overwrite, dryRun, keepOutput, sourceFolder = sourcePlatformFolder, platform, productName, appVersion, semanticPatch, packages, groups = groups.Count });
                    break;
                case CdnExportOperation.AppBundlesWithoutConfigs:
                    cmd.Result.Write(new { folder, overwrite, dryRun, keepOutput, sourceFolder = sourcePlatformFolder, platform, productName, appVersion, semanticPatch, packages });
                    break;
                default:
                    cmd.Result.Write(new { folder, overwrite, dryRun, keepOutput, sourceFolder = sourcePlatformFolder, platform, productName, packages });
                    break;
            }

            return ExitCode.Success;
        }

        /// <summary>
        /// 取得資料夾的完整路徑 (統一為正斜線，並去除結尾的分隔符號)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string _GetFullFolderPath(string path)
        {
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            string rootPath = Path.GetPathRoot(fullPath).Replace('\\', '/');
            // 磁碟根目錄需保留結尾的分隔符號
            return (fullPath.Length > rootPath.Length) ? fullPath.TrimEnd('/') : fullPath;
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
