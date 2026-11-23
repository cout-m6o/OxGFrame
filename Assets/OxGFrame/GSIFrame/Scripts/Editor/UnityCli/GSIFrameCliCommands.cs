using System.IO;
using System.Linq;
using Unity.Pipeline.Commands;

namespace OxGFrame.GSIFrame.Editor.UnityCli
{
    /// <summary>
    /// GSIFrame 的 Unity CLI 指令 (unity command oxgframe.gsiframe [name])
    /// <para>僅於專案安裝 com.unity.pipeline 時編譯；指令本身不含任何生成規則，一律轉呼叫框架既有的編輯器工具。</para>
    /// </summary>
    internal static class GSIFrameCliCommands
    {
        private const string _GROUP = "oxgframe.gsiframe";

        /// <summary>
        /// 腳本變更後的後續步驟 (編輯器重新編譯並重載腳本期間 Pipeline 服務會重啟，此時送出的指令可能得不到回應)
        /// </summary>
        private const string _RECOMPILE_STEPS = "run 'script recompile', poll 'script recompile_status' until it reports completed or up_to_date, then wait until 'editor status' reports compiling=false before sending the next command.";

        [CliGroupCommand(_GROUP, "Create a game stage (GSIBase) or game stage manager (GSIManagerBase) script from the OxGFrame template (same content as Assets/Create/OxGFrame/GSI Frame/Template Scripts) at an explicit path. Never overwrites an existing file. The new type is not usable until the scripts recompile: " + _RECOMPILE_STEPS, Name = "create_script", Effect = CommandEffect.Additive)]
        internal static ExitCode CreateScript(
            CommandContext cmd,
            [CliArg("template", "Template name: gsi (game stage) or gsi_manager (game stage manager)", Required = true)] string template,
            [CliArg("path", "Script asset path to create, e.g. Assets/Scripts/Stages/LoginStage.cs", Required = true)] string path)
        {
            if (!GSIFrameCreateScriptEditor.TEMPLATE_NAMES.Contains(template))
            {
                cmd.Log.Error($"Unknown template '{template}'. Available: {string.Join(", ", GSIFrameCreateScriptEditor.TEMPLATE_NAMES)}.");
                return ExitCode.InvalidUsage;
            }

            if (File.Exists(path))
            {
                cmd.Log.Error($"'{path}' already exists. Template scripts never overwrite an existing file.");
                return ExitCode.InvalidUsage;
            }

            if (!GSIFrameCreateScriptEditor.CreateScriptFromTemplate(template, path, out string error))
            {
                cmd.Log.Error(error);
                return ExitCode.Failed;
            }

            cmd.Result.Write(new { path = path.Replace('\\', '/'), template });
            return ExitCode.Success;
        }
    }
}
