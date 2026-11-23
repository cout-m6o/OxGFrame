using Cysharp.Threading.Tasks;
using MyBox;
using OxGKit.LoggingSystem;
using OxGKit.SaverSystem;
using OxGKit.Utilities.Requester;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Video;

namespace OxGFrame.MediaFrame
{
    [DisallowMultipleComponent]
    public abstract class MediaBase : MonoBehaviour
    {
        [Serializable]
        public class UrlSet
        {
            [Tooltip("Allow read URL_PATH from UrlCfg .txt file")]
            public bool getUrlPathFromCfg = true;
            [Tooltip("Get URL_PATH from UrlCfg .txt file"), ConditionalField(nameof(getUrlPathFromCfg))]
            public UrlCfg urlCfg = new UrlCfg();
            [Tooltip("If doesn't use [getUrlPathFromCfg] need to type Entire URL, ex: http://localhost/Audio/example.extension")]
            public string url = "";
        }

        [Serializable]
        public class UrlCfg
        {
            public enum RequestType
            {
                Assign,
                StreamingAssets
            }

            public RequestType requestType = RequestType.Assign;
            [Tooltip("Get URL_PATH from UrlCfg .txt file (Assign a file, This is not supports [WebGL])"), ConditionalField(nameof(requestType), false, RequestType.Assign)]
            public TextAsset file = null;
            [Tooltip("Get URL_PATH from UrlCfg .txt file (StreamingAssets Request)"), ConditionalField(nameof(requestType), false, RequestType.StreamingAssets)]
            public string fullPathName = MediaConfig.MEDIA_URL_CFG_NAME;

            #region Get UrlConfig File Content
            /// <summary>
            /// 已請求過的 UrlCfg 文件內容緩存 (以請求路徑作為 key，避免不同的 UrlCfg 文件取得相同的內容)
            /// </summary>
            private static readonly Dictionary<string, string> _urlCfgContents = new Dictionary<string, string>();
            public async UniTask<string> GetFileText()
            {
                switch (this.requestType)
                {
                    case RequestType.Assign:
                        // 避免未指定或已被銷毀的 TextAsset 造成異常
                        return (this.file != null) ? this.file.text : null;

                    case RequestType.StreamingAssets:
                        string pathName = System.IO.Path.Combine(GetRequestStreamingAssetsPath(), this.fullPathName);
                        if (!_urlCfgContents.TryGetValue(pathName, out string urlCfgContent) ||
                            string.IsNullOrEmpty(urlCfgContent))
                        {
                            urlCfgContent = await Requester.RequestText(pathName, null, null, null, false);
                            _urlCfgContents[pathName] = urlCfgContent;
                        }
                        return urlCfgContent;
                }

                return null;
            }
            #endregion
        }

        /// <summary>
        /// 最大準備 Timeout Seconds
        /// </summary>
        internal const int MAX_PREPARE_TIME_SECONDS = 60;

        /// <summary>
        /// 影音名稱
        /// </summary>
        [HideInInspector]
        public string mediaName { get; protected set; } = string.Empty;

        /// <summary>
        /// (Bundle) AssetName = (Resouce) PathName
        /// </summary>
        [HideInInspector]
        public string assetName { get; protected set; } = string.Empty;

        /// <summary>
        /// If checked, it can be directly placed in the scene and driven by MonoBehaviour
        /// </summary>
        [Tooltip("If checked, it can be directly placed in the scene and driven by MonoBehaviour")]
        public bool monoDrive = false;

        /// <summary>
        /// 是否在 MonoDrive 模式下自動播放
        /// </summary>
        [Tooltip("Whether to autoplay in MonoDrive mode"), ConditionalField(nameof(monoDrive))]
        public bool autoPlay = true;

        /// <summary>
        /// 不受 TimeScale 影響
        /// </summary>
        [Tooltip("Not affected by TimeScale")]
        public bool ignoreTimeScale = true;

        /// <summary>
        /// 最大預備時間
        /// </summary>
        public int maxPrepareTimeSeconds = MAX_PREPARE_TIME_SECONDS;

        /// <summary>
        /// 循環次數
        /// </summary>
        [Tooltip("Repeat times 0 (equal to 1). But -1 = loop (Infinitely)")]
        public int loops = 0;

        /// <summary>
        /// 當前循環次數
        /// </summary>
        protected int _loops = 0;

        /// <summary>
        /// 是否結束時自動停止
        /// </summary>
        [Tooltip("When finished playing will auto to set stop (loops = -1 is invalid)")]
        public bool autoEndToStop = true;

        /// <summary>
        /// 是否停止時銷毀
        /// </summary>
        [Tooltip("when stop will destroy")]
        public bool onStopAndDestroy = true;

        /// <summary>
        /// 是否銷毀時卸載
        /// </summary>
        [Tooltip("when destroy will unload asset (not recommend sound type is SoundEffect)")]
        public bool onDestroyAndUnload = false;

        /// <summary>
        /// 是否暫停
        /// </summary>
        protected bool _isPaused = false;

        /// <summary>
        /// 是否播放中
        /// </summary>
        protected bool _isPlaying = false;

        /// <summary>
        /// 初始標記 (表示確認初始完畢)
        /// </summary>
        protected bool _isInit = false;

        /// <summary>
        /// 影音長度
        /// </summary>
        protected float _mediaLength = 0f;

        /// <summary>
        /// 影音當前長度
        /// </summary>
        protected float _currentRemainingLength = 0f;

        /// <summary>
        /// 停止播放時的事件調用
        /// </summary>
        protected Action _endEvent = null;

        /// <summary>
        /// 影音準備好的標記
        /// </summary>
        public bool isPrepared { get; protected set; } = false;

        /// <summary>
        /// 正在被銷毀的標記
        /// </summary>
        internal bool isDestroying = false;

        private async UniTaskVoid Awake()
        {
            if (this.monoDrive)
            {
                string name = $"{nameof(this.monoDrive)}_{this.name}";
                this.SetNames(name, name);
                bool isInitialized = await this.Init();
                // 初始期間物件已被銷毀時，不需再進行銷毀 (避免存取已銷毀的物件而拋出異常)
                if (!isInitialized &&
                    this != null)
                {
                    Destroy(this.gameObject);
                }
            }
        }

        private async UniTaskVoid Start()
        {
            if (this.monoDrive)
            {
                if (this.autoPlay)
                {
                    // 等待結束後需釋放計時器與 CTS，避免殘留持續計時至逾時的 PlayerLoopTimer
                    using (var cts = new CancellationTokenSource())
                    using (cts.CancelAfterSlim(TimeSpan.FromSeconds(this.maxPrepareTimeSeconds <= 0 ? MAX_PREPARE_TIME_SECONDS : this.maxPrepareTimeSeconds), GetPrepareTimeoutDelayType()))
                    {
                        try
                        {
                            do
                            {
                                // 等待期間物件已被銷毀時 (ex: 初始失敗)，直接結束等待
                                if (this == null)
                                    return;
                                if (this.isPrepared)
                                    break;
                                // load balancing (需使用 Update 輪詢，TimeScale 為 0 時不會調用 FixedUpdate，會導致該期間無法完成等待)
                                await UniTask.Yield(PlayerLoopTiming.Update, cts.Token);
                            } while (true);
                        }
                        catch (OperationCanceledException ex)
                        {
                            Logging.PrintException<Logger>(ex);
                            // 準備逾時，銷毀後不可再進行播放
                            if (this != null)
                                Destroy(this.gameObject);
                            return;
                        }
                    }

                    // Wait until is prepared to play
                    this.Play(0, 0);
                }
            }
        }

        private void OnDestroy()
        {
            this.DetectOnDestroy();
        }

        internal void HandleFixedUpdate(float dt)
        {
            if (!this._isInit)
                return;
            // 播放器因 AudioListener.pause 而停住時，不可進行刷新，避免尚未播放完畢就被判定為播放結束
            if (this.IsPlayerPausedByAudioListener())
                return;
            this.OnFixedUpdate(dt);
        }

        /// <summary>
        /// 返回播放器是否因 AudioListener.pause 而停住 (子類實作)
        /// </summary>
        /// <returns></returns>
        internal virtual bool IsPlayerPausedByAudioListener() { return false; }

        /// <summary>
        /// 處理 TimeScale 為 0 時的刷新 (TimeScale 為 0 時不會調用 FixedUpdate，因此該期間改由 Update 驅動)
        /// </summary>
        /// <param name="dt"></param>
        internal void HandleZeroTimeScaleUpdate(float dt)
        {
            // 受 TimeScale 影響的影音，於 TimeScale 為 0 時不進行刷新 (與使用 fixedDeltaTime 刷新的結果一致)
            if (!this.ignoreTimeScale)
                return;
            // 播放器本身並未持續播放時 (ex: AudioListener.pause)，不可進行刷新，避免尚未播放完畢就被判定為播放結束
            if (!this.IsPlayerAdvancingAtZeroTimeScale())
                return;
            this.HandleFixedUpdate(dt);
        }

        /// <summary>
        /// 返回 TimeScale 為 0 時，播放器是否仍持續播放 (子類實作)
        /// </summary>
        /// <returns></returns>
        internal virtual bool IsPlayerAdvancingAtZeroTimeScale() { return false; }

        #region For MonoDrive
        /// <summary>
        /// TimeScale 為 0 期間的刷新計時 (僅用於 MonoDrive 的自驅動)
        /// </summary>
        private ZeroTimeScaleTimer _zeroTimeScaleTimer;

        /// <summary>
        /// MonoDrive 自驅動的 FixedUpdate 刷新
        /// </summary>
        internal void HandleSelfFixedUpdate()
        {
            // 需於每次 FixedUpdate 取得 (恢復 TimeScale 後，會扣除 TimeScale 為 0 期間已由 Update 刷新的時間)
            float fixedUnscaledDeltaTime = this._zeroTimeScaleTimer.GetFixedUnscaledDeltaTime();
            this.HandleFixedUpdate(this.ignoreTimeScale ? fixedUnscaledDeltaTime : Time.fixedDeltaTime);
        }

        /// <summary>
        /// MonoDrive 自驅動的 Update 刷新 (僅於 TimeScale 為 0 時進行刷新，其餘皆由 FixedUpdate 刷新)
        /// </summary>
        internal void HandleSelfUpdate()
        {
            if (this._zeroTimeScaleTimer.TryGetUpdateDeltaTime(out float dt))
                this.HandleZeroTimeScaleUpdate(dt);
        }

        /// <summary>
        /// Drive by self MonoBehaviour FixedUpdate
        /// </summary>
        /// <param name="dt"></param>
        protected void DriveSelfFixedUpdate(float dt) => this.HandleFixedUpdate(dt);

        /// <summary>
        /// Drive by other MonoBehaviour Update
        /// </summary>
        /// <param name="dt"></param>
        public void DriveFixedUpdate(float dt) => this.HandleFixedUpdate(dt);

        /// <summary>
        /// 檢測銷毀程序
        /// </summary>
        internal protected virtual void DetectOnDestroy() { }
        #endregion

        /// <summary>
        /// 初始用
        /// </summary>
        internal abstract UniTask<bool> Init();

        /// <summary>
        /// 固定每幀被調用
        /// </summary>
        /// <param name="dt"></param>
        protected abstract void OnFixedUpdate(float dt = 0f);

        /// <summary>
        /// 開始播放
        /// </summary>
        public abstract void Play(int loops, float volume);

        /// <summary>
        /// 停止播放
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// 暫停播放
        /// </summary>
        public abstract void Pause();

        /// <summary>
        /// 返回是否播放中
        /// </summary>
        public abstract bool IsPlaying();

        /// <summary>
        /// 返回是否暫停
        /// </summary>
        /// <returns></returns>
        public abstract bool IsPaused();

        /// <summary>
        /// 返回是否循環
        /// </summary>
        /// <returns></returns>
        public abstract bool IsLooping();

        /// <summary>
        /// 處理循環程序
        /// </summary>
        internal protected void ProcessLooping(UnityEngine.Object component, float dt)
        {
            if (this.CurrentRemainingLength() > 0f)
            {
                this._currentRemainingLength -= dt;
                if (this.CurrentRemainingLength() <= 0f)
                {
                    if (this._loops >= 0)
                    {
                        switch (component)
                        {
                            case AudioSource:
                                (component as AudioSource).Stop();
                                break;
                            case VideoPlayer:
                                (component as VideoPlayer).Stop();
                                break;
                        }

                        this._loops--;
                        if (this._loops <= 0)
                        {
                            this._currentRemainingLength = 0;
                            if (this.autoEndToStop)
                                this.StopSelf();
                        }
                        else
                        {
                            switch (component)
                            {
                                case AudioSource:
                                    (component as AudioSource).Play();
                                    break;
                                case VideoPlayer:
                                    (component as VideoPlayer).Play();
                                    break;
                            }
                        }
                    }
                    this._currentRemainingLength = this.Length();
                }
            }
        }

        /// <summary>
        /// 取得影音長度 (單位：秒)
        /// </summary>
        /// <returns></returns>
        public abstract float Length();

        /// <summary>
        /// 取得當前影音長度 (單位：秒)
        /// </summary>
        /// <returns></returns>
        public abstract float CurrentLength();

        /// <summary>
        /// 取得當前影音剩餘長度 -> Remaining time (單位：秒)
        /// </summary>
        /// <returns></returns>
        public abstract float CurrentRemainingLength();

        /// <summary>
        /// 設置停止播放時的事件 Callback
        /// </summary>
        /// <param name="endEvent"></param>
        public virtual void SetEndEvent(Action endEvent)
        {
            this._endEvent = endEvent;
        }

        /// <summary>
        /// Destroy 時會被呼叫
        /// </summary>
        public virtual void OnRelease()
        {
            this._endEvent = null;
            this._mediaLength = 0f;
            this._currentRemainingLength = 0f;
        }

        /// <summary>
        /// 停止播放自己
        /// </summary>
        protected abstract void StopSelf();

        /// <summary>
        /// 重置秒數
        /// </summary>
        protected virtual void ResetLength()
        {
            this._currentRemainingLength = this._mediaLength;
        }

        /// <summary>
        /// 重置循環次數
        /// </summary>
        protected virtual void ResetLoops()
        {
            this._loops = this.loops;
        }

        /// <summary>
        /// 設置名稱
        /// </summary>
        /// <param name="assetName"></param>
        /// <param name="mediaName"></param>
        internal void SetNames(string assetName, string mediaName)
        {
            this.assetName = assetName;
            this.mediaName = mediaName;
        }

        /// <summary>
        /// 取得準備逾時的計時類型
        /// <para> ※備註：TimeScale 為 0 時 deltaTime 恆為 0 (計時器永遠不會逾時)，因此該情況改以 unscaledDeltaTime 計時，其餘情況維持以 deltaTime 計時 </para>
        /// </summary>
        /// <returns></returns>
        internal static DelayType GetPrepareTimeoutDelayType()
        {
            return (Time.timeScale == 0f) ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
        }

        #region MEDIA_URL 請求
        public const string VIDEO_URLSET = "video_urlset";
        public const string AUDIO_URLSET = "audio_urlset";
        internal static string GetValueFromUrlCfg(string urlCfg, string key)
        {
            if (string.IsNullOrEmpty(urlCfg))
                return string.Empty;

            var content = urlCfg;
            var dataMap = Saver.ParsingDataMap(content);
            dataMap.TryGetValue(key, out string value);
            return value;
        }

        /// <summary>
        /// 取得 UnityWebRequest StreamingAssets 路徑 (OSX and iOS 需要 + file://)
        /// <para> ※備註：macOS Editor 無論當前的 Build Target 為何 (ex: Android, WebGL)，皆需 + file:// </para>
        /// </summary>
        /// <returns></returns>
        internal static string GetRequestStreamingAssetsPath()
        {
#if UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_EDITOR_OSX
            return $"file://{Application.streamingAssetsPath}";
#else
            return Application.streamingAssetsPath;
#endif
        }
        #endregion
    }

    /// <summary>
    /// TimeScale 為 0 期間的刷新計時
    /// <para> ※備註：TimeScale 為 0 時不會調用 FixedUpdate，因此該期間改由 Update 以 unscaledDeltaTime 進行刷新。而恢復 TimeScale 後，首次 FixedUpdate 的 fixedUnscaledDeltaTime 會一次補上整段期間的時間，需扣除已由 Update 刷新的部分，避免重複計時 </para>
    /// </summary>
    internal struct ZeroTimeScaleTimer
    {
        /// <summary>
        /// 最後一次調用 FixedUpdate 的幀數
        /// </summary>
        private int _lastFixedUpdateFrame;

        /// <summary>
        /// TimeScale 為 0 期間已由 Update 刷新的累計時間 (尚未扣除的部分)
        /// </summary>
        private double _elapsed;

        /// <summary>
        /// 於 FixedUpdate 調用，返回不受 TimeScale 影響的 DeltaTime (TimeScale 不曾為 0 時，等同 fixedUnscaledDeltaTime)
        /// </summary>
        /// <returns></returns>
        public float GetFixedUnscaledDeltaTime()
        {
            this._lastFixedUpdateFrame = Time.frameCount;

            float fixedUnscaledDeltaTime = Time.fixedUnscaledDeltaTime;
            if (this._elapsed > 0d)
            {
                // 扣除 TimeScale 為 0 期間已由 Update 刷新的時間 (最多扣至 0)
                fixedUnscaledDeltaTime -= Mathf.Min(fixedUnscaledDeltaTime, (float)this._elapsed);
                this._elapsed = 0d;
            }

            return fixedUnscaledDeltaTime;
        }

        /// <summary>
        /// 於 Update 調用，返回是否需進行刷新 (僅 TimeScale 為 0 時才需要)，並取得該幀的 DeltaTime
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public bool TryGetUpdateDeltaTime(out float dt)
        {
            dt = 0f;

            // TimeScale 不為 0 時，維持由 FixedUpdate 進行刷新
            if (Time.timeScale != 0f)
                return false;

            // 同一幀已由 FixedUpdate 刷新過時 (表示 TimeScale 於該幀才被設置為 0)，不需重複刷新
            if (Time.frameCount == this._lastFixedUpdateFrame)
                return false;

            dt = Time.unscaledDeltaTime;
            this._elapsed += dt;

            return true;
        }
    }
}
