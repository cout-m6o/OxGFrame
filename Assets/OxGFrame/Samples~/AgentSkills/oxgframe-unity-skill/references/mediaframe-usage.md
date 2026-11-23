# MediaFrame Usage (MediaFrames.AudioFrame / VideoFrame)

Namespace `OxGFrame.MediaFrame`. Audio/Video are loaded as **prefabs** carrying `AudioBase` / `VideoBase` components. Create those prefabs with the framework tools, never by hand: template prefabs under `Assets/Create/OxGFrame/Media Frame/...`, or select clips and run the converters under `Assets/OxGFrame/Media Frame/Convert/...` (one prefab per clip; see `editor-tools.md`). The same `res#` / bundle address rules apply.

Scene requirements: `MediaFrames.AudioFrame` mixer APIs need the `AudioManager` prefab (with its `MasterMixer`) in the scene; `VideoFrame` needs nothing extra. Call `InitInstance()` in a boot `Awake()`.

## Shared API shape (AudioFrame shown; VideoFrame identical with `VideoBase`/`VideoClip`)

```csharp
MediaFrames.AudioFrame.InitInstance();

await MediaFrames.AudioFrame.Preload(assetName);            // + packageName / string[] overloads

// Play(assetName, Transform parent = null, int loops = 0, float volume = 0f)
//   loops: 0 = play once, -1 = infinite loop
//   volume: > 0 overrides the prefab's setting, 0 keeps it
var audioBase = await MediaFrames.AudioFrame.Play(AudioPrefs.a101);
var withClip  = await MediaFrames.AudioFrame.Play(AudioPrefs.a201, this.clips[0]);   // supply a source clip at runtime

MediaFrames.AudioFrame.Stop(assetName);
MediaFrames.AudioFrame.Stop(assetName, disableEndEvent: false, forceDestroy: true);
MediaFrames.AudioFrame.Pause(assetName);
MediaFrames.AudioFrame.PauseAll();
MediaFrames.AudioFrame.ResumeAll();          // no single-asset Resume; re-Play a paused instance instead
MediaFrames.AudioFrame.StopAll(false, true);
MediaFrames.AudioFrame.ForceUnload(assetName);   // when the prefab's onDestroyAndUnload is unchecked

var ab = MediaFrames.AudioFrame.GetComponent<AudioBase>(assetName);
```

## Audio specifics

Demo path convention mirrors `SoundType` categories (drives auto-parenting under `[AudioManager]`):
`res#Audio/a_BGM/101`, `res#Audio/b_General/201`, `res#Audio/c_Interact/301`, `res#Audio/d_Voice/401`, `res#Audio/e_Atmosphere/501`.

`AudioBase` prefab fields: `audioType { soundType, priority }`, `sourceType` (Audio | StreamingAssets | Url), `audioClip`, `urlSet`, `loops (-1 = infinite)`, `ignoreTimeScale`, `onStopAndDestroy`, `onDestroyAndUnload`, `autoEndToStop`, mixer group assignment (`_mixerName`, `_mixerGroupName`).

`SoundType.Sole` allows one playing instance per asset (a second `Play` while it plays returns the same instance); `SoundType.SoundEffect` creates a new instance per `Play` and lets them overlap.

## Time scale and pausing

The framework tracks each instance's remaining play time to detect the end of playback, count `loops` and apply `autoEndToStop`. `ignoreTimeScale` (default on) selects the clock of that tracker — it does not change how Unity plays the media.

- `ignoreTimeScale` on: the tracker follows real (unscaled) time. This matches Unity's own playback: an `AudioSource` ignores `Time.timeScale`, and so does a `VideoPlayer` with its default `timeUpdateMode` (`UnscaledGameTime`).
- `ignoreTimeScale` off: the tracker follows scaled game time. Use it only when playback really follows the time scale (e.g. a `VideoPlayer` set to `GameTime`, or audio whose `pitch` you scale yourself).
- `Time.timeScale = 0` (pause menus), v3.7.1+: media can be prepared and started while paused; instances with `ignoreTimeScale` keep ending / looping normally during the pause, the others stay frozen until the time scale is restored.
- `AudioListener.pause`, v3.7.1+ (at any time scale): the tracker waits while the listener pause freezes the media — audio (unless the source has `ignoreListenerPause`) and a `VideoPlayer` in `DSPTime` mode — so they keep their remaining length and end normally after the listener resumes. Earlier versions kept counting and ended them early.
- While the time scale is 0 the tracker also waits for a `VideoPlayer` in `GameTime` mode, which Unity does not advance then (the `timeUpdateMode` checks need Unity 2022.2+).
- To pause framework-managed media deliberately, prefer `MediaFrames.AudioFrame.Pause(...)` / `PauseAll()` and `ResumeAll()` (same for `VideoFrame`): they also update the framework's own paused state.
- Scene-placed `monoDrive` media is tracked only when `OXGFRAME_AUDIOFRAME_MONODRIVE_FIXEDUPDATE_ON` / `OXGFRAME_VIDEOFRAME_MONODRIVE_FIXEDUPDATE_ON` is defined (or the owner calls `DriveFixedUpdate(dt)`).

### Mixer control (needs AudioManager prefab)

```csharp
var mixer = MediaFrames.AudioFrame.GetMixerByName("MasterMixer");
MediaFrames.AudioFrame.SetMixerExposedParam(mixer, "Master_Vol", vol);   // auto-records for restore
MediaFrames.AudioFrame.AutoClearMixerExposedParams(mixer);
MediaFrames.AudioFrame.AutoRestoreMixerExposedParams(mixer);
MediaFrames.AudioFrame.SetMixerSnapshot(mixer, "Paused");                // "Normal", "Reverb_Light", "Reverb_Heavy"
MediaFrames.AudioFrame.SetMixerTransitionToSnapshot(mixer,
    new[] { MediaFrames.AudioFrame.GetMixerSnapshot(mixer, "Reverb_Light"),
            MediaFrames.AudioFrame.GetMixerSnapshot(mixer, "Reverb_Heavy") },
    new float[] { 1 - val, val });
```

Sample `MasterMixer` exposed params: `Master_Vol`, `BGM_Vol`, `General_Vol`, `Interact_Vol`, `Voice_Vol`, `Atmosphere_Vol`. There is no separate master-volume API — volume control goes through mixer exposed params.

## Video specifics

`VideoBase` prefab render modes: **RenderTexture** (draw into UI via `RawImage`) or **Camera** (direct overlay).

```csharp
// Camera render mode: just play
await MediaFrames.VideoFrame.Play(VideoPrefs.VideoCamExample, this.clips[0]);

// RenderTexture mode: grab the RT and wire the end event
var video = await MediaFrames.VideoFrame.Play(VideoPrefs.VideoRtExample, this.clips[1]);
if (video != null)
{
    this.rawImage.enabled = true;
    this.rawImage.texture = video.GetTargetRenderTexture();
    video.SetEndEvent(() => { this.rawImage.texture = null; this.rawImage.enabled = false; });
}

MediaFrames.VideoFrame.Stop(VideoPrefs.VideoRtExample, false, true);

// VideoBase instance controls
float progress = video.CurrentLength() / video.Length();
video.SkipToPercent(progress + 5f / video.Length());
video.SetPlaySpeed(1.25f);        // recommended: 0.25/0.5/1.0/1.25/1.5/1.75/2.0
```

`VideoBase` sources: `Video` (VideoClip), `StreamingAssets` (`fullPathName` relative to StreamingAssets, with extension), `Url` (UrlSet; either a full URL or a path resolved from the media URL config). Prefab fields: `renderMode`, `_renderTextureSize`, `_targetCamera { camera, orderType, alpha }`, `_aspectRatio`, `waitForFirstFrame`, `playbackSpeed`, `maxPrepareTimeSeconds`, `loops`, `onStopAndDestroy`, `onDestroyAndUnload`.

## Editor preview

`AudioBase` and `VideoBase` inspectors provide Play/Pause/Stop buttons with a seek bar and timecode (works for all source types, including StreamingAssets/Url, without entering Play Mode). The `AudioBase` inspector's **Preload** button stores the clip length in `audioLength` (needed on WebGL, where a requested clip reports no length); the clip converters fill it in automatically. Video preview renders the picture (and audio track) inside the Inspector. If an `http://` source is blocked, the editor offers to switch Player Settings "Allow downloads over HTTP" to "Only in development builds".

## Release rules

- Stop audio/video in stage `OnExit` / UI `OnRelease` (`StopAll()` on scene transitions).
- `onStopAndDestroy` destroys the media object on stop; `onDestroyAndUnload` also unloads the asset — when unchecked, call `ForceUnload(assetName)` yourself.
- WebGL: browser autoplay policies require a user gesture; `SourceType.Audio`/`Video` clip assignment is not supported on WebGL for some formats — prefer StreamingAssets/Url there; always stop videos on UI close to release render textures.

## URL config (for SourceType.Url)

`MediaConfig.MEDIA_URL_CFG_NAME` (`MediaConfig.txt`, in StreamingAssets) holds the `audio_urlset` / `video_urlset` base URLs. Generate it with the framework exporter — `OxGFrame.MediaFrame.Editor.MediaHelper.ExportMediaUrlConfig(audioUrlset, videoUrlset, outputPath)`, or the window `OxGFrame/MediaFrame/Media Url Config Generator (MediaConfig.txt)` — not by hand (see `editor-tools.md`). `UrlSet.getUrlPathFromCfg` toggles between cfg-based base URL + relative path, or a full URL in `url` directly.
