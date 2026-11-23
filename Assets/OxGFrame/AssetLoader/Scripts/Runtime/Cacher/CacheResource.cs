using Cysharp.Threading.Tasks;
using OxGKit.LoggingSystem;
using System.Linq;
using UnityEngine;

namespace OxGFrame.AssetLoader.Cacher
{
    internal class CacheResource : AssetCache<ResourcePack>
    {
        /// <summary>
        /// 批次卸載深度 (大於 0 時，暫緩調用 Resources.UnloadUnusedAssets)
        /// </summary>
        private int _batchUnloadDepth = 0;

        /// <summary>
        /// 批次卸載期間，是否有資源等待調用底層接口釋放
        /// </summary>
        private bool _pendingUnloadUnusedAssets = false;

        private static CacheResource _instance = null;
        public static CacheResource GetInstance()
        {
            if (_instance == null)
                _instance = new CacheResource();
            return _instance;
        }

        #region Batch Unload
        /// <summary>
        /// 開始批次卸載
        /// <para> Resources.UnloadUnusedAssets 為全域掃描，逐一調用的成本極高，因此批次期間合併為結束時的單次調用 </para>
        /// </summary>
        internal void BeginBatchUnload()
        {
            this._batchUnloadDepth++;
        }

        /// <summary>
        /// 結束批次卸載 (批次期間若有資源被實際卸載，於此統一調用一次底層接口釋放)
        /// </summary>
        internal void EndBatchUnload()
        {
            if (this._batchUnloadDepth > 0)
                this._batchUnloadDepth--;

            if (this._batchUnloadDepth == 0 && this._pendingUnloadUnusedAssets)
            {
                this._pendingUnloadUnusedAssets = false;
                Resources.UnloadUnusedAssets();
            }
        }
        #endregion

        public override bool HasInCache(string assetName)
        {
            return this._cacher.ContainsKey(assetName);
        }

        public override ResourcePack GetFromCache(string assetName)
        {
            // 僅需查詢一次
            this._cacher.TryGetValue(assetName, out ResourcePack pack);
            return pack;
        }

        #region Asset
        public async UniTask PreloadAssetAsync<T>(string[] assetNames, Progression progression, byte maxRetryCount) where T : Object
        {
            if (assetNames == null || assetNames.Length == 0)
                return;

            // 先初始加載進度
            this.currentCount = 0;
            this.totalCount = assetNames.Length;

            for (int i = 0; i < assetNames.Length; i++)
            {
                var assetName = assetNames[i];

                if (string.IsNullOrEmpty(assetName))
                    continue;

                if (this.GetRetryCounter(assetName) != null &&
                    this.GetRetryCounter(assetName).IsOutOfRetries())
                {
                    this.StopRetryCounter(assetName);
                    Logging.PrintWarning<Logger>($"Asset: {assetName} Load failed and cannot retry anymore!!! Please to check asset is existing.");
                    continue;
                }

                // 檢查是否有進行中的載入任務，如果有就等待它完成
                if (this.TryGetLoadingTask(assetName, out var existingTask))
                {
                    Logging.Print<Logger>($"Asset: {assetName} is loading, waiting for existing task...");

                    try
                    {
                        // 進行中的任務可能是預載任務 (非泛型) 或型別化的載入任務 (泛型)，後者無法轉型，改為等待其結束
                        if (existingTask is UniTaskCompletionSource source)
                            await source.Task;
                        else
                            await this.WaitForLoadingTask(assetName);

                        // 等待完成後更新進度
                        this.currentCount++;
                        progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);
                    }
                    catch (System.Exception ex)
                    {
                        Logging.PrintError<Logger>($"【Preload】Asset: {assetName} failed: {ex.Message}");
                    }
                    continue;
                }

                // 如果有在緩存中就不進行預加載
                if (this.HasInCache(assetName))
                {
                    this.currentCount++;
                    progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);
                    Logging.PrintWarning<Logger>($"【Preload】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: [{assetName}] already preloaded!!!");
                    continue;
                }

                // 建立載入任務並加入緩存
                var completionSource = new UniTaskCompletionSource();
                this.TryAddLoadingTask(assetName, completionSource);

                try
                {
                    await this._PreloadAssetCoreAsync<T>(assetName, progression, maxRetryCount);
                    completionSource.TrySetResult();

                    // 載入完成後更新進度
                    this.currentCount++;
                    progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);
                }
                catch (System.Exception ex)
                {
                    completionSource.TrySetException(ex);
                    Logging.PrintError<Logger>($"【Preload】Asset: {assetName} failed: {ex.Message}");
                }
                finally
                {
                    // 任務完成後移除
                    this.TryRemoveLoadingTask(assetName);
                    this.ProcessPendingUnloads(assetName, ProcessType.Asset, this._UnloadAssetCore);
                }
            }
        }

        /// <summary>
        /// 實際的預載入邏輯
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <param name="progression"></param>
        /// <param name="maxRetryCount"></param>
        /// <returns></returns>
        private async UniTask _PreloadAssetCoreAsync<T>(string assetName, Progression progression, byte maxRetryCount) where T : Object
        {
            bool loaded = false;
            ResourcePack pack = new ResourcePack();

            var req = Resources.LoadAsync<T>(assetName);
            if (req != null)
            {
                // 注意：這裡不更新 currentCount，因為進度由外層控制
                do
                {
                    if (req.isDone)
                    {
                        // 確定資源是否存在
                        if (req.asset == null)
                            break;

                        loaded = true;
                        pack.SetPack(assetName, req.asset);
                        break;
                    }
                    await UniTask.Yield();
                } while (true);
            }

            if (loaded)
            {
                // skipping duplicate keys
                if (!this.HasInCache(assetName))
                {
                    this._cacher.Add(assetName, pack);
                    Logging.Print<Logger>($"【Preload】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: {assetName}");
                }
            }
            else
            {
                // Retry 邏輯
                if (this.GetRetryCounter(assetName) == null)
                {
                    this.StartRetryCounter(assetName, maxRetryCount);
                    Logging.Print<Logger>($"【Preload】 => << {nameof(CacheResource)} >> Asset: {assetName} start doing retry. Max retry count: {maxRetryCount}");
                }
                else
                {
                    Logging.Print<Logger>($"【Preload】 => << {nameof(CacheResource)} >> Asset: {assetName} doing retry. Remaining retry count: {this.GetRetryCounter(assetName).retryCount}, Max retry count: {maxRetryCount}");
                }

                this.GetRetryCounter(assetName).DelRetryCount();
                this.TryRemoveLoadingTask(assetName);
                // 巢狀重試會重置進度計數，因此不傳入 progression 並於結束後還原，交由最外層統一更新進度 (避免批次預載的進度被覆寫)
                float currentCount = this.currentCount;
                float totalCount = this.totalCount;
                await this.PreloadAssetAsync<T>(new string[] { assetName }, null, maxRetryCount);
                this.currentCount = currentCount;
                this.totalCount = totalCount;
            }
        }

        public void PreloadAsset<T>(string[] assetNames, Progression progression, byte maxRetryCount) where T : Object
        {
            if (assetNames == null || assetNames.Length == 0)
                return;

            // 先初始加載進度
            this.currentCount = 0;
            this.totalCount = assetNames.Length;

            for (int i = 0; i < assetNames.Length; i++)
            {
                var assetName = assetNames[i];

                if (string.IsNullOrEmpty(assetName))
                    continue;

                if (this.GetRetryCounter(assetName) != null &&
                    this.GetRetryCounter(assetName).IsOutOfRetries())
                {
                    this.StopRetryCounter(assetName);
                    Logging.PrintWarning<Logger>($"Asset: {assetName} Load failed and cannot retry anymore!!! Please to check asset is existing.");
                    continue;
                }

                // 如果有在緩存中就不進行預加載
                if (this.HasInCache(assetName))
                {
                    this.currentCount++;
                    progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);
                    Logging.PrintWarning<Logger>($"【Preload】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: [{assetName}] already preloaded!!!");
                    continue;
                }

                bool loaded = false;
                ResourcePack pack = new ResourcePack();

                var asset = Resources.Load<T>(assetName);
                if (asset != null)
                {
                    this.currentCount++;
                    progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);

                    loaded = true;
                    pack.SetPack(assetName, asset);
                }

                if (loaded)
                {
                    // skipping duplicate keys
                    if (!this.HasInCache(assetName))
                    {
                        this._cacher.Add(assetName, pack);
                        Logging.Print<Logger>($"【Preload】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: {assetName}");
                    }
                }
                else
                {
                    if (this.GetRetryCounter(assetName) == null)
                    {
                        this.StartRetryCounter(assetName, maxRetryCount);
                        Logging.Print<Logger>($"【Preload】 => << {nameof(CacheResource)} >> Asset: {assetName} start doing retry. Max retry count: {maxRetryCount}");
                    }
                    else
                    {
                        Logging.Print<Logger>($"【Preload】 => << {nameof(CacheResource)} >> Asset: {assetName} doing retry. Remaining retry count: {this.GetRetryCounter(assetName).retryCount}, Max retry count: {maxRetryCount}");
                    }

                    this.GetRetryCounter(assetName).DelRetryCount();
                    // 巢狀重試會重置進度計數，因此不傳入 progression 並於結束後還原，再由此處統一更新進度 (避免批次預載的進度被覆寫)
                    float currentCount = this.currentCount;
                    float totalCount = this.totalCount;
                    this.PreloadAsset<T>(new string[] { assetName }, null, maxRetryCount);
                    this.currentCount = currentCount + 1;
                    this.totalCount = totalCount;
                    progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);
                    continue;
                }
            }
        }

        public async UniTask<T> LoadAssetAsync<T>(string assetName, Progression progression, byte maxRetryCount) where T : Object
        {
            if (string.IsNullOrEmpty(assetName))
                return null;

            if (this.GetRetryCounter(assetName) != null &&
                this.GetRetryCounter(assetName).IsOutOfRetries())
            {
                this.StopRetryCounter(assetName);
                Logging.PrintWarning<Logger>($"Asset: {assetName} Load failed and cannot retry anymore!!! Please to check asset is existing.");
                return null;
            }

            ResourcePack pack = null;

            // 檢查是否有進行中的載入任務
            while (this.TryGetLoadingTask(assetName, out var existingTask))
            {
                Logging.Print<Logger>($"Asset: {assetName} is loading, waiting for existing task...");
                if (existingTask is UniTaskCompletionSource<T> source)
                {
                    var asset = await source.Task;
                    if (asset != null)
                    {
                        pack = this.GetFromCache(assetName);
                        if (pack != null)
                        {
                            pack.AddRef();
                            Logging.Print<Logger>($"【Load Shared】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: {assetName}, ref: {pack.refCount}");
                        }
                    }
                    return asset;
                }

                // 進行中的任務型別不同 (ex: 預載任務或以不同型別載入)，無法共用其結果，等待其結束後改由緩存流程取得
                await this.WaitForLoadingTask(assetName);
            }

            // 先從緩存拿
            pack = this.GetFromCache(assetName);
            if (pack != null)
            {
                // 快取命中，直接返回
                this.currentCount = this.totalCount = 1;
                progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);

                var cachedAsset = pack.GetAsset<T>();
                if (cachedAsset != null)
                {
                    pack.AddRef();
                    Logging.Print<Logger>($"【Load】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: {assetName}, ref: {pack.refCount}");
                }
                return cachedAsset;
            }

            // 建立載入任務並加入緩存
            var completionSource = new UniTaskCompletionSource<T>();
            this.TryAddLoadingTask(assetName, completionSource);

            try
            {
                var result = await this._LoadAssetCoreAsync<T>(assetName, progression, maxRetryCount);
                completionSource.TrySetResult(result);
                return result;
            }
            catch (System.Exception ex)
            {
                completionSource.TrySetException(ex);
                throw;
            }
            finally
            {
                // 任務完成後移除
                this.TryRemoveLoadingTask(assetName);
                this.ProcessPendingUnloads(assetName, ProcessType.Asset, this._UnloadAssetCore);
            }
        }

        /// <summary>
        /// 實際的資產載入邏輯
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <param name="progression"></param>
        /// <param name="maxRetryCount"></param>
        /// <returns></returns>
        private async UniTask<T> _LoadAssetCoreAsync<T>(string assetName, Progression progression, byte maxRetryCount) where T : Object
        {
            // 初始加載進度
            this.currentCount = 0;
            this.totalCount = 1;

            bool loaded = false;
            ResourcePack pack = new ResourcePack();

            var req = Resources.LoadAsync<T>(assetName);
            if (req != null)
            {
                float lastCount = 0;
                do
                {
                    this.currentCount += (req.progress - lastCount);
                    lastCount = req.progress;
                    progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);

                    if (req.isDone)
                    {
                        // 確定資源是否存在
                        if (req.asset == null)
                            break;

                        loaded = true;
                        pack.SetPack(assetName, req.asset);
                        break;
                    }
                    await UniTask.Yield();
                } while (true);
            }

            if (loaded)
            {
                // skipping duplicate keys
                if (!this.HasInCache(assetName))
                    this._cacher.Add(assetName, pack);
            }

            var asset = pack.GetAsset<T>();

            if (asset != null)
            {
                // 引用計數++
                pack.AddRef();
                Logging.Print<Logger>($"【Load】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: {assetName}, ref: {pack.refCount}");
                return asset;
            }
            else
            {
                // Retry 邏輯
                if (this.GetRetryCounter(assetName) == null)
                {
                    this.StartRetryCounter(assetName, maxRetryCount);
                    Logging.Print<Logger>($"【Load】 => << {nameof(CacheResource)} >> Asset: {assetName} start doing retry. Max retry count: {maxRetryCount}");
                }
                else
                {
                    Logging.Print<Logger>($"【Load】 => << {nameof(CacheResource)} >> Asset: {assetName} doing retry. Remaining retry count: {this.GetRetryCounter(assetName).retryCount}, Max retry count: {maxRetryCount}");
                }

                this.GetRetryCounter(assetName).DelRetryCount();
                this.TryRemoveLoadingTask(assetName);
                return await this.LoadAssetAsync<T>(assetName, progression, maxRetryCount);
            }
        }

        public T LoadAsset<T>(string assetName, Progression progression, byte maxRetryCount) where T : Object
        {
            if (string.IsNullOrEmpty(assetName))
                return null;

            if (this.GetRetryCounter(assetName) != null &&
                this.GetRetryCounter(assetName).IsOutOfRetries())
            {
                this.StopRetryCounter(assetName);
                Logging.PrintWarning<Logger>($"Asset: {assetName} Load failed and cannot retry anymore!!! Please to check asset is existing.");
                return null;
            }

            // 初始加載進度
            this.currentCount = 0;
            this.totalCount = 1;

            // 先從緩存拿
            ResourcePack pack = this.GetFromCache(assetName);
            T asset = default;

            if (pack == null)
            {
                bool loaded = false;
                pack = new ResourcePack();

                asset = Resources.Load<T>(assetName);
                if (asset != null)
                {
                    this.currentCount = this.totalCount;
                    progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);

                    loaded = true;
                    pack.SetPack(assetName, asset);
                }

                if (loaded)
                {
                    // skipping duplicate keys
                    if (!this.HasInCache(assetName))
                        this._cacher.Add(assetName, pack);
                }
            }
            else
            {
                this.currentCount = this.totalCount;
                progression?.Invoke(this.currentCount / this.totalCount, this.currentCount, this.totalCount);
            }

            asset = pack.GetAsset<T>();
            if (asset != null)
            {
                // 引用計數++
                pack.AddRef();
                Logging.Print<Logger>($"【Load】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: {assetName}, ref: {pack.refCount}");
            }
            else
            {
                if (this.GetRetryCounter(assetName) == null)
                {
                    this.StartRetryCounter(assetName, maxRetryCount);
                    Logging.Print<Logger>($"【Load】 => << {nameof(CacheResource)} >> Asset: {assetName} start doing retry. Max retry count: {maxRetryCount}");
                }
                else
                {
                    Logging.Print<Logger>($"【Load】 => << {nameof(CacheResource)} >> Asset: {assetName} doing retry. Remaining retry count: {this.GetRetryCounter(assetName).retryCount}, Max retry count: {maxRetryCount}");
                }

                this.GetRetryCounter(assetName).DelRetryCount();
                return this.LoadAsset<T>(assetName, progression, maxRetryCount);
            }

            return asset;
        }

        public void UnloadAsset(string assetName, bool forceUnload)
        {
            this.CheckUnload(assetName, forceUnload, ProcessType.Asset, this._UnloadAssetCore);
        }

        private void _UnloadAssetCore(string assetName, bool forceUnload, ProcessType processType)
        {
            if (!this.HasInCache(assetName))
            {
                Logging.PrintWarning<Logger>($"【Unload】 Asset: {assetName} not in cache, skipping.");
                return;
            }

            ResourcePack pack = this.GetFromCache(assetName);

            // 標記為正在卸載
            this.AddUnloadingFlag(assetName);

            try
            {
                // 引用計數--
                pack.DelRef();
                Logging.Print<Logger>($"【Unload】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: {assetName}, ref: {pack.refCount}");

                // 判斷是否需要實際卸載
                bool shouldUnload = forceUnload || pack.IsReleasable();

                if (shouldUnload)
                {
                    this._cacher[assetName] = null;
                    this._cacher.Remove(assetName);

                    // 批次卸載期間先行標記，待批次結束時統一調用一次
                    if (this._batchUnloadDepth > 0)
                        this._pendingUnloadUnusedAssets = true;
                    else
                        Resources.UnloadUnusedAssets();

                    Logging.Print<Logger>($"【Unload Completes】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}, asset: {assetName}");
                }
            }
            finally
            {
                // 移除卸載標記
                this.RemoveUnloadingFlag(assetName);
            }
        }

        public void ReleaseAssets()
        {
            if (this.count == 0)
                return;

            // 強制釋放緩存與資源 (批次處理，避免每個資源都觸發一次 Resources.UnloadUnusedAssets)
            this.BeginBatchUnload();
            try
            {
                foreach (var assetName in this._cacher.Keys.ToArray())
                {
                    if (this.HasInCache(assetName))
                    {
                        this.UnloadAsset(assetName, true);
                    }
                }
            }
            finally
            {
                // 結尾會統一調用一次底層接口釋放，因此無需再由批次結束時觸發
                this._pendingUnloadUnusedAssets = false;
                this.EndBatchUnload();
            }

            this._cacher.Clear();
            Resources.UnloadUnusedAssets();

            Logging.Print<Logger>($"【Release All】 => Current << {nameof(CacheResource)} >> Cache Count: {this.count}");
        }
        #endregion
    }
}