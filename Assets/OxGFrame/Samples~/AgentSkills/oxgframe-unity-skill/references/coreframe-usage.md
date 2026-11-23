# CoreFrame Usage (CoreFrames.UIFrame / SRFrame / USFrame / CPFrame)

Namespace `OxGFrame.CoreFrame`. Facade `CoreFrames` with four nested static classes. Group id default is `0`. All Show/Preload APIs accept `res#` (Resources) or bundle addresses, and have `(packageName, assetName, ...)` overloads.

## UIFrame

```csharp
CoreFrames.UIFrame.InitInstance();                         // boot Awake (recommended)
bool ok = CoreFrames.UIFrame.SetupAndCheckUICanvas(canvasName);

// Preload
await CoreFrames.UIFrame.Preload(assetName);               // + (packageName, assetName) / string[] overloads

// Show — full signature:
// Show(int groupId, string assetName, object data = null, string awaitingUIAssetName = null,
//      uint priority = 0, Progression progression = null, Transform parent = null,
//      float awaitingUIExtraDuration = 0f)
await CoreFrames.UIFrame.Show(ScreenUIs.Id, ScreenUIs.Demo1UI, "hello data", ScreenUIs.DemoLoadingUI, 0);
var ui = await CoreFrames.UIFrame.Show<Demo1UI>(ScreenUIs.Id, ScreenUIs.Demo1UI);
// `data` is `object`, and every overload has a (packageName, assetName) twin, so a string or `null` in the
// `data` slot can be taken for an asset name. Compiler-verified (the same holds for CoreFrames.SRFrame.Show):
//   Show(id, name, "text") / Show(id, name, null)    -> binds (groupId, packageName, assetName): wrong asset, NO compile error
//   Show(name, "text")                               -> binds (packageName, assetName): wrong asset, NO compile error
//   Show(id, name, "text" or null, awaitingName)     -> CS0121 (ambiguous); so is Show(name, "text" or null, awaitingName)
// Safe spellings: pass a string / null `data` by name (`data: "text"`), leave `data` out when there is none,
// and pass the optional arguments after it by name (or include `priority`, as the demo line above does).
await CoreFrames.UIFrame.Show(ScreenUIs.Id, ScreenUIs.Demo1UI, data: "hello data");
await CoreFrames.UIFrame.Show(ScreenUIs.Id, ScreenUIs.Demo1UI, awaitingUIAssetName: ScreenUIs.DemoLoadingUI);
await CoreFrames.UIFrame.Show(ScreenUIs.Id, ScreenUIs.Demo1UI, parent: container);   // see "Custom parent" below

// Close / CloseAll — disableOnPreClose skips OnPreClose(); forceDestroy destroys + unloads
CoreFrames.UIFrame.Close(ScreenUIs.Demo1UI);
CoreFrames.UIFrame.Close(ScreenUIs.Demo1UI, disableOnPreClose: true, forceDestroy: true);
CoreFrames.UIFrame.CloseAll(true, true);                   // default group (id 0) only
CoreFrames.UIFrame.CloseAll(groupId, true, true, withoutAssetNames);
CoreFrames.UIFrame.CloseAllForAllGroups(true, true);
CoreFrames.UIFrame.CloseAllAndExcluded(...);               // ignores uiSettings.whenCloseAllToSkip

// Hide / Reveal — Hide keeps the instance; only a Hidden UI can be Revealed
CoreFrames.UIFrame.Hide(assetName);   CoreFrames.UIFrame.HideAll(groupId);
CoreFrames.UIFrame.Reveal(assetName); CoreFrames.UIFrame.RevealAll(groupId);

// Stack-by-stack (ESC menu pattern)
if (CoreFrames.UIFrame.GetStackByStackCount(groupId, canvasName) > 0)
    CoreFrames.UIFrame.CloseStackByStack(groupId, canvasName);

// Queries & access
CoreFrames.UIFrame.CheckIsShowing(assetName);  CheckIsHiding(assetName);  CheckHasAnyHiding();
var comp = CoreFrames.UIFrame.GetComponent<Demo1UI>(assetName);

// Refresh messaging -> OnReceiveAndRefresh(object) on showing/hiding instances
CoreFrames.UIFrame.SendRefreshData(new RefreshInfo { assetName = ..., data = ... });
CoreFrames.UIFrame.SendRefreshDataToAll();     // e.g. after language change
```

### UI prefab settings (`uiSettings` serialized on the UI prefab)

- `canvasName` — must match a `UICanvas` GameObject in the scene (demo: `CanvasCamera`, `CanvasWorld`).
- `nodeType` + order: `Fixed(0) < TopFixed(1000) < Popup(2000) < TopPopup(3000) < LoadingPopup(4000) < SysPopup(5000) < TopSysPopup(6000) < AwaitingPopup(7000)`; per-UI `order` 0–1000 when not stacked.
- `stack` — stacked UIs auto-sort by open order; `allowCloseStackByStack` enables the ESC-pop pattern.
- `whenCloseAllToSkip` / `whenHideAllToSkip` — exclude from bulk operations.
- `autoMask` + `maskSettings` (color/sprite/`isClickMaskToClose`) — pooled `_UIMaskNode` behind popups.
- `reverseChanges` — showing this UI hides the previous reverse UI (data preserved); closing reveals it. Freeze (`_UIFreezeNode`) blocks input during show/close animations.
- `FrameBase` fields: `groupId`, `monoDrive` (scene-driven, no manager), `allowInstantiate` (multi-instance, always destroy on close), `onCloseAndDestroy`.

### Custom parent (`Show(..., parent: transform)`, UIFrame and SRFrame)

- The instance is created under `parent` instead of the canvas node (UI) / the manager (SR). UIFrame honours the argument since v3.7.1; earlier versions ignored it for UI.
- It applies when the instance is created: a cached single-instance frame that is shown again stays where it is; multi-instance (`allowInstantiate`) frames get the parent passed to each `Show`.
- The framework adds a `UIParent` / `SRParent` component to `parent`; destroying that object closes and releases its frames through the manager.
- A UI under a custom parent keeps its own override-sorting `Canvas` (`nodeType` + `order` still decide the sorting order).

## SRFrame (scene resources — non-canvas managed prefabs)

Same surface as UIFrame minus canvas/stack concepts:

```csharp
CoreFrames.SRFrame.InitInstance();
await CoreFrames.SRFrame.Preload(ScenePrefs.CubeSceneSR);
await CoreFrames.SRFrame.Show(ScenePrefs.Id, ScenePrefs.CubeSceneSR);
await CoreFrames.SRFrame.Show(ScenePrefs.Id, ScenePrefs.CubeSceneSR, null, null, 0, null, parent);
var sr = await CoreFrames.SRFrame.Show<MeshResSR>(ResPrefs.Id, ResPrefs.MeshResSR);
if (sr == null) sr = CoreFrames.SRFrame.GetComponent<MeshResSR>(ResPrefs.MeshResSR);
CoreFrames.SRFrame.Hide(name); CoreFrames.SRFrame.Reveal(name);
CoreFrames.SRFrame.Close(name, false, true);
CoreFrames.SRFrame.CloseAll(groupId, true, true);
```

Use SR for: camera/audio-listener rigs, gameplay controllers, pools, per-stage world systems.

## USFrame (Unity scenes)

`build#SceneName` loads from Build Settings; no prefix loads a bundle scene (needs PatchLauncher initialized).

```csharp
CoreFrames.USFrame.InitInstance();

await CoreFrames.USFrame.LoadSingleSceneAsync(sceneName, progression);
await CoreFrames.USFrame.LoadSingleSceneAsync(packageName, sceneName, LocalPhysicsMode.None, progression);
await CoreFrames.USFrame.LoadAdditiveSceneAsync(sceneName, true, 100);   // (activateOnLoad, priority)
// Pass `activateOnLoad` positionally: `LoadAdditiveSceneAsync(sceneName, activateOnLoad: true)` is ambiguous (CS0121)
// between the short and the LocalPhysicsMode overload (also with `priority:` added).
await CoreFrames.USFrame.LoadSceneAsync(sceneName, LoadSceneMode.Additive, true, 100,
    (progress, currentCount, totalCount) => { /* progress UI */ });
CoreFrames.USFrame.LoadScene(sceneName, LoadSceneMode.Additive, progression);   // sync

// Main + subs in one call (AdditiveSceneInfo lives in the OxGFrame.CoreFrame.USFrame namespace)
await CoreFrames.USFrame.LoadMainAndSubScenesAsync(mainSceneName,
    new AdditiveSceneInfo[] { new AdditiveSceneInfo { sceneName = "Sub01", activeRootGameObjects = true } });

CoreFrames.USFrame.SetActiveSceneRootGameObjects(sceneName, true);
CoreFrames.USFrame.Unload(recursively: true, sceneName);    // all instances vs top-most only
CoreFrames.USFrame.GetSceneByName(sceneName);  SetActiveScene(sceneName);
```

## CPFrame (clone prefabs — cells/items; no Show/Close, lifetime = GameObject)

```csharp
CoreFrames.CPFrame.InitInstance();
await CoreFrames.CPFrame.PreloadAsync(TplPrefs.Demo1CP);

var cp = CoreFrames.CPFrame.LoadWithClone<Demo1CP>(TplPrefs.Demo1CP);                   // sync
var cp2 = await CoreFrames.CPFrame.LoadWithCloneAsync<Demo2CP>(TplPrefs.Demo2CP, parent);
var cp3 = await CoreFrames.CPFrame.LoadWithCloneAsync<Demo2CP>(name, position, rotation, parent, scale);

// Factory-method pattern (recommended, from CPFrameDemo):
public static async UniTask<Demo2CP> CloneParsedDemo2CP(int price, Transform parent = null)
{
    var cp = await CoreFrames.CPFrame.LoadWithCloneAsync<Demo2CP>(TplPrefs.Demo2CP, parent);
    if (cp.ParsingDemo2CP(price)) return cp;
    return null;
}
```

`CPBase` unloads its own asset reference on destroy. Destroy the GameObject to release.

## Lifecycle (FrameBase order)

`Awake (once) → OnCreate (once) → OnAutoBind (once) → OnBind (once) → OnPreShow (every open) → OnShow (every open)`

Bound nodes are collected between `OnCreate` and `OnAutoBind`: the collector is still empty in `OnCreate()`, so use bound fields from `OnBind()` onward.

| Hook | UIBase | SRBase | CPBase |
|---|---|---|---|
| `OnCreate()` | yes | yes | yes |
| `OnAutoBind()` (generated) | yes | yes | yes |
| `OnBind()` — one-time event wiring | yes | yes | yes |
| `async UniTask OnPreShow()` | yes | yes | obsolete no-op |
| `OnShow(object obj)` — receives `Show(..., data)` | yes | yes | `OnShow()` no arg |
| `OnShowAnimation(AnimationEnd end)` / `OnCloseAnimation` | UI only — MUST call `end()` | – | – |
| `OnReveal()` / `OnHide()` | yes | yes | obsolete |
| `OnPreClose()` / `OnClose()` | yes | yes | `OnClose()` = OnDisable |
| `OnUpdate/OnFixedUpdate/OnLateUpdate(float dt)` | yes | yes | only when driven (see below) |
| `OnReceiveAndRefresh(object)` | yes | yes | obsolete |
| `OnRelease()` — remove listeners, free resources | yes | yes | = OnDestroy |

Self-control from inside the class: `this.CloseSelf()`, `this.CloseSelf(disableOnPreClose, forceDestroy)`, `this.HideSelf()`. Use these for self-close; use `CoreFrames.UIFrame.Close(...)` only from external flows.

CP update hooks are not driven by a manager: the owner calls `cp.DriveUpdate(dt)` / `DriveFixedUpdate(dt)` / `DriveLateUpdate(dt)` (or the CP calls `DriveSelfUpdate(dt)` from its own loop); a scene-placed `monoDrive` CP is driven automatically only when the matching `OXGFRAME_CPFRAME_MONODRIVE_UPDATE_ON` / `_FIXEDUPDATE_ON` / `_LATEUPDATE_ON` define is set.

## CodeBinding

Binding has two halves that must stay in sync: the **GameObject names** in the prefab (you write these, following the grammar below) and the **`#region Binding Components` block** in the script (the generator writes it — never type or edit it yourself; how to trigger the tool is in `editor-tools.md`). Do not invent an alternative binding scheme (serialized references, `transform.Find` chains, your own naming) for frames that use CodeBinding.

### Naming grammar (fixed by the framework: `FrameConfig` + `Binder`)

```text
<Head>@<Name>[*<Tail> ...][$<access>][[attr] ...]

_Node@Open*Btn$private      _Node@Decor*Img$public[hi]      _Node@View*Img[sf]      ~Node@Slot
```

- **Head** — `_Node` for `UIBase` / `SRBase` prefabs, `~Node` for `CPBase` prefabs. It must match the script's base class: at runtime `~` nodes are bound only for CP and `_` nodes only for UI/SR, so the members generated for mismatched nodes stay `null`. Since v3.7.1 the generator reports them with a warning (`Found N node(s) whose bind prefix does not match the script type ...`, also returned in `messages[]` of `generate_bind_codes`) — rename those nodes and generate again.
- **Name** — required. It becomes part of a C# field name, so use a valid identifier fragment (letters and digits; no spaces, `-`, `.`, or Unity's ` (1)` duplicate suffix).
- **`*Tail`** — optional, repeatable. Each tail is a key of the project's `CodeBindingSettings.tailRules` and yields one field of that component type (`_Node@Icon*Img*Btn` gives an `Image` field and a `Button` field). No tail binds the `GameObject`. The node must really carry that component.
- **`$access`** — optional; a key of `variableAccessRules` (`protected`, `private`, `public` by default). Missing or unknown falls back to the first rule.
- **`[attr]`** — optional, repeatable; a key of `attrReferenceRules` (`[hi]` = `[HideInInspector]`, `[sf]` = `[SerializeField]` by default).
- The order is mandatory (Name, tails, access, attributes) and every part is case-sensitive.
- **Arrays** are not a naming feature: when two or more scanned nodes produce the same bind key (same Name and Tail), the generator emits one array field for them. The first of those nodes decides access and attributes.
- Children that are not bound must not start with `_` (UI/SR) or `~` (CP) — the runtime logs "Invalid naming format" for them.
- **Stop end** — a node whose name ends with `#` ends the **whole remaining scan** (depth-first: its subtree, its later siblings, and the later siblings of its ancestors), is itself never bound, and has the `#` removed from its name at runtime. Its position is decided by the stop-end tool (the node right after the last bound node) — never place it by hand.

### Project settings decide the generated code (`CodeBindingSettings`)

Tail keys, access keywords, attribute tokens, the field prefix, first-letter casing, `this.` usage, plural suffixes, and whether the tool writes the script (`methodType` Auto) or only shows a window (Manual) are **per-project data**, not framework constants. Find the asset (`t:CodeBindingSettings`; one per project, created at `Assets/CodeBindingSettings.asset` on the first generator run) and read it before naming nodes. Use only keys that exist there, and ask the developer before adding or changing a rule.

Default tail keys: `Trans`, `RectTrans`, `Img`, `RawImg`, `Txt`, `Btn`, `Tgl`, `Sld`, `ScrBar`, `ScrView`, `Drd`, `Field`, `TmpTxt`, `TmpDrd`, `TmpField`, `BtnPlus`, `NodePool` (the last two need the optional `OxGKit.ButtonSystem` / `OxGKit.PoolSystem` packages).

### What the generator emits (read it, never write it)

With default settings, the nodes `_Node@Open*Btn$private`, `_Node@Decor*Img$public[hi]`, `_Node@View*Img[sf]` and two nodes named `_Node@Item*Img` produce:

```csharp
#region Binding Components
private Button _openBtn;
[HideInInspector] public Image decorImg;
[SerializeField] protected Image _viewImg;
protected Image[] _itemImgs;

/// <summary>
/// Auto Binding Section
/// </summary>
protected override void OnAutoBind()
{
    base.OnAutoBind();
    this._openBtn = this.collector.GetNodeComponent<Button>("Open*Btn");
    this.decorImg = this.collector.GetNodeComponent<Image>("Decor*Img");
    this._viewImg = this.collector.GetNodeComponent<Image>("View*Img");
    this._itemImgs = this.collector.GetNodeComponents<Image>("Item*Img");
}
#endregion
```

The whole region is replaced on every run and the required `using` lines are added. Bindings that a non-abstract base class already declares in its own region are left out. Do not rename, restyle or hand-extend generated members (they follow `CodeBindingSettings`, not the team's code style); if a name is wrong, fix the GameObject name or the settings and regenerate. Binding regions in older scripts and samples may show an earlier layout — regenerate instead of imitating them.

Runtime lookups, for the rare case outside generated fields: `collector.GetNode("Name")`, `GetNodes("Name")`, `GetNodeComponent<T>("Name*Tail")`, `GetNodeComponents<T>("Name*Tail")` — the key is the Name plus `*Tail`, without head, access or attributes.

### Workflow (the same steps a developer performs)

Every step that names a tool is triggered, not imitated — `editor-tools.md` lists the entry points in order of preference: the Unity CLI commands (`unity command oxgframe.coreframe ...`), the path-based static methods, the menu items.

1. Read the project's rules first (`get_binding_settings`, or the `CodeBindingSettings` asset): tails, access rules, attributes, casing and method type are per project.
2. Create the script from the framework template (`create_script`) — it already contains the empty `#region Binding Components` / `#endregion` markers. Wait for the compile and the script reload (Unity CLI: the waiting rule in `editor-tools.md`; send nothing else meanwhile).
3. Create the prefab from the framework template (`create_prefab`), add the script to the prefab root, fill `uiSettings` / `srSettings`.
4. Build the hierarchy and name the nodes to bind with the grammar above. With the Unity CLI, do steps 3–4 on a temporary instance of the prefab and apply it back (`editor-tools.md` lists the exact commands).
5. Optional: run **Auto Generate Stop End Symbol** (`generate_stop_end`) on the prefab.
6. Run **Auto Generate Bind Codes** (`generate_bind_codes`) on the prefab root; confirm it reports `inserted` (Console: `[Auto] Completed automatically binding content to script!!!`). Wait for the compile again, the same way as in step 2.
7. Read the regenerated region, then write logic against those members: one-time event wiring in `OnBind()`, per-show logic in `OnShow`.
8. After any rename / add / remove in the hierarchy, run the generator again on the whole root.

## Script skeletons come from the templates

Do not write a UI / SR / CP class from memory. Create it from the framework template (`Assets/Create/OxGFrame/Core Frame/...`, see `editor-tools.md`): the file then has the binding markers and every lifecycle override in the framework's order. For orientation, the UI template contains, in this order:

`OnCreate` → `OnPreShow` (async) → `OnPreClose` → `OnShowAnimation` / `OnCloseAnimation` (both already call `animationEnd()`) → `OnBind` → `OnShow(object)` → `OnClose` → `OnReveal` → `OnHide` → `OnUpdate(float)` → `OnReceiveAndRefresh(object)` → `OnRelease`

Typical additions after the bindings were generated:

```csharp
protected override void OnBind()
{
    this._closeBtn.onClick.AddListener(this._OnClickClose);   // member name taken from the generated region
}

public override void OnRelease()
{
    this._closeBtn.onClick.RemoveListener(this._OnClickClose);
}

private void _OnClickClose() { this.CloseSelf(); }
```
