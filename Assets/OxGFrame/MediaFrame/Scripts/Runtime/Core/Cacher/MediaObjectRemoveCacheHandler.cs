using OxGFrame.MediaFrame.AudioFrame;
using OxGFrame.MediaFrame.VideoFrame;
using OxGKit.Utilities.Cacher;
using System;

namespace OxGFrame.MediaFrame.Cacher
{
    public class MediaObjectRemoveCacheHandler : IRemoveCacheHandler<string, string>
    {
        public void RemoveCache(string key, string value)
        {
            // 僅在該資源已無任何實例時才進行卸載
            // (仍有實例時略過，避免 ForceUnload 連帶停止並銷毀所有正在播放的影音，待該資源下次播放時會重新加入 LRU)
            if (key.StartsWith(nameof(AudioBase) + "_", StringComparison.Ordinal))
            {
                var audioManager = AudioManager.GetInstance();
                if (audioManager != null &&
                    audioManager.GetMediaComponents<AudioBase>(value).Length == 0)
                    audioManager.ForceUnload(value);
            }
            else if (key.StartsWith(nameof(VideoBase) + "_", StringComparison.Ordinal))
            {
                var videoManager = VideoManager.GetInstance();
                if (videoManager != null &&
                    videoManager.GetMediaComponents<VideoBase>(value).Length == 0)
                    videoManager.ForceUnload(value);
            }
        }
    }
}