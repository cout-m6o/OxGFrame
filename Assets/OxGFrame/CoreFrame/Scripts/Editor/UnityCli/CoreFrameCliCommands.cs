using System.IO;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace OxGFrame.CoreFrame.Editor.UnityCli
{
    /// <summary>
    /// CoreFrame 的 Unity CLI 指令 (unity command oxgframe.coreframe [name])
    /// <para>僅於專案安裝 com.unity.pipeline 時編譯；指令本身不含任何生成規則，一律轉呼叫框架既有的編輯器工具。</para>
    /// </summary>
    internal static class CoreFrameCliCommands
    {
        private const string _GROUP = "oxgframe.coreframe";

        /// <summary>
        /// 腳本變更後的後續步驟 (編輯器重新編譯並重載腳本期間 Pipeline 服務會重啟，此時送出的指令可能得不到回應)
        /// </summary>
        private const string _RECOMPILE_STEPS = "run 'script recompile', poll 'script recompile_status' until it reports completed or up_to_date, then wait until 'editor status' reports compiling=false before sending the next command.";

        [CliGroupCommand(_GROUP, "Get the CodeBinding rules of this project (naming symbols, heads, tail/access/attribute rules and the bind method type). Read it before naming prefab nodes for binding.", Name = "get_binding_settings", Effect = CommandEffect.ReadOnly)]
        internal static ExitCode GetBindingSettings(CommandContext cmd)
        {
            CodeBindingSettings settings = CodeBindingSettings.FindSettings(out string assetPath, out string error);
            if (!string.IsNullOrEmpty(error))
            {
                cmd.Log.Error(error);
                return ExitCode.Failed;
            }

            // 專案內尚未建立配置文件時回報預設規則 (生成器於首次執行時會以相同的預設值建立配置文件)
            bool isTemporary = settings == null;
            if (isTemporary)
                settings = ScriptableObject.CreateInstance<CodeBindingSettings>();

            try
            {
                cmd.Result.Write(new
                {
                    asset = assetPath,
                    methodType = settings.methodType.ToString(),
                    method = $"{settings.GetMethodAccessModifier()} override void {settings.GetMethodName()}()",
                    variableCase = settings.variableCaseType.ToString(),
                    indicateModifier = settings.GetIndicateModifier(),
                    headSeparator = FrameConfig.BIND_HEAD_SEPARATOR,
                    tailSeparator = FrameConfig.BIND_TAIL_SEPARATOR,
                    accessSeparator = FrameConfig.BIND_ACCESS_MODIFIER_SEPARATOR,
                    stopEnd = FrameConfig.BIND_STOP_END,
                    heads = FrameConfig.BIND_COMPONENTS.Keys.Select(head => new
                    {
                        head,
                        baseClasses = head.StartsWith(FrameConfig.BIND_PREFIXES[1], System.StringComparison.Ordinal) ? "CPBase" : "UIBase, SRBase"
                    }).ToArray(),
                    accessRules = settings.variableAccessRules.Select(rule => new
                    {
                        access = rule.Key,
                        prefix = rule.Value
                    }).ToArray(),
                    attrRules = settings.attrReferenceRules.Select(rule => new
                    {
                        key = rule.Key,
                        attribute = rule.Value
                    }).ToArray(),
                    tailRules = settings.tailRules.Select(rule => new
                    {
                        tail = rule.Key,
                        component = rule.Value.componentName,
                        usingNamespace = rule.Value.usingNamespace,
                        pluralEndRemoveCount = (int)rule.Value.pluralize.endRemoveCount,
                        pluralEnd = rule.Value.pluralize.endPluralTxt
                    }).ToArray()
                });
            }
            finally
            {
                if (isTemporary)
                    Object.DestroyImmediate(settings);
            }

            return ExitCode.Success;
        }

        [CliGroupCommand(_GROUP, "Run the OxGFrame bind-code generator (same as GameObject/OxGFrame/Auto Generate Bind Codes) on the root of a prefab asset. With method type Auto it rewrites the '#region Binding Components' block of the attached UIBase/SRBase/CPBase script; with Manual it only returns the content. After it rewrote the script (inserted=true) the editor recompiles: " + _RECOMPILE_STEPS, Name = "generate_bind_codes", Effect = CommandEffect.Idempotent)]
        internal static ExitCode GenerateBindCodes(
            CommandContext cmd,
            [CliArg("path", "Prefab asset path, e.g. Assets/Prefabs/UI/ShopUI.prefab", Required = true)] string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                cmd.Log.Error($"No prefab at '{path}'.");
                return ExitCode.NotFound;
            }

            CodeBindingAutoGenerateEditor.GenerateResult result = CodeBindingAutoGenerateEditor.GenerateByPrefabPath(path);
            cmd.Result.Write(new
            {
                prefab = path,
                script = result.scriptPath,
                methodType = result.methodType,
                inserted = result.inserted,
                bindCount = result.bindCount,
                usings = result.usingNamespaces,
                content = result.content,
                messages = result.messages
            });

            if (!result.generated)
            {
                cmd.Log.Error("The bind codes were not generated. See 'messages' for what the generator reported.");
                return ExitCode.Failed;
            }

            // Auto 方式下未寫入腳本時視為失敗 (未掛載腳本、腳本內缺少綁定區塊或腳本非 UTF-8 編碼)
            if (result.methodType == nameof(CodeBindingSettings.MethodType.Auto) &&
                !result.inserted)
            {
                cmd.Log.Error("The bind codes were generated but not written into the script. See 'messages' for what the generator reported.");
                return ExitCode.Failed;
            }

            return ExitCode.Success;
        }

        [CliGroupCommand(_GROUP, "Run the OxGFrame stop-end tool (same as GameObject/OxGFrame/Auto Generate Stop End Symbol) on a prefab asset: marks the node after the last bind node with the stop-end symbol and saves the prefab (unsaved changes of that prefab asset are written first).", Name = "generate_stop_end", Effect = CommandEffect.Idempotent)]
        internal static ExitCode GenerateStopEnd(
            CommandContext cmd,
            [CliArg("path", "Prefab asset path, e.g. Assets/Prefabs/UI/ShopUI.prefab", Required = true)] string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                cmd.Log.Error($"No prefab at '{path}'.");
                return ExitCode.NotFound;
            }

            StopEndAutoGenerateEditor.GenerateResult result = StopEndAutoGenerateEditor.GenerateByPrefabPath(path);
            cmd.Result.Write(new
            {
                prefab = path,
                changed = result.changed,
                stopEndNodes = result.stopEndNodes,
                messages = result.messages
            });

            if (!result.generated)
            {
                cmd.Log.Error("The stop-end symbol was not generated. See 'messages' for what the tool reported.");
                return ExitCode.Failed;
            }

            return ExitCode.Success;
        }

        [CliGroupCommand(_GROUP, "Create a UIBase, SRBase or CPBase script from the OxGFrame template (same content as Assets/Create/OxGFrame/Core Frame/.../Template Scripts) at an explicit path. Never overwrites an existing file. The new type is not usable until the scripts recompile: " + _RECOMPILE_STEPS, Name = "create_script", Effect = CommandEffect.Additive)]
        internal static ExitCode CreateScript(
            CommandContext cmd,
            [CliArg("template", "Template name: ui, sr or cp", Required = true)] string template,
            [CliArg("path", "Script asset path to create, e.g. Assets/Scripts/UI/ShopUI.cs", Required = true)] string path)
        {
            if (!CoreFrameCreateScriptEditor.TEMPLATE_NAMES.Contains(template))
            {
                cmd.Log.Error($"Unknown template '{template}'. Available: {string.Join(", ", CoreFrameCreateScriptEditor.TEMPLATE_NAMES)}.");
                return ExitCode.InvalidUsage;
            }

            if (File.Exists(path))
            {
                cmd.Log.Error($"'{path}' already exists. Template scripts never overwrite an existing file.");
                return ExitCode.InvalidUsage;
            }

            if (!CoreFrameCreateScriptEditor.CreateScriptFromTemplate(template, path, out string error))
            {
                cmd.Log.Error(error);
                return ExitCode.Failed;
            }

            cmd.Result.Write(new { path = path.Replace('\\', '/'), template });
            return ExitCode.Success;
        }

        [CliGroupCommand(_GROUP, "Create a UI, SR or CP prefab from the OxGFrame template (same as Assets/Create/OxGFrame/Core Frame/.../Template Prefabs) at an explicit path. Never overwrites an existing file.", Name = "create_prefab", Effect = CommandEffect.Additive)]
        internal static ExitCode CreatePrefab(
            CommandContext cmd,
            [CliArg("template", "Template name: ui, sr, cp_transform or cp_rect_transform", Required = true)] string template,
            [CliArg("path", "Prefab asset path to create, e.g. Assets/Prefabs/UI/ShopUI.prefab", Required = true)] string path)
        {
            if (!CoreFrameCreatePrefabEditor.TEMPLATE_NAMES.Contains(template))
            {
                cmd.Log.Error($"Unknown template '{template}'. Available: {string.Join(", ", CoreFrameCreatePrefabEditor.TEMPLATE_NAMES)}.");
                return ExitCode.InvalidUsage;
            }

            if (File.Exists(path))
            {
                cmd.Log.Error($"'{path}' already exists. Template prefabs never overwrite an existing file.");
                return ExitCode.InvalidUsage;
            }

            if (!CoreFrameCreatePrefabEditor.CreatePrefabFromTemplate(template, path, out string error))
            {
                cmd.Log.Error(error);
                return ExitCode.Failed;
            }

            cmd.Result.Write(new { path = path.Replace('\\', '/'), template });
            return ExitCode.Success;
        }
    }
}
