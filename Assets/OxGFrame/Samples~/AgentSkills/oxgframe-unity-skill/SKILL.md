---
name: oxgframe-unity-skill
description: Use when developing, reviewing, or migrating Unity projects that use OxGFrame, including AssetLoaders asset/scene/raw-file loading, AssetPatcher patch and download flow, PatchLauncher play modes, CoreFrames UI/SR/US/CP loading and lifecycle, UIBase/SRBase/CPBase prefab CodeBinding (_Node@ / ~Node@), the framework's own editor tools (bind-code generator, script/prefab templates, config exporters) that must be triggered — through the Unity CLI commands `unity command oxgframe.*`, the editor methods or the menu items — instead of hand-writing their output, MediaFrames audio/video playback, GSIFrame game stage flow, NetFrame websocket/tcp/kcp clients, CenterFrame API/Event centers, Hotfixer HybridCLR hotfix bootstrap, and WebGL/IL2CPP concerns. Do not use for YooAsset build-pipeline internals unless explicitly requested.
---

# OxGFrame Unity Skill

## Purpose

Make the agent behave like an experienced OxGFrame engineer. OxGFrame is a modular Unity game framework (UniTask-based, YooAsset-backed asset management, HybridCLR hotfix support, multi-platform incl. WebGL).

Verified scope:

- `AssetLoaders` address-based asset, raw-file, and scene loading; group (`groupId`) cachers.
- `AssetPatcher` patch flow, play modes, packages (App/DLC), downloaders, `PackageOperation`.
- `CoreFrames.UIFrame` / `SRFrame` / `USFrame` / `CPFrame` and the `UIBase` / `SRBase` / `CPBase` lifecycle.
- Prefab CodeBinding workflow: `_Node@` / `~Node@` naming grammar, `CodeBindingSettings`, the bind-code generator and the stop-end tool.
- Editor tooling: every Unity CLI command (`unity command oxgframe.*`), menu item and public editor method that generates scripts, prefabs, binding code and config files.
- `MediaFrames.AudioFrame` / `VideoFrame`.
- `GSIFrame` stage manager, `NetFrame` network nodes, `CenterFrame` API/Event centers, `Hotfixers` HybridCLR flow.

The bundled references are grounded in the framework source (v3.7.1) and the 16 official Samples — treat them as the canonical usage patterns. Supported dependency ranges: Unity 2021.3 – 6.x, YooAsset 2.x and 3.x, HybridCLR 8.x and 9.x (9.x does not support Unity 2021).

## First response rules

1. Reply in the user's language; keep code and identifiers in English.
2. Start with the conclusion, then implementation details.
3. Coding style is a per-team decision — ASK before applying. Before writing the first code for a project, ask the developer whether project code should follow the OxGFrame coding style (`references/coding-style.md`) or the team's own conventions, then apply that choice consistently (persist it to project memory/context when available). The OxGFrame style is only mandatory for code inside the OxGFrame package itself (framework contributions and extensions); application/business-logic code belongs to the team's own conventions, and the project's existing style wins by default. Tool-generated code is outside this choice: it follows the project's `CodeBindingSettings` and is never restyled by hand.
4. Before changing code, inspect the target project's real definitions (address constants, package names, existing UI/SR/CP classes). Do not invent package names or asset addresses.
5. If an API is not listed in these references or the target project uses a different OxGFrame version, verify against the installed package source first and say so.
6. For WebGL/IL2CPP targets, call out async, stripping/link.xml, autoplay/media, and download constraints when relevant.

## Tool-first rule (mandatory)

OxGFrame already ships an editor tool for everything that follows a framework rule. Work the way a developer does: prepare the inputs, then trigger the framework's tool — never hand-write what a tool produces and never derive your own rule that merely looks compatible.

- **Binding code** (`#region Binding Components`, `OnAutoBind`) comes only from **Auto Generate Bind Codes**. Name the GameObjects with the framework grammar and the project's `CodeBindingSettings`, run the generator, then read what it wrote. Never type, edit or "fix up" the region, and never invent another binding scheme.
- **Stop-end markers** come only from **Auto Generate Stop End Symbol**.
- **Scripts and prefabs** (UI / SR / CP, game stages, API / Event centers, audio / video prefabs) come from the framework templates and converters.
- **Config files** (`AppInfo.json`, `PatchInfo.json`, `CdnConfig.dat`, `HotfixManifest.dat`, `MediaConfig.txt`) come from the framework exporters.
- **Deprecated-API renames** after an upgrade come from the API Updater.
- **The project's binding rules** (tails, access rules, attributes, casing, method type) are read from the project (`get_binding_settings` / the `CodeBindingSettings` asset), never assumed from the defaults.

How to trigger, in order of preference: (1) the **Unity CLI** — `unity command oxgframe.<module> <name> ...` — when the project has Unity's Pipeline package (`com.unity.pipeline` 1.x, Unity 6.0+); (2) the path-based static editor methods through any other editor bridge or batch mode; (3) the developer pressing the menu item. Never install the Unity CLI or the Pipeline package without the developer's consent. With the Unity CLI, every command that writes a script is followed by a compile and a script reload: wait for both (`script recompile`, poll `script recompile_status`, then `editor status` — the exact rule is in `references/editor-tools.md`) before sending the next command.

`references/editor-tools.md` lists every tool with its Unity CLI command, menu path, public method, preconditions and how to trigger it. If you cannot trigger a tool in the current environment, say so and hand that single step to the developer — do not replace it with your own output.

## Reference loading

Load only what the task needs:

- `references/editor-tools.md` — the framework's editor tools and how an agent triggers them (Unity CLI commands, path-based methods, menu items); read it before creating any script, prefab, binding code or config file.
- `references/source-map.md` — where every module lives in the package; verification commands.
- `references/samples-guide.md` — what each of the 16 official Samples demonstrates; scene setup requirements.
- `references/assetloader-usage.md` — AssetLoaders, AssetPatcher, patch events, PackageOperation (DLC), play modes, decryption.
- `references/coreframe-usage.md` — CoreFrames UI/SR/US/CP APIs, lifecycle tables, the CodeBinding grammar and workflow, UI stack/reverse/mask/freeze.
- `references/mediaframe-usage.md` — MediaFrames Audio/Video APIs, mixer control, media prefab setup.
- `references/gsi-net-center-usage.md` — GSIFrame stages, NetFrame clients, CenterFrame API/Event centers.
- `references/hotfix-flow.md` — Hotfixers + HybridCLR flow, main/hotfix assembly split.
- `references/production-architecture.md` — optional production patterns: boot stage chain, package split strategy, release checklists.
- `references/coding-style.md` — naming, class layout, async and lifecycle rules (OxGFrame framework standard; confirm with the developer before applying it to project code — see First response rules).

## Core conventions (always apply)

- **Address prefixes**: `res#Path/Name` loads from `Resources`; `build#SceneName` (USFrame only) loads a Build Settings scene; no prefix loads from the bundle package (YooAsset address — with the default `AddressByFileName` rule this is the bare file name without extension, e.g. `model_01`, `HotfixMain`).
- **Constants over literals**: keep asset names in static constant classes with an optional `groupId` (see `samples-guide.md`); never scatter address strings through gameplay code.
- **Scene requirements**: anything bundle-driven needs the `PatchLauncher` prefab in the boot scene and must gate on `AssetPatcher.IsInitialized()`. Mixer APIs need the `AudioManager` prefab. UIFrame needs `UICanvas` objects whose names match each UI prefab's `canvasName`.
- **`InitInstance()`**: call `CoreFrames.UIFrame/SRFrame/USFrame/CPFrame.InitInstance()` and `MediaFrames.AudioFrame/VideoFrame.InitInstance()` once in a boot `Awake()` for efficiency.
- **Package-name overloads**: every load API has a `(assetName, ...)` form that uses the default package and a `(packageName, assetName, ...)` form; be explicit about packages in multi-package projects.

## OxGKit APIs used by OxGFrame (brief)

OxGFrame depends on several OxGKit utility modules — you will meet these APIs when reading or extending the framework, and they are safe to use in projects that have OxGFrame installed:

- `OxGKit.LoggingSystem` — every module logs through per-module `Logging.Print<Logger>/PrintInfo/PrintWarning/PrintError/PrintException`; define `OXGKIT_LOGGER_ON` to enable logging in player builds.
- `OxGKit.SaverSystem` — `Saver` (PlayerPrefs-backed) persists patch preferences; `Saver.ParsingDataMap(text)` parses `key value` config text (used by media URL configs).
- `OxGKit.TimeSystem` — `RTUpdater` drives NetFrame's update loop; `RealTimer`/`DeltaTimer` for timing.
- `OxGKit.Utilities` — `Requester` (audio/texture/text web requests with LRU/ARC caching, used by MediaFrame), `UnityMainThread`, `DontDestroy`, `EasyAnim`.
- `OxGKit.ButtonSystem` — optional (not a framework dependency): `ButtonPlus` is the target of the default CodeBinding tail `BtnPlus`, so use that tail only when the package is installed.

This skill only covers what OxGFrame itself uses. Dedicated skills for the individual OxGKit systems (input, localization, cursor, pools, actions, tweens, infinite scroll, singletons, ...) are provided separately by the OxGKit project — use those for deep OxGKit work.

## Non-negotiable patterns

- Do not call YooAsset directly for game feature code when OxGFrame provides a wrapper (`AssetLoaders`, `CoreFrames`, `MediaFrames`).
- Implement with the highest-level API available: `CoreFrames.UIFrame.Show` for UI, `SRFrame.Show` for scene resources, `USFrame.Load*SceneAsync` for Unity scenes, `CPFrame.LoadWithClone(Async)` for clone prefabs, `MediaFrames` for audio/video; use `AssetLoaders` only when no frame helper fits.
- Never hand-write or edit generated artifacts (binding region, stop-end markers, template skeletons, exported config files) — trigger the framework tool (Tool-first rule).
- Component references live in the generated `OnAutoBind()`; register one-time events in `OnBind()`; consume `Show(..., data)` payloads in `OnShow(object)` — never in `OnPreShow()`; refresh visual/data state in `OnShow` / `OnReceiveAndRefresh`; release listeners in `OnRelease()`.
- `OnShowAnimation` / `OnCloseAnimation` overrides MUST invoke `animationEnd()` or the UI never unfreezes.
- Use `CloseSelf()` when a UI closes itself from its own callback; use `CoreFrames.UIFrame.Close(assetName)` for external close flows. `Close(assetName, disableOnPreClose, forceDestroy)` — `disableOnPreClose` skips `OnPreClose()`, `forceDestroy` destroys and unloads.
- `Hide` keeps the instance and can be `Reveal`ed; `Close` runs the close flow. Do not `Reveal` a closed UI.
- `CPBase` has a reduced lifecycle (`OnShow()` with no data parameter; several UI/SR hooks are `[Obsolete]` no-ops on CP) and unloads its own asset reference on destroy.
- Pair every manual `AssetLoaders.LoadAssetAsync` with `UnloadAsset(assetName)` (or use `groupId` + `UnloadAssets(groupId)` for lists); stop audio/video and close UI/SR in stage `OnExit`.
- Register patch/hotfix event listeners BEFORE starting the flow (`AssetPatcher.Check()`, `packageOperation.Ready()`, `Hotfixers.CheckHotfix(...)`).
- Do not block stage transitions with synchronous loads on WebGL; prefer the async APIs everywhere.
- Do not put hotfix-only runtime code in the main (AOT) assembly; the main assembly must not reference the hotfix assembly (use reflection or config-driven entry).

## Output shape

For implementation guidance:

1. Conclusion.
2. Exact code or patch-ready snippets — for hand-written logic only. For anything a framework tool generates, name the tool, its inputs (target path, GameObject names, selection) and the expected result instead of pasting generated output.
3. Where the files/classes/prefabs belong.
4. Lifecycle/release notes.
5. Verification steps (compile, play-mode path to exercise).

For reviews, lead with findings and cite file/line references from the target project.
