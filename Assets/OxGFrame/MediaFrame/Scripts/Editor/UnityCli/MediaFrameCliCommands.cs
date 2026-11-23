using System.IO;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace OxGFrame.MediaFrame.Editor.UnityCli
{
    /// <summary>
    /// MediaFrame 的 Unity CLI 指令 (unity command oxgframe.mediaframe [name])
    /// <para>僅於專案安裝 com.unity.pipeline 時編譯；指令本身不含任何生成規則，一律轉呼叫框架既有的編輯器工具。</para>
    /// </summary>
    internal static class MediaFrameCliCommands
    {
        private const string _GROUP = "oxgframe.mediaframe";

        [CliGroupCommand(_GROUP, "Create an audio or video prefab from the OxGFrame template (same as Assets/Create/OxGFrame/Media Frame/.../Template Prefabs) at an explicit path. With 'clip' the clip is assigned like the Convert menu does. Never overwrites an existing file.", Name = "create_prefab", Effect = CommandEffect.Additive)]
        internal static ExitCode CreatePrefab(
            CommandContext cmd,
            [CliArg("template", "Template name: audio_sole, audio_sound_effect, video_render_texture or video_camera", Required = true)] string template,
            [CliArg("path", "Prefab asset path to create, e.g. Assets/Prefabs/Audio/Click.prefab", Required = true)] string path,
            [CliArg("clip", "Optional AudioClip or VideoClip asset path to assign, e.g. Assets/Audio/click.mp3")] string clip = null)
        {
            if (!MediaFrameCreatePrefabEditor.TEMPLATE_NAMES.Contains(template))
            {
                cmd.Log.Error($"Unknown template '{template}'. Available: {string.Join(", ", MediaFrameCreatePrefabEditor.TEMPLATE_NAMES)}.");
                return ExitCode.InvalidUsage;
            }

            if (File.Exists(path))
            {
                cmd.Log.Error($"'{path}' already exists. Template prefabs never overwrite an existing file.");
                return ExitCode.InvalidUsage;
            }

            if (!string.IsNullOrEmpty(clip) &&
                AssetDatabase.LoadMainAssetAtPath(clip) == null)
            {
                cmd.Log.Error($"No clip at '{clip}'.");
                return ExitCode.NotFound;
            }

            if (!MediaFrameCreatePrefabEditor.CreatePrefabFromTemplate(template, path, clip, out string error))
            {
                cmd.Log.Error(error);
                return ExitCode.Failed;
            }

            cmd.Result.Write(new { path = path.Replace('\\', '/'), template, clip });
            return ExitCode.Success;
        }

        [CliGroupCommand(_GROUP, "Export the media url config (MediaConfig.txt) with the OxGFrame exporter (same as OxGFrame/MediaFrame/Media Url Config Generator). Overwriting an existing file requires confirm=true; use dry-run to preview. Not undoable via Ctrl+Z.", Name = "export_url_config", Effect = CommandEffect.Destructive)]
        internal static ExitCode ExportUrlConfig(
            CommandContext cmd,
            [CliArg("audio-url", "Audio source url path. Default: http://127.0.0.1/audio/")] string audioUrl = null,
            [CliArg("video-url", "Video source url path. Default: http://127.0.0.1/video/")] string videoUrl = null,
            [CliArg("output-path", "Output folder. Default: the StreamingAssets folder")] string outputPath = null,
            [CliArg("confirm", "Overwrite the existing file. Without it an existing file is not replaced.")] bool confirm = false,
            [CliArg("dry-run", "Preview the export without writing anything.")] bool dryRun = false)
        {
            if (string.IsNullOrEmpty(outputPath))
                outputPath = Application.streamingAssetsPath;

            string file = Path.Combine(outputPath, MediaConfig.MEDIA_URL_CFG_NAME).Replace('\\', '/');
            bool overwrite = File.Exists(file);

            if (dryRun)
            {
                cmd.Result.Write(new { file, overwrite, dryRun = true });
                return ExitCode.Success;
            }

            if (overwrite &&
                !confirm)
            {
                cmd.Log.Error($"Refusing to overwrite '{file}'. Pass confirm=true to apply, or dry-run=true to preview.");
                return ExitCode.InvalidUsage;
            }

            MediaHelper.ExportMediaUrlConfig(audioUrl, videoUrl, outputPath);
            AssetDatabase.Refresh();

            cmd.Result.Write(new { file, overwrite, dryRun = false });
            return ExitCode.Success;
        }
    }
}
