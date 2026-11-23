# Editor Tools — trigger them, never re-implement them

OxGFrame ships an editor tool for every artifact that follows a framework rule. Produce those artifacts by triggering the tool through an entry point the framework provides (Unity CLI command / menu item / shortcut / `public static` method). Never hand-write an "equivalent", never predict a tool's output, and never derive your own rule that merely looks compatible. If a tool cannot be triggered in the current environment, hand that step to the developer and wait — do not substitute your own output.

All C# entry points below live in auto-referenced editor assemblies: a script in a plain `Assets/**/Editor/` folder can call them directly (a script inside a custom asmdef needs a reference to the matching `OxGFrame.*.Editor` assembly).

## What must come from a tool

| Artifact | Unity CLI (`unity command ...`) | Menu item -> C# entry point | Never |
|---|---|---|---|
| `#region Binding Components` (fields + `OnAutoBind`) | `oxgframe.coreframe generate_bind_codes` | `GameObject/OxGFrame/Auto Generate Bind Codes (Shift+B)` -> `CodeBindingAutoGenerateEditor.GenerateByPrefabPath(path)` / `Execute()` | type or edit the region, guess field names |
| Stop-end marker `#` | `oxgframe.coreframe generate_stop_end` | `GameObject/OxGFrame/Auto Generate Stop End Symbol (Shift+E)` -> `StopEndAutoGenerateEditor.GenerateByPrefabPath(path)` / `Execute()` | append / move `#` by hand |
| UI / SR / CP script | `oxgframe.coreframe create_script` | `Assets/Create/OxGFrame/Core Frame/<UI\|SR\|CP> Frame/Template Scripts/...` -> `CoreFrameCreateScriptEditor.CreateScriptFromTemplate(...)` | write the class skeleton from memory |
| UI / SR / CP prefab | `oxgframe.coreframe create_prefab` | `Assets/Create/OxGFrame/Core Frame/<UI\|SR\|CP> Frame/Template Prefabs/...` -> `CoreFrameCreatePrefabEditor.CreatePrefabFromTemplate(...)` | hand-author prefab YAML |
| Game stage / stage manager script | `oxgframe.gsiframe create_script` | `Assets/Create/OxGFrame/GSI Frame/Template Scripts/...` -> `GSIFrameCreateScriptEditor.CreateScriptFromTemplate(...)` | write the skeleton from memory |
| API / Event (center) script | `oxgframe.centerframe create_script` | `Assets/Create/OxGFrame/Center Frame/<API\|Event> Center/Template Scripts/...` -> `CenterFrameCreateScriptEditor.CreateScriptFromTemplate(...)` | write the skeleton from memory |
| Audio / Video prefab | `oxgframe.mediaframe create_prefab` | `Assets/Create/OxGFrame/Media Frame/<Audio\|Video>/Template Prefabs/...`, `Assets/OxGFrame/Media Frame/Convert/...` -> `MediaFrameCreatePrefabEditor.CreatePrefabFromTemplate(...)` | hand-author prefab YAML |
| `MediaConfig.txt` | `oxgframe.mediaframe export_url_config` | `OxGFrame/MediaFrame/Media Url Config Generator (MediaConfig.txt)` -> `MediaHelper.ExportMediaUrlConfig(...)` | write the file by hand |
| `AppInfo.json` | `oxgframe.assetloader export_app_config` | `OxGFrame/AssetLoader/Export Bundle And Config Generator` -> `BundleHelper.ExportAppConfig(...)` | write by hand |
| `PatchInfo.json`, CDN output (app / DLC bundles) | `oxgframe.assetloader export_configs_and_app_bundles` / `export_app_bundles` / `export_individual_dlc_bundles` | same window -> `BundleHelper.ExportConfigsAndAppBundles(...)` / `ExportAppBundles(...)` / `ExportIndividualDlcBundles(...)` | write / copy by hand |
| `CdnConfig.dat` | `oxgframe.assetloader export_bundle_url_config` | `OxGFrame/AssetLoader/Bundle Url Config Generator (CdnConfig.dat)` -> `BundleHelper.ExportBundleUrlConfig(...)` | write by hand (it may be ciphered) |
| `HotfixManifest.dat` | `oxgframe.hotfixer export_hotfix_config` | `OxGFrame/Hotfixer/Hotfix Config Generator (HotfixManifest.dat)` -> `HotfixHelper.ExportHotfixDllConfig(...)` | write by hand (it may be ciphered) |
| AOT / hotfix dll copies in `Assets/HotfixCollector` | `oxgframe.hotfixer compile_and_copy` | `HybridCLR/OxGFrame With HybridCLR/Compile And Copy To HotfixCollector` -> `HotfixHelper.CompileAndCopyToHotfixCollector()` | copy dlls by hand |
| Renames of deprecated OxGFrame APIs after an upgrade | `oxgframe.apiupdater scan` / `apply` | `OxGFrame/Others/API Updater (Scan and update deprecated APIs)` -> `APIUpdater.Scan()` / `Apply(...)` | search-and-replace by hand |

The project's own binding rules are read with `oxgframe.coreframe get_binding_settings` (or from the `CodeBindingSettings` asset) — never assumed.

## How to trigger a tool — use the first channel that works

1. **Unity CLI** — the project contains the Pipeline package (`com.unity.pipeline` 1.x, Unity 6.0+), the `unity` CLI is installed and the project is open in the editor: run the `oxgframe.*` commands (next section).
2. **Another editor bridge** — an MCP server or any integration that can run editor C# (including `unity command script eval` when the Pipeline package is present but the `oxgframe.*` groups are not): call the path-based C# entry points (section after next). `EditorApplication.ExecuteMenuItem("<menu path>")` also works for menu items that need no naming step; a path that shows a shortcut is executed without the shortcut token, e.g. `"GameObject/OxGFrame/Auto Generate Bind Codes (Shift+B)"`.
3. **No bridge, editor open** — you cannot run editor code yourself. Prepare every input (names, files, selection target), then ask the developer to trigger the menu item / shortcut, and continue once the result is on disk. With the developer's consent you may instead add a temporary one-shot editor script (see the end of this file).
4. **Editor closed** — `Unity -batchmode -projectPath <project> -executeMethod <Class.Method> -quit -logFile <log>` with a small static wrapper that calls the path-based entry points. (A project that is open in an editor cannot be opened by a second instance.)

Blocking UI to know about:

- Every `Template ...` menu item and Unity's `[CreateAssetMenu]` items start **rename mode** in the Project window: nothing is created until the name is confirmed. The CLI commands and the path-based entry points create the file at the given path at once.
- The generator **windows** (`OxGFrame/AssetLoader/...`, `OxGFrame/Hotfixer/...`, `OxGFrame/MediaFrame/...`) keep their fields in EditorPrefs and finish with modal dialogs. Do not drive the windows — use the exporter commands / static exporters, which take the same values as parameters.
- With method type `Manual` the menu item opens the "Code Binding Clipboard" window; the CLI command and `GenerateByPrefabPath` return the content instead.
- After a framework upgrade the API Updater may show a modal prompt once in an interactive editor.

## Unity CLI commands (OxGFrame 3.7.1+)

OxGFrame registers its tools as Unity CLI commands when the project contains `com.unity.pipeline` 1.x. They compile into separate `OxGFrame.<Module>.Editor.UnityCli` assemblies that exist only while that package is installed, and each command is a thin wrapper around the same editor tool the menu item runs.

Check once per session (add `--project-path <project>` when not run from the project folder):

```
unity command oxgframe.coreframe
```

It lists the group's commands (`unity command oxgframe` lists the seven groups). An unknown group means the Pipeline package is missing or older than 1.0, the scripts have not compiled yet, or OxGFrame is older than 3.7.1 — use the next channel. Do not install the Unity CLI or the Pipeline package on your own: installing changes the project's manifest, so ask the developer first.

Argument syntax (Pipeline 1.0): `--key value` or `--key=value`; a bare `--flag` means `true`. `--format json` prints the result as JSON on stdout. Errors go to stderr and the exit code says how the command ended: `0` success, `2` invalid usage (missing or wrong argument, unknown template, existing target, overwrite without `--confirm`), `6` failed, `13` not found (no such prefab / clip). A command sent while the editor is still starting up, or while a modal dialog is open, is refused as busy — retry it. Put bind names in single quotes on the command line: they contain `*`, `$` and `[`.

| Command | Arguments | Result fields |
|---|---|---|
| `oxgframe.coreframe get_binding_settings` | — | `asset` (`null` = no settings asset yet, defaults shown), `methodType`, `method`, `variableCase`, `indicateModifier`, `headSeparator`, `tailSeparator`, `accessSeparator`, `stopEnd`, `heads[]`, `accessRules[]` (first = default), `attrRules[]`, `tailRules[]` |
| `oxgframe.coreframe create_script` | `--template ui\|sr\|cp` `--path Assets/.../Name.cs` | `path`, `template` |
| `oxgframe.coreframe create_prefab` | `--template ui\|sr\|cp_transform\|cp_rect_transform` `--path Assets/.../Name.prefab` | `path`, `template` |
| `oxgframe.coreframe generate_stop_end` | `--path <prefab asset>` | `changed`, `stopEndNodes[]`, `messages[]` |
| `oxgframe.coreframe generate_bind_codes` | `--path <prefab asset>` | `script`, `methodType`, `inserted`, `bindCount`, `usings[]`, `content`, `messages[]` |
| `oxgframe.gsiframe create_script` | `--template gsi\|gsi_manager` `--path Assets/.../Name.cs` | `path`, `template` |
| `oxgframe.centerframe create_script` | `--template event\|event_center\|api\|api_center` `--path Assets/.../Name.cs` | `path`, `template` |
| `oxgframe.mediaframe create_prefab` | `--template audio_sole\|audio_sound_effect\|video_render_texture\|video_camera` `--path Assets/.../Name.prefab` `[--clip <AudioClip / VideoClip asset>]` | `path`, `template`, `clip` |
| `oxgframe.mediaframe export_url_config` | `[--audio-url <url>]` `[--video-url <url>]` `[--output-path <folder>]` `[--confirm]` `[--dry-run]` | `file`, `overwrite`, `dryRun` |
| `oxgframe.assetloader export_app_config` | `[--product-name <name>]` `[--app-version <version>]` `[--build-target <BuildTarget>]` `[--output-path <folder>]` `[--confirm]` `[--dry-run]` | `file`, `overwrite`, `dryRun`, `productName`, `appVersion`, `platform` |
| `oxgframe.assetloader export_bundle_url_config` | `[--bundle-ip <url>]` `[--bundle-fallback-ip <url>]` `[--store-link <url>]` `[--cipher]` `[--output-path <folder>]` `[--confirm]` `[--dry-run]` | `file`, `overwrite`, `dryRun`, `cipher` |
| `oxgframe.assetloader export_configs_and_app_bundles` | `--app-packages A,B` `[--dlc-packages 'Name,version#Name,']` `[--dlc-without-platform Name,...]` `[--group-infos 'g1,t1#g2,t1,t2']` `[--product-name <name>]` `[--app-version <version>]` `[--semantic-patch]` `[--build-target <BuildTarget>]` `[--source-folder <folder>]` `[--export-folder <folder>]` `[--keep-output]` `[--confirm]` `[--dry-run]` `[--timeout <seconds>]` | `folder`, `overwrite`, `dryRun`, `keepOutput`, `sourceFolder`, `platform`, `productName`, `appVersion`, `semanticPatch`, `packages[]` (`name`, `type` = `app` / `dlc`, `version`, `dlcVersion`, `withoutPlatform`), `groups` |
| `oxgframe.assetloader export_app_bundles` | `--app-packages A,B` `[--dlc-packages 'Name,version#Name,']` `[--dlc-without-platform Name,...]` `[--product-name <name>]` `[--app-version <version>]` `[--semantic-patch]` `[--build-target <BuildTarget>]` `[--source-folder <folder>]` `[--export-folder <folder>]` `[--keep-output]` `[--confirm]` `[--dry-run]` `[--timeout <seconds>]` | `folder`, `overwrite`, `dryRun`, `keepOutput`, `sourceFolder`, `platform`, `productName`, `appVersion`, `semanticPatch`, `packages[]` |
| `oxgframe.assetloader export_individual_dlc_bundles` | `--dlc-packages 'Name,version#Name,'` `[--dlc-without-platform Name,...]` `[--product-name <name>]` `[--build-target <BuildTarget>]` `[--source-folder <folder>]` `[--export-folder <folder>]` `[--keep-output]` `[--confirm]` `[--dry-run]` `[--timeout <seconds>]` | `folder`, `overwrite`, `dryRun`, `keepOutput`, `sourceFolder`, `platform`, `productName`, `packages[]` |
| `oxgframe.hotfixer export_hotfix_config` | `--hotfix-dlls A.dll,B.dll` `[--aot-dlls X.dll,Y.dll]` `[--cipher]` `[--confirm]` `[--dry-run]` | `file`, `overwrite`, `dryRun`, `cipher`, `aotDlls[]`, `hotfixDlls[]` |
| `oxgframe.hotfixer compile_and_copy` | `[--timeout <seconds>]` | `buildTarget` |
| `oxgframe.apiupdater scan` | `[--timeout <seconds>]` | one record per match: `file`, `line`, `declaringType`, `oldApi`, `newApi`, `ready`, `reviewReason`, `text` |
| `oxgframe.apiupdater apply` | `[--confirm]` `[--dry-run]` `[--timeout <seconds>]` | `dryRun`, `selected`, `needsReview`, `applied`, `skipped`, `changedFiles` |

Rules that hold for every command:

- `create_script` / `create_prefab` never overwrite: an existing target is refused. The path must be under `Assets/` (missing folders are created) or inside an existing folder of an embedded package under `Packages/`. A created script ends up selected in the Project window, exactly as after the menu item.
- Exporters default to the `StreamingAssets` folder and to the same default values as the windows. They follow Unity's confirm / dry-run convention: `--dry-run` previews without writing; replacing an existing file needs `--confirm` (so does `apiupdater apply`, which rewrites scripts). Get the developer's consent before passing `--confirm` — these files decide what the game ships.
- The three CDN exports copy the **newest built version** of every named package from the YooAsset build output (`<source-folder>/<active build target>/<package>/<version>`) into `<export-folder>/<Root Folder Name of PatchSettings>` — the layout and the call order of the window's Process button. They do not build anything: build the bundles first (YooAsset's builder, or the BuildTool sample). Every check runs before a folder is touched: a package without a built version ends with exit code `13` and nothing is written. The output folder is **cleared first** unless `--keep-output` is passed (use it to add another platform next to an existing export), an existing output folder needs `--confirm`, and `--dry-run` lists the versions that would be exported. `--build-target` only names the platform folder (and the app config) — the bundles always come from the folder of the active build target. Quote the `--dlc-packages` and `--group-infos` values (they contain `,` and `#`); a DLC entry is always `name,version` without spaces — keep the comma and leave the version empty to name the DLC folder by the newest package version. The default timeout is 600 seconds: pass a larger `--timeout` for big packages.
- `compile_and_copy` removes files in the collector folders that are not part of the new output, and `messages[]` / stderr carry the tool's own Console output — read them instead of guessing why something was not written.
- **Wait for the compile after every command that writes a script** — `create_script`, `generate_bind_codes` when it reports `inserted = true`, and `apiupdater apply`. The editor recompiles and reloads its scripts on its own, the Pipeline server restarts during that reload, and a command that is in flight at that moment comes back empty. So send nothing else until the reload is over: (1) before the command, read `codeLoadCount` from `unity command editor status`; (2) after it, run `unity command script recompile` and poll `unity command script recompile_status` until `status` is `completed` or `up_to_date` (`failed` must be `false`); (3) after `completed`, poll `unity command editor status` until `compiling` is `false` and `codeLoadCount` is higher than in step 1 — `up_to_date` means nothing was recompiled, so there is no reload to wait for. A poll that gets no answer while the scripts reload is normal — repeat it. Only then is the new type usable (`script attach` on an uncompiled script fails and says the same).
- Generic steps (creating folders, building a hierarchy, adding components, setting serialized fields, reading the Console) are Unity's own commands: `unity command` lists the groups, `unity command <group>` lists a group.
- Change a prefab through a temporary instance, as below. Verified with Pipeline 1.0.1: `gameobject create --parent <prefab asset path>` does not add a child to the prefab (the object lands in the open scene), and `script attach --target <prefab asset path>` only changes the asset in memory until something saves it. `generate_stop_end` writes such unsaved changes of its prefab before it edits it; do not run the generators on a prefab the developer has open in Prefab Mode with unsaved changes — ask them to save first.

A new UI from the command line (verified end to end with Pipeline 1.0.1 on Unity 6000.0 and 6000.3):

```
unity command oxgframe.coreframe get_binding_settings --format json
unity command editor status                           # note codeLoadCount
unity command oxgframe.coreframe create_script --template ui --path Assets/Scripts/UI/ShopUI.cs
unity command script recompile
unity command script recompile_status                 # repeat until completed / up_to_date
unity command editor status                           # repeat until compiling is false and codeLoadCount went up
unity command oxgframe.coreframe create_prefab --template ui --path Assets/Prefabs/UI/ShopUI.prefab
# build the hierarchy on a temporary instance with Unity's own commands; node names follow get_binding_settings
unity command prefab instantiate --prefab Assets/Prefabs/UI/ShopUI.prefab            # result: hierarchyPath /ShopUI
unity command script attach --target /ShopUI --script Assets/Scripts/UI/ShopUI.cs
unity command gameobject create --name '_Node@Title*Txt' --parent /ShopUI
unity command component add --target '/ShopUI/_Node@Title*Txt' --type UnityEngine.UI.Text
unity command prefab apply_overrides --instance /ShopUI                              # writes the prefab asset
unity command gameobject delete --target /ShopUI                                     # remove the temporary instance
unity command oxgframe.coreframe generate_stop_end --path Assets/Prefabs/UI/ShopUI.prefab
unity command oxgframe.coreframe generate_bind_codes --path Assets/Prefabs/UI/ShopUI.prefab --format json
unity command script recompile
unity command script recompile_status                 # repeat until completed / up_to_date
unity command editor status                           # repeat until compiling is false and codeLoadCount went up again
# read the regenerated region; only then write logic that uses the generated fields
```

## Path-based C# entry points (OxGFrame 3.7.1+)

The same tools without `Selection`, rename mode, windows or dialogs — what the CLI commands call. Use them from any editor bridge, from `script eval`, or from a batch-mode wrapper.

| Method | Notes |
|---|---|
| `OxGFrame.CoreFrame.Editor.CodeBindingAutoGenerateEditor.GenerateByPrefabPath(string prefabPath)` | Runs the generator on the prefab root and restores the selection. Returns `GenerateResult { generated, inserted, methodType, scriptPath, bindCount, content, usingNamespaces, messages }`. |
| `OxGFrame.CoreFrame.Editor.StopEndAutoGenerateEditor.GenerateByPrefabPath(string prefabPath)` | Runs the stop-end tool on the prefab contents and saves the prefab when a name changed (unsaved changes of that prefab asset are written first). Returns `GenerateResult { generated, changed, stopEndNodes, messages }`. |
| `OxGFrame.CoreFrame.Editor.CodeBindingSettings.FindSettings(out string assetPath, out string error)` | The project's settings asset, or `null` when none exists yet (never creates one). |
| `OxGFrame.CoreFrame.Editor.CoreFrameCreateScriptEditor.CreateScriptFromTemplate(string templateName, string assetPath, out string error)` | `ui`, `sr`, `cp` |
| `OxGFrame.CoreFrame.Editor.CoreFrameCreatePrefabEditor.CreatePrefabFromTemplate(string templateName, string assetPath, out string error)` | `ui`, `sr`, `cp_transform`, `cp_rect_transform` |
| `OxGFrame.GSIFrame.Editor.GSIFrameCreateScriptEditor.CreateScriptFromTemplate(string templateName, string assetPath, out string error)` | `gsi`, `gsi_manager` |
| `OxGFrame.CenterFrame.Editor.CenterFrameCreateScriptEditor.CreateScriptFromTemplate(string templateName, string assetPath, out string error)` | `event`, `event_center`, `api`, `api_center` |
| `OxGFrame.MediaFrame.Editor.MediaFrameCreatePrefabEditor.CreatePrefabFromTemplate(string templateName, string assetPath, string clipPath, out string error)` | `audio_sole`, `audio_sound_effect`, `video_render_texture`, `video_camera`; `clipPath` `null` = no clip |

The `Create...FromTemplate` methods return `false` with the reason in `error` (also logged to the Console); each class lists its names in `TEMPLATE_NAMES`. The script methods execute Unity's own script-creation action, so the file is byte-for-byte what the menu item creates (class name = file name, root namespace handling included). The exporters and the API Updater already take plain parameters — see their sections below.

```csharp
// one call each; no selection, no rename mode, no dialog
OxGFrame.CoreFrame.Editor.CoreFrameCreateScriptEditor.CreateScriptFromTemplate("ui", "Assets/Scripts/UI/ShopUI.cs", out string scriptError);
OxGFrame.CoreFrame.Editor.CoreFrameCreatePrefabEditor.CreatePrefabFromTemplate("ui", "Assets/Prefabs/UI/ShopUI.prefab", out string prefabError);
var stopEnd = OxGFrame.CoreFrame.Editor.StopEndAutoGenerateEditor.GenerateByPrefabPath("Assets/Prefabs/UI/ShopUI.prefab");
var bind = OxGFrame.CoreFrame.Editor.CodeBindingAutoGenerateEditor.GenerateByPrefabPath("Assets/Prefabs/UI/ShopUI.prefab");
Debug.Log($"inserted={bind.inserted} script={bind.scriptPath}\n{string.Join("\n", bind.messages)}");
```

## Code binding (generator + stop end)

Naming rules, settings and semantics are in `coreframe-usage.md` (CodeBinding). Both tools take the prefab asset; the generator works on its root (the GameObject that carries the UIBase / SRBase / CPBase script) and everything below it.

Preconditions for the generator to write the script:

- The root carries the script, and the script is already compiled (create script -> wait for the compile -> attach -> generate).
- The script contains `#region Binding Components` and `#endregion` (the framework templates do). Without the markers nothing is written.
- The script file is UTF-8, and the project has exactly one `CodeBindingSettings` asset (created automatically at `Assets/CodeBindingSettings.asset` on the first run; more than one throws).
- `CodeBindingSettings.methodType` is `Auto`. With `Manual` nothing is written: the command / `GenerateByPrefabPath` return `content` and the required usings instead (the menu item shows them in the "Code Binding Clipboard" window). Replace the script's whole `#region Binding Components ... #endregion` block with `content` exactly as returned and add the listed usings — never retype or adjust it — or ask the developer to switch to `Auto`.

Reading the result:

- `inserted = true` (Console: `[Auto] Completed automatically binding content to script!!!`): the script was rewritten and the editor recompiles — wait for that compile and reload before the next command (Unity CLI: the waiting rule above). Read the regenerated region to learn the real field names before writing code that uses them. Also check `messages` for `[Warning] Found N node(s) whose bind prefix does not match the script type ...` (v3.7.1+): the listed nodes use the wrong head for this script (`_` for UI / SR, `~` for CP), their members stay `null` at runtime, so rename them and generate again.
- `inserted = false` with method type `Auto` (the command exits with an error): nothing was written. `messages` holds the generator's reason — `Cannot find script (FrameBase => UIBase, SRBase, CPBase)...` (no script on the root) or `Unable to find specific replacement string in script...` (markers missing). Fix the cause and run the tool again.
- Stop end: `stopEndNodes` lists the node that now ends the scan (`changed = false` when it was already in place). Run it before the generator when nodes after the last bind node should be skipped.

The menu methods still exist for a custom selection (scene objects, or several roots at once): select ONLY the frame root(s), then call `CodeBindingAutoGenerateEditor.Execute()` / `StopEndAutoGenerateEditor.Execute()`. The selection must not contain a node together with one of its descendants, the region is rebuilt from the current selection only, and `StopEndAutoGenerateEditor.Execute()` renames nodes without saving (open the prefab contents and save them yourself).

## Scripts from templates

| Template name | Menu item (under `Assets/Create/OxGFrame/`) | Template file (under the module's `Scripts/Editor/TplScripts/`) |
|---|---|---|
| `ui` (`oxgframe.coreframe`) | `Core Frame/UI Frame/Template Scripts/Template UI.cs (For UGUI Prefab)` | `UIFrame/TplUI.cs.txt` |
| `sr` (`oxgframe.coreframe`) | `Core Frame/SR Frame/Template Scripts/Template SR.cs (For Scene Resource Prefab)` | `SRFrame/TplSR.cs.txt` |
| `cp` (`oxgframe.coreframe`) | `Core Frame/CP Frame/Template Scripts/Template CP.cs (For Clone Prefab)` | `CPFrame/TplCP.cs.txt` |
| `gsi` (`oxgframe.gsiframe`) | `GSI Frame/Template Scripts/Template GSI.cs (Game Stage)` | `GSIFrame/TplGSI.cs.txt` |
| `gsi_manager` (`oxgframe.gsiframe`) | `GSI Frame/Template Scripts/Template GSIManager.cs (Game Stage Manager)` | `GSIFrame/TplGSIManager.cs.txt` |
| `event` (`oxgframe.centerframe`) | `Center Frame/Event Center/Template Scripts/Template Event.cs (Event)` | `EventCenter/TplEvent.cs.txt` |
| `event_center` (`oxgframe.centerframe`) | `Center Frame/Event Center/Template Scripts/Template EventCenter.cs (Event Center Manager)` | `EventCenter/TplEventCenter.cs.txt` |
| `api` (`oxgframe.centerframe`) | `Center Frame/API Center/Template Scripts/Template API.cs (API)` | `APICenter/TplAPI.cs.txt` |
| `api_center` (`oxgframe.centerframe`) | `Center Frame/API Center/Template Scripts/Template APICenter.cs (API Center Manager)` | `APICenter/TplAPICenter.cs.txt` |

The menu items create `NewTplXX.cs` in rename mode (the developer confirms the name); `create_script` / `CreateScriptFromTemplate` create the same content at the path you pass. If you cannot run editor code at all and the developer prefers not to press the menu, copy the template file's text verbatim (never from memory) and apply exactly what Unity applies: `#SCRIPTNAME#` -> the class name (= file name); the `#ROOTNAMESPACEBEGIN#` / `#ROOTNAMESPACEEND#` lines are removed when no root namespace applies to the target folder (neither its assembly definition nor the project's editor settings define one), otherwise the class is wrapped in `namespace <Root> { ... }` and the lines in between are indented by 4 spaces.

## Prefabs from templates

| Template name | Menu item (under `Assets/Create/OxGFrame/`) | Root it creates |
|---|---|---|
| `ui` (`oxgframe.coreframe`) | `Core Frame/UI Frame/Template Prefabs/Template UI (UGUI Prefab)` | `RectTransform` (stretched) + `Canvas` + `GraphicRaycaster`, calibrated for `UIBase` |
| `sr` (`oxgframe.coreframe`) | `Core Frame/SR Frame/Template Prefabs/Template SR (Scene Resource Prefab)` | plain `Transform` |
| `cp_transform` (`oxgframe.coreframe`) | `Core Frame/CP Frame/Template Prefabs/Template CP (Transform Prefab)` | plain `Transform` |
| `cp_rect_transform` (`oxgframe.coreframe`) | `Core Frame/CP Frame/Template Prefabs/Template CP (RectTransform Prefab)` | `RectTransform` (128x128) + `CanvasRenderer` |
| `audio_sole` (`oxgframe.mediaframe`) | `Media Frame/Audio/Template Prefabs/Template Audio Sole (Sole Audio Prefab)` | `AudioBase`: Sole, `loops = -1`, destroy + unload on stop |
| `audio_sound_effect` (`oxgframe.mediaframe`) | `Media Frame/Audio/Template Prefabs/Template Audio SoundEffect (SoundEffect Audio Prefab)` | `AudioBase`: SoundEffect, `loops = 0`, destroy on stop, keep asset |
| `video_render_texture` (`oxgframe.mediaframe`) | `Media Frame/Video/Template Prefabs/Template Video RenderTexture (RenderTexture Video Prefab)` | `VideoBase`: RenderTexture mode |
| `video_camera` (`oxgframe.mediaframe`) | `Media Frame/Video/Template Prefabs/Template Video Camera (Camera Video Prefab)` | `VideoBase`: Camera mode |

The menu items create `NewTplXX` in the folder of `Selection.activeObject` in rename mode; `create_prefab` / `CreatePrefabFromTemplate` create the same prefab at the path you pass. The Core Frame templates attach **no** script: add the UI / SR / CP component to the root afterwards and fill its settings (`uiSettings.canvasName`, ...).

Media prefabs with a clip: pass `--clip` / `clipPath` and the prefab is set up like the Convert menu does (clip assigned, source type set, `audioLength` filled in for audio). The Convert menu itself (`Assets/OxGFrame/Media Frame/Convert/Convert to ...` -> `MediaFrameCreatePrefabEditor.ConvertToAudioSole()`, `ConvertToAudioSoundEffect()`, `ConvertToVideoRenderTexture()`, `ConvertToVideoCamera()`) works on the selected clips and writes one prefab per clip, named after the clip, next to the active selection.

## Config exporters (use these instead of the windows)

```
unity command oxgframe.mediaframe export_url_config --audio-url http://host/audio/ --video-url http://host/video/ --dry-run
unity command oxgframe.assetloader export_bundle_url_config --bundle-ip http://host --bundle-fallback-ip http://host2 --store-link http://store --cipher
unity command oxgframe.assetloader export_app_config --product-name MyGame --app-version 1.0.0
unity command oxgframe.hotfixer export_hotfix_config --hotfix-dlls MyGame.Hotfix.Runtime.dll --aot-dlls mscorlib.dll,UniTask.dll --cipher
unity command oxgframe.assetloader export_configs_and_app_bundles --app-packages DefaultPackage,HotfixPackage --dlc-packages 'Dlc1Package,1.0#Dlc2Package,' --app-version 1.0.0 --dry-run
```

```csharp
// MediaConfig.txt (audio_urlset / video_urlset); refresh the AssetDatabase afterwards.
MediaHelper.ExportMediaUrlConfig("http://host/audio/", "http://host/video/", Application.streamingAssetsPath);

// CdnConfig.dat (bundle_ip / bundle_fallback_ip / store_link); cipher: true writes the ciphered form.
BundleHelper.ExportBundleUrlConfig(bundleIp, bundleFallbackIp, storeLink, Application.streamingAssetsPath, cipher: false);

// AppInfo.json (4th argument true = use the active build target)
BundleHelper.ExportAppConfig(productName, appVersion, Application.streamingAssetsPath, true, EditorUserBuildSettings.activeBuildTarget);

// HotfixManifest.dat -> Assets/StreamingAssets (names with the .dll extension)
HotfixHelper.ExportHotfixDllConfig(new List<string> { "mscorlib.dll", "UniTask.dll" }, new List<string> { "MyGame.Hotfix.Runtime.dll" }, cipher: true);
```

- The C# exporters overwrite silently; the commands refuse to replace an existing file without `--confirm`.
- CDN output: prefer the commands (`export_configs_and_app_bundles`, `export_app_bundles`, `export_individual_dlc_bundles`) — they check everything before a folder is touched. The C# exporters behind them (`BundleHelper.ExportConfigsAndAppBundles(...)`, `ExportAppBundles(...)`, `ExportIndividualDlcBundles(...)`) do no such checks: they need YooAsset bundles already built, **delete the output folder first** when `isClearOutputPath` is true, and throw halfway when the source folder or a version part is missing. Before calling them yourself, confirm the paths with the developer and check the packages with `BundleHelper.FindNewestPackageVersions(inputPath, packageNames, out string inputPathWithPlatform)` (package name -> newest built version; a package without one is left out). Read the signatures in `AssetLoader/Scripts/Editor/Bundle/BundleHelper.cs`.
- File names come from `PatchSettings` / `HotfixSettings` (defaults `AppInfo.json`, `PatchInfo.json`, `CdnConfig.dat`, `HotfixManifest.dat`); read the project's settings asset if one exists under a `Resources` folder.
- `compile_and_copy` / `HotfixHelper.CompileAndCopyToHotfixCollector()` compiles the hotfix dlls and copies AOT + hotfix dlls into `Assets/HotfixCollector/AOTDlls` and `HotfixDlls` as `.dll.bytes`. With HybridCLR 9.x run `HybridCLR/Generate/All` first (see `hotfix-flow.md`).
- BuildTool sample (only when imported): `OxGFrame.Extensions.BuildTool.Editor.BuildTool.BuildBundles(...)` / `BuildApp()` are the batch-mode entry points.

## Settings assets

| Asset | How it is found | How to create |
|---|---|---|
| `CodeBindingSettings` | the single asset of that type anywhere in the project | created automatically on the first generator run, or `Assets/Create/OxGFrame/Create Settings/Create Code Binding Settings` |
| `CryptogramSettings` | the single asset of that type | created automatically when first needed, or `.../Create Cryptogram Settings`; edit through `YooAsset/OxGFrame Cryptogram Settings With YooAsset` |
| `PatchSettings` | `Resources/PatchSettings` (built-in defaults when absent) | `Assets/Create/OxGFrame/Create Settings/Create Patch Settings in Resources` (into `<selected folder>/Resources/`) |
| `HotfixSettings` | `Resources/HotfixSettings` (built-in defaults when absent) | `Assets/Create/OxGFrame/Create Settings/Create Hotfix Settings in Resources` |

Read a settings asset before relying on its defaults (`get_binding_settings` does that for the binding rules); change it only when the developer asks.

## API Updater

```
unity command oxgframe.apiupdater scan --format json
unity command oxgframe.apiupdater apply --dry-run
unity command oxgframe.apiupdater apply --confirm
```

```csharp
var result = OxGFrame.Common.Editor.APIUpdater.Scan();          // read result.matches
var applied = OxGFrame.Common.Editor.APIUpdater.Apply(result.matches);   // applies only matches with selected == true
```

`Scan()` pre-selects the matches that need no review (`ready = true` in the command's records); matches that need review are never applied by `apply`. Show the developer what will change and get consent before applying (the window does the same).

## One-shot editor script (optional, only with the developer's consent)

When there is no editor bridge and the developer prefers automation over pressing the shortcut, a temporary script can run a tool after the next compile:

```csharp
// Assets/Editor/OxGFrameAgentTask.cs — delete this file as soon as the Console shows the result.
using UnityEditor;
using UnityEngine;

public static class OxGFrameAgentTask
{
    [InitializeOnLoadMethod]
    private static void Run()
    {
        const string key = "OxGFrameAgentTask.ShopUI.Bind";      // unique per task: runs once per editor session
        if (SessionState.GetBool(key, false)) return;
        SessionState.SetBool(key, true);

        EditorApplication.delayCall += () =>
        {
            var result = OxGFrame.CoreFrame.Editor.CodeBindingAutoGenerateEditor.GenerateByPrefabPath("Assets/.../ShopUI.prefab");
            Debug.Log($"[OxGFrameAgentTask] inserted={result.inserted}\n{string.Join("\n", result.messages)}");
        };
    }
}
```

It runs when Unity next compiles (the developer has to bring the editor to the foreground). Always remove the script afterwards, and never leave more than one such script in the project.
