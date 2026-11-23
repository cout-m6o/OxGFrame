#if YOOASSET_3
using System.Collections.Generic;
#endif
using YooAsset;

namespace OxGFrame.AssetLoader.Bundle
{
#if YOOASSET_2
    public class HostServers : IRemoteServices
    {
        private readonly string _hostServer;
        private readonly string _fallbackHostServer;

        public HostServers(string hostServer, string fallbackHostServer)
        {
            this._hostServer = hostServer;
            // 未提供備援位址時以主位址替代，避免組出無效的備援 URL
            this._fallbackHostServer = string.IsNullOrEmpty(fallbackHostServer) ? hostServer : fallbackHostServer;
        }

        public string GetRemoteMainURL(string fileName)
        {
            // 使用 URL 組合 (Path.Combine 於 Windows 會產生反斜線)
            return BundleConfig.CombineUrl(this._hostServer, fileName);
        }

        public string GetRemoteFallbackURL(string fileName)
        {
            return BundleConfig.CombineUrl(this._fallbackHostServer, fileName);
        }
    }
#elif YOOASSET_3
    public class HostServers : IRemoteService
    {
        private readonly string _hostServer;
        private readonly string _fallbackHostServer;

        public HostServers(string hostServer, string fallbackHostServer)
        {
            this._hostServer = hostServer;
            // 未提供備援位址時以主位址替代，避免組出無效的備援 URL
            this._fallbackHostServer = string.IsNullOrEmpty(fallbackHostServer) ? hostServer : fallbackHostServer;
        }

        /// <summary>
        /// [YooAsset v3] 依優先序回傳遠端候選位址 (主位址 -> 備援位址)
        /// </summary>
        public IReadOnlyList<string> GetRemoteUrls(string fileName)
        {
            // 使用 URL 組合 (Path.Combine 於 Windows 會產生反斜線)
            return new List<string>()
            {
                BundleConfig.CombineUrl(this._hostServer, fileName),
                BundleConfig.CombineUrl(this._fallbackHostServer, fileName)
            };
        }
    }
#endif
}
