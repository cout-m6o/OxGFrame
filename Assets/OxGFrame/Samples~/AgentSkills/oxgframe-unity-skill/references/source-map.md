# OxGFrame Source Map

Use this map to locate and re-verify APIs before applying this skill to a project. Written against OxGFrame v3.7.1 (Unity 2021.3 – 6.x).

## Official sources

- Repository: `https://github.com/cout-m6o/OxGFrame`
- Docs: `https://oxgframe.ouhiyo.com/`
- Coding style wiki: `https://github.com/cout-m6o/OxGFrame/wiki/Coding-Style`

## Package location in a target project

The package id is `com.michaelo.oxgframe`. Depending on how it is installed, the source lives at:

- UPM (git): `Library/PackageCache/com.michaelo.oxgframe@<hash>/`
- Embedded: `Packages/OxGFrame/` or directly under `Assets/OxGFrame/` (framework development repo)

Locate it with:

```
# PowerShell
Get-ChildItem Library/PackageCache -Directory | Where-Object { $_.Name -like "com.michaelo.oxgframe*" }
# or search for the facade
rg -l "static class AssetLoaders" --type cs
```

## Key runtime files per module

| Module | Facade / key files |
|---|---|
| AssetLoader | `AssetLoader/Scripts/Runtime/AssetLoaders.cs` (load facade), `AssetPatcher.cs` (patch facade), `Bundle/BundleConfig.cs`, `Bundle/PatchLauncher.cs`, `Bundle/PackageOperation.cs`, `Bundle/PatchEvent/PatchEvents.cs` + `PatchUserEvents.cs` + `PackageEvents.cs`, `Bundle/PatchFsm/PatchFsmStates.cs`, `Bundle/PlayMode/PlayModeParameters.cs`, `Utility/BundleUtility.cs` |
| CoreFrame | `CoreFrame/Scripts/Runtime/Core/CoreFrames.cs` (facade: UIFrame/SRFrame/USFrame/CPFrame), `Core/Implement/FrameBase.cs` + `FrameManager.cs`, `Core/UIFrame/UIBase.cs` + `UIManager.cs` + `UIConfig/UIConfig.cs`, `Core/SRFrame/SRBase.cs`, `Core/CPFrame/CPBase.cs`, `Core/USFrame/USManager.cs`, `Core/Common/Binder.cs` + `FrameConfig.cs` (binding symbols) |
| MediaFrame | `MediaFrame/Scripts/Runtime/Core/MediaFrames.cs` (facade: AudioFrame/VideoFrame), `Core/AudioFrame/AudioBase.cs` + `AudioManager.cs`, `Core/VideoFrame/VideoBase.cs` + `VideoManager.cs`, `Core/Implement/MediaBase.cs` |
| GSIFrame | `GSIFrame/Scripts/Runtime/GSIManagerBase.cs`, `GSIBase.cs` |
| NetFrame | `NetFrame/Scripts/Runtime/NetFrames.cs` (facade), `Core/NetManager.cs`, `Core/Implement/NetNode/NetNode.cs`, `NetOption/*.cs`, providers: `WebSocketNetProvider.cs`, `TcpNetProvider.cs`, `KcpNetProvider.cs`, `Core/Interface/INetTips.cs` |
| CenterFrame | `CenterFrame/Scripts/Runtime/Common/CenterBase.cs`, `APICenter/APIBase.cs` + `Acax.cs` (Http), `EventCenter/EventBase.cs` |
| Hotfixer | `Hotfixer/Scripts/Runtime/Hotfixers.cs` (facade), `Hotfix/HotfixManager.cs`, `Hotfix/HotfixFsm/HotfixFsmStates.cs`, `HotfixEvent/HotfixEvents.cs` + `HotfixUserEvents.cs` |

## Editor entry points

Agents must trigger these tools instead of hand-writing their output — commands, methods, preconditions and trigger recipes are in `editor-tools.md`.

Automation surface (3.7.1+):

- Unity CLI commands — `<Module>/Scripts/Editor/UnityCli/*CliCommands.cs`, one assembly per module (`OxGFrame.<Module>.Editor.UnityCli`: AssetLoader, CenterFrame, Common, CoreFrame, GSIFrame, Hotfixer, MediaFrame). They compile only while `com.unity.pipeline` 1.x is installed (version define `UNITY_PIPELINE_1`), register the groups `oxgframe.coreframe`, `oxgframe.mediaframe`, `oxgframe.assetloader`, `oxgframe.hotfixer`, `oxgframe.gsiframe`, `oxgframe.centerframe`, `oxgframe.apiupdater`, and only forward to the editor tools below.
- Path-based entry points in the regular editor assemblies (every supported Unity version) — the `#region Automation` blocks of `CodeBindingAutoGenerateEditor.cs`, `StopEndAutoGenerateEditor.cs`, `CoreFrameCreateScriptEditor.cs`, `CoreFrameCreatePrefabEditor.cs`, `GSIFrameCreateScriptEditor.cs`, `CenterFrameCreateScriptEditor.cs`, `MediaFrameCreatePrefabEditor.cs`, plus `CodeBindingSettings.FindSettings` and `BundleHelper.FindNewestPackageVersions` (the pre-check of the CDN export commands).

Menu overview:

- `GameObject/OxGFrame/Auto Generate Bind Codes (Shift+B)` — CodeBinding generator (`CoreFrame/Scripts/Editor/CodeBindingAutoGenerateEditor.cs`; rules in `CodeBindingSettings.cs`, runtime side in `Core/Common/Binder.cs` + `FrameConfig.cs`).
- `GameObject/OxGFrame/Auto Generate Stop End Symbol (Shift+E)` — places the `#` stop-end marker (`StopEndAutoGenerateEditor.cs`).
- `Assets/Create/OxGFrame/Core Frame/...` — UI/SR/CP template scripts and prefabs (templates in `CoreFrame/Scripts/Editor/TplScripts/`).
- `Assets/Create/OxGFrame/GSI Frame/...` — game stage / stage manager template scripts.
- `Assets/Create/OxGFrame/Center Frame/...` — API/Event center template scripts.
- `Assets/Create/OxGFrame/Media Frame/...` — Audio/Video template prefabs; `Assets/OxGFrame/Media Frame/Convert/...` — clip-to-prefab converters (asset context menu).
- `Assets/Create/OxGFrame/Create Settings/...` — `Create Code Binding Settings`, `Create Cryptogram Settings`, `Create API Update Rules Settings`, `Create Patch Settings in Resources`, `Create Hotfix Settings in Resources`.
- `OxGFrame/AssetLoader/...` — `Export Bundle And Config Generator`, `Bundle Url Config Generator (CdnConfig.dat)`, `Bundle Cryptogram Utility (For Verify)`, sandbox download-directory helpers; `YooAsset/OxGFrame Cryptogram Settings With YooAsset`.
- `OxGFrame/MediaFrame/Media Url Config Generator (MediaConfig.txt)`.
- `OxGFrame/Hotfixer/Hotfix Config Generator (HotfixManifest.dat)`; `HybridCLR/OxGFrame With HybridCLR/Compile And Copy To HotfixCollector`; `Assets/OxGFrame/Hotfixer/Convert HotfixManifest.dat (BYTES [Cipher] <-> JSON [Plaintext])`.
- `OxGFrame/Others/API Updater (Scan and update deprecated APIs)` — scans the project for deprecated OxGFrame APIs and applies reviewed renames; ships in the `Common` module (`Common/Scripts/Editor/APIUpdater`, editor-only, auto-included as a dependency of every module).
- `OxGFrame/Extensions/BuildTool/Build Bundles by Bundle Map JSON` — only when the BuildTool sample is imported.

## Verification commands

```
# Find every public facade method quickly
rg -n "public static (async )?(UniTask|void|bool|string|T |U )" <pkg>/CoreFrame/Scripts/Runtime/Core/CoreFrames.cs

# How does the target project use OxGFrame today?
rg -n "CoreFrames\.(UIFrame|SRFrame|USFrame|CPFrame)|MediaFrames\.(AudioFrame|VideoFrame)|AssetLoaders\.|AssetPatcher\.|Hotfixers\." Assets -g "*.cs"

# Address constants / prefixes in use
rg -n "res#|build#|public const string|public static readonly string" Assets -g "*.cs" | rg -v Packages

# Binding conventions present in prefabs/scenes
rg -n "_Node@|~Node@" Assets

# Which play mode & packages the project ships
rg -n "playMode|listAppPackages|listDlcPackages" Assets -g "*.prefab" -g "*.unity"
```

## Dependencies the framework assumes

`com.cysharp.unitask` (UniTask), `com.tuyoogame.yooasset` (YooAsset, external package), `com.code-philosophy.hybridclr` (Hotfixer), OxGKit modules (`com.michaelo.oxgkit.loggingsystem`, `.saversystem`, `.timesystem`, `.utilities`), LWMyBox; bundled third-party code: UniFramework (Event/Machine/Reference), CSChaCha20, XXTEA, SimpleDiskUtils, kcp2k, Telepathy, UnityWebSocket. `com.michaelo.oxgkit.buttonsystem` is optional (only the default `BtnPlus` binding tail refers to it). AssetLoader (and several samples) use Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`); the samples additionally use the Unity Input System.

Version support (check the installed package versions before using version-specific APIs):

- **Unity** 2021.3 minimum; verified on 2022.3 and 6.3.
- **YooAsset** 2.x and 3.x are both supported; the framework switches code with the `YOOASSET_2` / `YOOASSET_3` version defines. Verified: 2.3.18, 2.3.19, 3.0.3-beta, 3.0.4 – 3.0.7 (3.0.5+ recommended). Bundles built by v2 and v3 are not interchangeable — rebuild after switching.
- **HybridCLR** 8.x and 9.x are both supported (`HYBRIDCLR_9_OR_NEWER`); verified 8.12.0 and 9.1.0. HybridCLR 9.x does not support Unity 2021 (stay on 8.x there).
- These defines are declared per assembly definition (versionDefines) and exist only in the framework's own assemblies. Project code that needs them must add the same versionDefines to its own asmdef — code without an asmdef (`Assembly-CSharp`) never receives them.
