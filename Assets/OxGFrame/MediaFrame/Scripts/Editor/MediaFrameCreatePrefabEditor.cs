using OxGFrame.MediaFrame.AudioFrame;
using OxGFrame.MediaFrame.VideoFrame;
using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace OxGFrame.MediaFrame.Editor
{
    public static class MediaFrameCreatePrefabEditor
    {
        private class DoCreatePrefabAsset : EndNameEditAction
        {
            // Subclass and override this method to create specialised prefab asset creation functions
            public virtual GameObject CreateGameObject(string name)
            {
                return new GameObject(name);
            }

            public virtual void OnPrefabCreated(GameObject prefab)
            {
                // Override this to perform actions after prefab is created
            }

            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                GameObject go = this.CreateGameObject(Path.GetFileNameWithoutExtension(pathName));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, pathName);
                GameObject.DestroyImmediate(go);

                // Call post-creation hook
                this.OnPrefabCreated(prefab);

                // Save assets to ensure changes are persisted
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private class DoCreateAudioSolePrefabAsset : DoCreatePrefabAsset
        {
            public override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(AudioBase));

                // sole settings
                AudioBase audBase = obj.GetComponent<AudioBase>();
                audBase.audioType.soundType = SoundType.Sole;
                audBase.loops = -1;
                audBase.onStopAndDestroy = true;
                audBase.onDestroyAndUnload = true;
                audBase.autoEndToStop = false;

                return obj;
            }
        }

        private class DoCreateAudioSoundEffectPrefabAsset : DoCreatePrefabAsset
        {
            public override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(AudioBase));

                // sound effect settings
                AudioBase audBase = obj.GetComponent<AudioBase>();
                audBase.audioType.soundType = SoundType.SoundEffect;
                audBase.loops = 0;
                audBase.onStopAndDestroy = true;
                audBase.onDestroyAndUnload = false;
                audBase.autoEndToStop = true;

                return obj;
            }
        }

        private class DoCreateVideoRenderTexturePrefabAsset : DoCreatePrefabAsset
        {
            public override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(VideoBase));

                // render texture settings
                VideoBase vidBase = obj.GetComponent<VideoBase>();
                vidBase.renderMode = OxGFrame.MediaFrame.VideoFrame.RenderMode.RenderTexture;
                vidBase.onStopAndDestroy = true;
                vidBase.onDestroyAndUnload = true;
                vidBase.autoEndToStop = true;

                return obj;
            }
        }

        private class DoCreateVideoCameraPrefabAsset : DoCreatePrefabAsset
        {
            public override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(VideoBase));

                // camera settings
                VideoBase vidBase = obj.GetComponent<VideoBase>();
                vidBase.renderMode = OxGFrame.MediaFrame.VideoFrame.RenderMode.Camera;
                vidBase.onStopAndDestroy = true;
                vidBase.onDestroyAndUnload = true;
                vidBase.autoEndToStop = true;

                return obj;
            }
        }

        private class DoCreateAudioSolePrefabFromClip : DoCreatePrefabAsset
        {
            public AudioClip audioClip;

            public override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(AudioBase));

                // sole settings
                AudioBase audBase = obj.GetComponent<AudioBase>();
                audBase.audioType.soundType = SoundType.Sole;
                audBase.loops = -1;
                audBase.onStopAndDestroy = true;
                audBase.onDestroyAndUnload = true;
                audBase.autoEndToStop = false;

                // 設置 AudioClip
                if (this.audioClip != null)
                {
                    audBase.sourceType = AudioFrame.SourceType.Audio;
                    audBase.audioClip = this.audioClip;
                }

                return obj;
            }

            public override void OnPrefabCreated(GameObject prefab)
            {
                // Preload audio length after prefab is created
                if (this.audioClip != null)
                {
                    AudioBase audBase = prefab.GetComponent<AudioBase>();
                    if (audBase != null)
                    {
                        audBase.audioLength = this.audioClip.length;
                        EditorUtility.SetDirty(prefab);
                        Debug.Log($"<color=#ffe700>AudioClip Info => Channel: {this.audioClip.channels}, Frequency: {this.audioClip.frequency}, Sample: {this.audioClip.samples}, Length: {this.audioClip.length}, State: {this.audioClip.loadState}, Preload Audio Data*: {this.audioClip.preloadAudioData}</color>");
                    }
                }
            }
        }

        private class DoCreateAudioSoundEffectPrefabFromClip : DoCreatePrefabAsset
        {
            public AudioClip audioClip;

            public override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(AudioBase));

                // sound effect settings
                AudioBase audBase = obj.GetComponent<AudioBase>();
                audBase.audioType.soundType = SoundType.SoundEffect;
                audBase.loops = 0;
                audBase.onStopAndDestroy = true;
                audBase.onDestroyAndUnload = false;
                audBase.autoEndToStop = true;

                // 設置 AudioClip
                if (this.audioClip != null)
                {
                    audBase.sourceType = AudioFrame.SourceType.Audio;
                    audBase.audioClip = this.audioClip;
                }

                return obj;
            }

            public override void OnPrefabCreated(GameObject prefab)
            {
                // Preload audio length after prefab is created
                if (this.audioClip != null)
                {
                    AudioBase audBase = prefab.GetComponent<AudioBase>();
                    if (audBase != null)
                    {
                        audBase.audioLength = this.audioClip.length;
                        EditorUtility.SetDirty(prefab);
                        Debug.Log($"<color=#ffe700>AudioClip Info => Channel: {this.audioClip.channels}, Frequency: {this.audioClip.frequency}, Sample: {this.audioClip.samples}, Length: {this.audioClip.length}, State: {this.audioClip.loadState}, Preload Audio Data*: {this.audioClip.preloadAudioData}</color>");
                    }
                }
            }
        }

        private class DoCreateVideoRenderTexturePrefabFromClip : DoCreatePrefabAsset
        {
            public UnityEngine.Video.VideoClip videoClip;

            public override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(VideoBase));

                // render texture settings
                VideoBase vidBase = obj.GetComponent<VideoBase>();
                vidBase.renderMode = OxGFrame.MediaFrame.VideoFrame.RenderMode.RenderTexture;
                vidBase.onStopAndDestroy = true;
                vidBase.onDestroyAndUnload = true;
                vidBase.autoEndToStop = true;

                // 設置 VideoClip
                if (this.videoClip != null)
                {
                    vidBase.sourceType = VideoFrame.SourceType.Video;
                    vidBase.videoClip = this.videoClip;
                }

                return obj;
            }
        }

        private class DoCreateVideoCameraPrefabFromClip : DoCreatePrefabAsset
        {
            public UnityEngine.Video.VideoClip videoClip;

            public override GameObject CreateGameObject(string name)
            {
                var obj = new GameObject(name, typeof(VideoBase));

                // camera settings
                VideoBase vidBase = obj.GetComponent<VideoBase>();
                vidBase.renderMode = OxGFrame.MediaFrame.VideoFrame.RenderMode.Camera;
                vidBase.onStopAndDestroy = true;
                vidBase.onDestroyAndUnload = true;
                vidBase.autoEndToStop = true;

                // 設置 VideoClip
                if (this.videoClip != null)
                {
                    vidBase.sourceType = VideoFrame.SourceType.Video;
                    vidBase.videoClip = this.videoClip;
                }

                return obj;
            }
        }

        private static void _CreatePrefabAsset(string name, DoCreatePrefabAsset createAction)
        {
            string directory = _GetSelectedAssetDirectory();
            string path = Path.Combine(directory, $"{name}.prefab");
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, createAction, path, EditorGUIUtility.FindTexture("Prefab Icon"), null);
        }

        private static void _CreatePrefabAssetDirectly(string name, string directory, DoCreatePrefabAsset createAction)
        {
            try
            {
                string path = Path.Combine(directory, $"{name}.prefab");
                path = AssetDatabase.GenerateUniqueAssetPath(path);

                GameObject go = createAction.CreateGameObject(Path.GetFileNameWithoutExtension(path));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                GameObject.DestroyImmediate(go);

                // Call post-creation hook
                createAction.OnPrefabCreated(prefab);
            }
            finally
            {
                // 直接建立的流程不經過 Project 視窗的命名編輯，createAction (HideAndDontSave) 不會被自動清理，使用後必須手動銷毀
                if (createAction != null)
                    ScriptableObject.DestroyImmediate(createAction);
            }
        }

        private static string _GetSelectedAssetDirectory()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);

            // 未選取任何資產時 (例如由選單列建立) 路徑為空字串，Path.GetDirectoryName("") 會拋出 ArgumentException，此時回退至 Assets 根目錄
            // 結尾保留 "/"，讓 Path.Combine 在 Windows 也組出以 "Assets/" 開頭的路徑 (Unity 以此判斷是否為完整的資產路徑)
            if (string.IsNullOrEmpty(path))
                return "Assets/";

            if (Directory.Exists(path))
                return path;
            else
                return Path.GetDirectoryName(path);
        }

        #region Automation
        /// <summary>
        /// 可用的 Template Prefab 名稱
        /// </summary>
        public static readonly string[] TEMPLATE_NAMES = new string[]
        {
            "audio_sole",
            "audio_sound_effect",
            "video_render_texture",
            "video_camera"
        };

        /// <summary>
        /// 於指定路徑建立 Template Prefab (供 Unity CLI 與自動化流程調用)
        /// <para>執行與選單相同的建立動作，差異僅在於不進入 Project 視窗的命名編輯；指定 Clip 時與 Convert 選單相同，會一併設置 Clip。</para>
        /// </summary>
        /// <param name="templateName">Template 名稱 (audio_sole, audio_sound_effect, video_render_texture, video_camera)</param>
        /// <param name="assetPath">Prefab 的資源路徑 (ex: Assets/Prefabs/Audio/Click.prefab)</param>
        /// <param name="clipPath">AudioClip 或 VideoClip 的資源路徑 (選填)</param>
        /// <param name="error">失敗原因 (成功時為 null)</param>
        /// <returns></returns>
        public static bool CreatePrefabFromTemplate(string templateName, string assetPath, string clipPath, out string error)
        {
            if (System.Array.IndexOf(TEMPLATE_NAMES, templateName) == -1)
            {
                error = $"Unknown template name: \"{templateName}\" (available: {string.Join(", ", TEMPLATE_NAMES)}).";
                Debug.LogError(error);
                return false;
            }

            // 取得指定的 Clip (類型需與 Template 相符)
            bool hasClip = !string.IsNullOrEmpty(clipPath);
            bool isAudio = templateName.StartsWith("audio_", System.StringComparison.Ordinal);
            AudioClip audioClip = (hasClip && isAudio) ? AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath) : null;
            UnityEngine.Video.VideoClip videoClip = (hasClip && !isAudio) ? AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(clipPath) : null;
            if (hasClip &&
                audioClip == null &&
                videoClip == null)
            {
                error = $"Cannot find the {(isAudio ? nameof(AudioClip) : nameof(UnityEngine.Video.VideoClip))} asset. Clip Path: {clipPath}";
                Debug.LogError(error);
                return false;
            }

            if (!_PrepareCreateAssetPath(ref assetPath, ".prefab", out error))
            {
                Debug.LogError(error);
                return false;
            }

            DoCreatePrefabAsset createAction;
            switch (templateName)
            {
                case "audio_sole":
                    if (hasClip)
                    {
                        var action = ScriptableObject.CreateInstance<DoCreateAudioSolePrefabFromClip>();
                        action.audioClip = audioClip;
                        createAction = action;
                    }
                    else
                        createAction = ScriptableObject.CreateInstance<DoCreateAudioSolePrefabAsset>();
                    break;
                case "audio_sound_effect":
                    if (hasClip)
                    {
                        var action = ScriptableObject.CreateInstance<DoCreateAudioSoundEffectPrefabFromClip>();
                        action.audioClip = audioClip;
                        createAction = action;
                    }
                    else
                        createAction = ScriptableObject.CreateInstance<DoCreateAudioSoundEffectPrefabAsset>();
                    break;
                case "video_render_texture":
                    if (hasClip)
                    {
                        var action = ScriptableObject.CreateInstance<DoCreateVideoRenderTexturePrefabFromClip>();
                        action.videoClip = videoClip;
                        createAction = action;
                    }
                    else
                        createAction = ScriptableObject.CreateInstance<DoCreateVideoRenderTexturePrefabAsset>();
                    break;
                default:
                    if (hasClip)
                    {
                        var action = ScriptableObject.CreateInstance<DoCreateVideoCameraPrefabFromClip>();
                        action.videoClip = videoClip;
                        createAction = action;
                    }
                    else
                        createAction = ScriptableObject.CreateInstance<DoCreateVideoCameraPrefabAsset>();
                    break;
            }

            try
            {
                createAction.Action(0, assetPath, null);
            }
            finally
            {
                // 不經過命名編輯時 createAction 不會被自動清理，使用後必須手動銷毀
                if (createAction != null)
                    ScriptableObject.DestroyImmediate(createAction);
            }

            if (!File.Exists(assetPath))
            {
                error = $"Failed to create the prefab: {assetPath}";
                Debug.LogError(error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 檢查並準備欲建立資源的路徑
        /// <para>必須位於 Assets 或 Packages 內、副檔名相符且尚未存在；所屬資料夾不存在時自動建立 (僅限 Assets 內)。</para>
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="extension"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        private static bool _PrepareCreateAssetPath(ref string assetPath, string extension, out string error)
        {
            error = null;
            assetPath = string.IsNullOrEmpty(assetPath) ? string.Empty : assetPath.Replace('\\', '/');

            bool isInAssets = assetPath.StartsWith("Assets/", System.StringComparison.Ordinal);
            bool isInPackages = assetPath.StartsWith("Packages/", System.StringComparison.Ordinal);
            if ((!isInAssets && !isInPackages) ||
                !assetPath.EndsWith(extension, System.StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(assetPath)) ||
                System.Array.IndexOf(assetPath.Split('/'), "..") != -1)
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
        #endregion

        #region Template Prefab 創建菜單
        [MenuItem("Assets/Create/OxGFrame/Media Frame/Audio/Template Prefabs/Template Audio Sole (Sole Audio Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateTplAudioSole()
        {
            _CreatePrefabAsset("NewTplAudioSole", ScriptableObject.CreateInstance<DoCreateAudioSolePrefabAsset>());
        }

        [MenuItem("Assets/Create/OxGFrame/Media Frame/Audio/Template Prefabs/Template Audio SoundEffect (SoundEffect Audio Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateTplAudioSoundEffect()
        {
            _CreatePrefabAsset("NewTplAudioSoundEffect", ScriptableObject.CreateInstance<DoCreateAudioSoundEffectPrefabAsset>());
        }

        [MenuItem("Assets/Create/OxGFrame/Media Frame/Video/Template Prefabs/Template Video RenderTexture (RenderTexture Video Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateTplVideoRenderTexture()
        {
            _CreatePrefabAsset("NewTplVideoRenderTexture", ScriptableObject.CreateInstance<DoCreateVideoRenderTexturePrefabAsset>());
        }

        [MenuItem("Assets/Create/OxGFrame/Media Frame/Video/Template Prefabs/Template Video Camera (Camera Video Prefab)", isValidateFunction: false, priority: 51)]
        public static void CreateTplVideoCamera()
        {
            _CreatePrefabAsset("NewTplVideoCamera", ScriptableObject.CreateInstance<DoCreateVideoCameraPrefabAsset>());
        }
        #endregion

        #region 從 Clip 轉換為 Prefab 的菜單
        [MenuItem("Assets/OxGFrame/Media Frame/Convert/Convert to Audio Sole (Sole Audio Prefab)", isValidateFunction: false, priority: 30)]
        public static void ConvertToAudioSole()
        {
            var clips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);
            if (clips.Length == 0) return;

            string directory = _GetSelectedAssetDirectory();

            foreach (var clip in clips)
            {
                var createAction = ScriptableObject.CreateInstance<DoCreateAudioSolePrefabFromClip>();
                createAction.audioClip = clip;
                _CreatePrefabAssetDirectly(clip.name, directory, createAction);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00ff00>Successfully converted {clips.Length} AudioClip(s) to Audio Sole Prefab(s)!</color>");
        }

        [MenuItem("Assets/OxGFrame/Media Frame/Convert/Convert to Audio Sole (Sole Audio Prefab)", isValidateFunction: true)]
        public static bool ValidateConvertToAudioSole()
        {
            return Selection.GetFiltered<AudioClip>(SelectionMode.Assets).Length > 0;
        }

        [MenuItem("Assets/OxGFrame/Media Frame/Convert/Convert to Audio SoundEffect (SoundEffect Audio Prefab)", isValidateFunction: false, priority: 30)]
        public static void ConvertToAudioSoundEffect()
        {
            var clips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);
            if (clips.Length == 0) return;

            string directory = _GetSelectedAssetDirectory();

            foreach (var clip in clips)
            {
                var createAction = ScriptableObject.CreateInstance<DoCreateAudioSoundEffectPrefabFromClip>();
                createAction.audioClip = clip;
                _CreatePrefabAssetDirectly(clip.name, directory, createAction);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00ff00>Successfully converted {clips.Length} AudioClip(s) to Audio SoundEffect Prefab(s)!</color>");
        }

        [MenuItem("Assets/OxGFrame/Media Frame/Convert/Convert to Audio SoundEffect (SoundEffect Audio Prefab)", isValidateFunction: true)]
        public static bool ValidateConvertToAudioSoundEffect()
        {
            return Selection.GetFiltered<AudioClip>(SelectionMode.Assets).Length > 0;
        }

        [MenuItem("Assets/OxGFrame/Media Frame/Convert/Convert to Video RenderTexture (RenderTexture Video Prefab)", isValidateFunction: false, priority: 30)]
        public static void ConvertToVideoRenderTexture()
        {
            var clips = Selection.GetFiltered<UnityEngine.Video.VideoClip>(SelectionMode.Assets);
            if (clips.Length == 0) return;

            string directory = _GetSelectedAssetDirectory();

            foreach (var clip in clips)
            {
                var createAction = ScriptableObject.CreateInstance<DoCreateVideoRenderTexturePrefabFromClip>();
                createAction.videoClip = clip;
                _CreatePrefabAssetDirectly(clip.name, directory, createAction);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00ff00>Successfully converted {clips.Length} VideoClip(s) to Video RenderTexture Prefab(s)!</color>");
        }

        [MenuItem("Assets/OxGFrame/Media Frame/Convert/Convert to Video RenderTexture (RenderTexture Video Prefab)", isValidateFunction: true)]
        public static bool ValidateConvertToVideoRenderTexture()
        {
            return Selection.GetFiltered<UnityEngine.Video.VideoClip>(SelectionMode.Assets).Length > 0;
        }

        [MenuItem("Assets/OxGFrame/Media Frame/Convert/Convert to Video Camera (Camera Video Prefab)", isValidateFunction: false, priority: 30)]
        public static void ConvertToVideoCamera()
        {
            var clips = Selection.GetFiltered<UnityEngine.Video.VideoClip>(SelectionMode.Assets);
            if (clips.Length == 0) return;

            string directory = _GetSelectedAssetDirectory();

            foreach (var clip in clips)
            {
                var createAction = ScriptableObject.CreateInstance<DoCreateVideoCameraPrefabFromClip>();
                createAction.videoClip = clip;
                _CreatePrefabAssetDirectly(clip.name, directory, createAction);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00ff00>Successfully converted {clips.Length} VideoClip(s) to Video Camera Prefab(s)!</color>");
        }

        [MenuItem("Assets/OxGFrame/Media Frame/Convert/Convert to Video Camera (Camera Video Prefab)", isValidateFunction: true)]
        public static bool ValidateConvertToVideoCamera()
        {
            return Selection.GetFiltered<UnityEngine.Video.VideoClip>(SelectionMode.Assets).Length > 0;
        }
        #endregion
    }
}