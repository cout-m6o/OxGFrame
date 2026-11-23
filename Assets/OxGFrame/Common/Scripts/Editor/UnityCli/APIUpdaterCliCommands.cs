using Unity.Pipeline.Commands;

namespace OxGFrame.Common.Editor.UnityCli
{
    /// <summary>
    /// API Updater 的 Unity CLI 指令 (unity command oxgframe.apiupdater [name])
    /// <para>僅於專案安裝 com.unity.pipeline 時編譯；指令本身不含任何比對規則，一律轉呼叫框架既有的 API Updater。</para>
    /// </summary>
    internal static class APIUpdaterCliCommands
    {
        private const string _GROUP = "oxgframe.apiupdater";

        /// <summary>
        /// 腳本變更後的後續步驟 (編輯器重新編譯並重載腳本期間 Pipeline 服務會重啟，此時送出的指令可能得不到回應)
        /// </summary>
        private const string _RECOMPILE_STEPS = "run 'script recompile', poll 'script recompile_status' until it reports completed or up_to_date, then wait until 'editor status' reports compiling=false before sending the next command.";

        [CliGroupCommand(_GROUP, "Scan the project scripts for deprecated OxGFrame APIs (same scan as OxGFrame/Others/API Updater). Writes one record per match and changes nothing.", Name = "scan", Effect = CommandEffect.ReadOnly)]
        internal static ExitCode Scan(
            CommandContext cmd,
            [CliArg("timeout", "Timeout in seconds")] double timeoutInSeconds = 300)
        {
            APIUpdateScanResult result = APIUpdater.Scan();

            foreach (APIUpdateMatch match in result.matches)
            {
                cmd.Result.WriteRecord(new
                {
                    file = match.assetPath,
                    line = match.lineNumber,
                    declaringType = match.rule.declaringTypeName,
                    oldApi = match.rule.oldApi,
                    newApi = match.GetEffectiveNewApi() ?? match.rule.newApi,
                    ready = match.selected && match.CanApply(),
                    reviewReason = match.reviewReason,
                    text = (match.lineText ?? string.Empty).Trim()
                });
            }

            cmd.Log.Info($"Scanned {result.scannedFileCount} script(s) with OxGFrame {result.frameworkVersion}: {result.GetReadyMatchCount()} ready, {result.GetReviewMatchCount()} need review, {result.manualOnlyRules.Count} rule(s) need manual migration.");
            return ExitCode.Success;
        }

        [CliGroupCommand(_GROUP, "Apply the deprecated-API updates that need no manual review (the matches the API Updater window selects by default). Rewrites project scripts: requires confirm=true; use dry-run to preview. Not undoable via Ctrl+Z. After it changed a script the editor recompiles: " + _RECOMPILE_STEPS, Name = "apply", Effect = CommandEffect.Destructive)]
        internal static ExitCode Apply(
            CommandContext cmd,
            [CliArg("confirm", "Apply the updates. Without it the call is refused.")] bool confirm = false,
            [CliArg("dry-run", "Preview how many updates would be applied without changing anything.")] bool dryRun = false,
            [CliArg("timeout", "Timeout in seconds")] double timeoutInSeconds = 300)
        {
            APIUpdateScanResult result = APIUpdater.Scan();
            int selected = result.GetSelectedCount();
            int needsReview = result.GetReviewMatchCount();

            if (dryRun)
            {
                cmd.Result.Write(new { dryRun = true, selected, needsReview, applied = 0, skipped = 0, changedFiles = 0 });
                return ExitCode.Success;
            }

            if (!confirm)
            {
                cmd.Log.Error("Refusing to rewrite project scripts. Pass confirm=true to apply, or dry-run=true to preview.");
                return ExitCode.InvalidUsage;
            }

            APIUpdater.ApplyResult applyResult = APIUpdater.Apply(result.matches);

            cmd.Result.Write(new
            {
                dryRun = false,
                selected,
                needsReview,
                applied = applyResult.appliedCount,
                skipped = applyResult.skippedCount,
                changedFiles = applyResult.changedFileCount
            });
            return ExitCode.Success;
        }
    }
}
