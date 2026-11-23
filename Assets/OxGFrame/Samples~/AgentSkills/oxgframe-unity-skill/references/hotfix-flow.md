# Hotfixer + HybridCLR Flow

Namespace `OxGFrame.Hotfixer`. Hotfixer downloads and loads HybridCLR hotfix assemblies (plus AOT metadata) from a bundle package, then hands control to the hotfix code.

## Assembly split (mandatory)

- **Main (AOT) assembly** — ships with the player. Contains boot scene logic, the hotfix check, splash/patch UI. Must NOT reference the hotfix assembly (enter hotfix code via a loaded scene, `AddComponent` from a hotfix-collected prefab, or reflection).
- **Hotfix assembly** — compiled to DLL, collected as a bundle asset, downloaded and `Assembly.Load`ed at runtime.

HotfixerDemo layout: `Scripts/Main/` (`HotfixerDemo.Main.Runtime.asmdef`) and `Scripts/Hotfix/` (`HotfixerDemo.Hotfix.Runtime.asmdef`), scenes `Scenes/Main/` + `Scenes/Hotfix/`. Collector groups for the hotfix package: the hotfix scene, `Assets/HotfixCollector/AOTDlls`, `Assets/HotfixCollector/HotfixDlls`, plus any hotfix prefabs.

## CheckHotfix

```csharp
// Explicit lists:
Hotfixers.CheckHotfix(
    "HotfixPackage",
    new string[] { "mscorlib.dll", "UniTask.dll" },              // AOT metadata assemblies
    new string[] { "HotfixerDemo.Hotfix.Runtime.dll" });         // hotfix assemblies

// Or config-driven (reads HotfixManifest.dat from StreamingAssets):
Hotfixers.CheckHotfix("HotfixPackage", () =>
{
    Debug.LogWarning("Generate HotfixManifest.dat via MenuItem OxGFrame/Hotfixer/Hotfix Config Generator (HotfixManifest.dat).");
});
```

`HotfixManifest.dat` (name and cipher come from `HotfixSettings`) is produced by the framework exporter — the window above or `HotfixHelper.ExportHotfixDllConfig(aotDlls, hotfixDlls, cipher)` — never written by hand (`editor-tools.md`).

Gate on the patcher first (`while (!AssetPatcher.IsInitialized()) yield return null;`), register hotfix event listeners BEFORE calling `CheckHotfix`, then poll:

```csharp
if (Hotfixers.IsDone() && !this._isLoaded)
{
    this._isLoaded = true;
    CoreFrames.USFrame.LoadSingleSceneAsync("HotfixPackage", "HotfixMain").Forget();
}
```

Facade summary:

```csharp
Hotfixers.IsDone();          // all dlls downloaded + loaded
Hotfixers.IsDisabled();      // true when OXGFRAME_HYBRIDCLR_DISABLED define is set
Hotfixers.Reset();           // allow CheckHotfix to run again
Hotfixers.GetHotfixAssembly("Name.dll");                 // reflection entry from Main
Hotfixers.GetHotfixAssemblyNamesWithoutExtensions();
Hotfixers.GetAOTAssemblyNames();
```

## Hotfix FSM + events

States: `FsmHotfixPrepare → FsmInitHotfixPackage → FsmUpdateHotfixPackage → FsmHotfixCreateDownloader (pauses when downloads exist) → FsmHotfixBeginDownload → FsmHotfixDownloadOver → FsmHotfixClearCache → FsmLoadAOTAssemblies → FsmLoadHotfixAssemblies → FsmHotfixDone`.

```csharp
private EventGroup _hotfixEvents = new EventGroup();
this._hotfixEvents.AddListener<HotfixEvents.HotfixFsmState>(this._OnHandleEventMessage);
this._hotfixEvents.AddListener<HotfixEvents.HotfixInitFailed>(this._OnHandleEventMessage);
this._hotfixEvents.AddListener<HotfixEvents.HotfixUpdateFailed>(this._OnHandleEventMessage);
this._hotfixEvents.AddListener<HotfixEvents.HotfixCreateDownloader>(this._OnHandleEventMessage);   // totalCount/totalBytes -> confirm UI
this._hotfixEvents.AddListener<HotfixEvents.HotfixDownloadProgression>(this._OnHandleEventMessage);
this._hotfixEvents.AddListener<HotfixEvents.HotfixDownloadFailed>(this._OnHandleEventMessage);

// user events (continue / retry):
HotfixUserEvents.UserBeginDownload.SendEventMessage();
HotfixUserEvents.UserTryInitHotfix.SendEventMessage();
HotfixUserEvents.UserTryUpdateHotfix.SendEventMessage();
HotfixUserEvents.UserTryCreateDownloader.SendEventMessage();
```

## HybridCLR integration details

- `FsmLoadAOTAssemblies` loads each AOT dll (TextAsset bytes) via `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet)`.
- `FsmLoadHotfixAssemblies` does `Assembly.Load(bytes)` and registers the assembly with the manager.
- In Editor / `EditorSimulateMode` / when `OXGFRAME_HYBRIDCLR_DISABLED` is defined, DLL loading is skipped and assemblies resolve from `AppDomain.CurrentDomain` (everything is AOT-compiled) — the rest of the flow still runs, so the same game code works with hotfix on or off.
- `HotfixDllPlan.json` feeds the Hotfix Config Generator window:

```json
[ { "planName": "Plan", "aotDlls": [ "mscorlib.dll", "UniTask.dll" ], "hotfixDlls": [ "MyGame.Hotfix.Runtime.dll" ] } ]
```

## HybridCLR versions and the dll copy step

- HybridCLR 8.x and 9.x are both supported (verified 8.12.0 and 9.1.0). The framework's editor code switches with the `HYBRIDCLR_9_OR_NEWER` version define; the runtime API used by the hotfix flow is the same in both. HybridCLR 9.x does not support Unity 2021 — stay on 8.x there.
- `HybridCLR/OxGFrame With HybridCLR/Compile And Copy To HotfixCollector` (`HotfixHelper.CompileAndCopyToHotfixCollector()`) compiles the hotfix dlls and copies the AOT + hotfix dlls into `Assets/HotfixCollector/AOTDlls` / `HotfixDlls` as `.dll.bytes`. Use it instead of copying files by hand.
- Which AOT dlls are copied: on 8.x the `Patch AOT Assemblies` list of the HybridCLR settings; on 9.x that setting no longer exists, so the list is read from the generated `AOTGenericReferences.cs` (`PatchedAOTAssemblyList`) — run `HybridCLR/Generate/All` first.
- `Assets/OxGFrame/Hotfixer/Convert HotfixManifest.dat (BYTES [Cipher] <-> JSON [Plaintext])` toggles the selected config file between its ciphered and plaintext forms.

## Boot order recipe (Main scene)

1. `PatchLauncher` prefab in the boot scene; wait `AssetPatcher.IsInitialized()`.
2. Register hotfix events; `Hotfixers.CheckHotfix("HotfixPackage", ...)`.
3. Show splash/progress UI while waiting; drive retries via `HotfixUserEvents`.
4. On `Hotfixers.IsDone()`, load the hotfix main scene from the hotfix package.
5. Hotfix scene's driver starts the runtime stage manager (GSIFrame) — patch remaining packages with `AssetPatcher.Check()`, then enter the game.

## IL2CPP / stripping notes

- Keep `link.xml` covering types used only via reflection from hotfix code.
- AOT generics: run HybridCLR generation (`HybridCLR/Generate/All`) before building; keep the AOT dlls passed to `CheckHotfix` / listed in `HotfixManifest.dat` in sync with the dlls copied into `HotfixCollector/AOTDlls`.
- Do not place hotfix-only `MonoBehaviour`s in Main scenes; hotfix scenes/prefabs live in the hotfix package.
