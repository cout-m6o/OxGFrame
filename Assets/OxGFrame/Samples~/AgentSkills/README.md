# OxGFrame AI Agent Skill

An Agent Skill for AI coding agents (Claude Code, OpenAI Codex CLI, Cursor, Copilot, etc.) that teaches the agent how to develop Unity projects with **OxGFrame**.

The skill is a folder with a `SKILL.md` entry file (YAML frontmatter + instructions) and a `references/` folder with detailed, source-verified API usage documents. The format follows the Agent Skills convention (progressive disclosure: the agent reads `SKILL.md` first, then loads only the references it needs).

## What is included

| Skill | Purpose |
|---|---|
| `oxgframe-unity-skill` | OxGFrame framework usage: AssetLoader (patch/download/load), CoreFrame (UI/SR/US/CP), MediaFrame (Audio/Video), GSIFrame (game stages), NetFrame, CenterFrame (API/Event), Hotfixer (HybridCLR), plus coding style and production architecture guidance. All API usage is verified against the OxGFrame source and the official Samples. |

**Tool-first by design.** The skill makes the agent work the way a developer does: for everything OxGFrame generates — binding code (`_Node@` / `~Node@` CodeBinding), stop-end markers, template scripts and prefabs, config files — the agent prepares the inputs and triggers the framework's own editor tool (menu item or the public editor method behind it) instead of hand-writing an "equivalent" or inventing its own rules. `references/editor-tools.md` lists every tool and how to trigger it; when the agent has no way to run editor code, it hands that single step to the developer.

**Unity CLI ready.** Since v3.7.1 OxGFrame exposes those tools as Unity CLI commands (`unity command oxgframe.coreframe generate_bind_codes --path Assets/.../ShopUI.prefab`, `create_script`, `create_prefab`, `generate_stop_end`, the config exporters, the API Updater, ...), and the skill uses them as its first choice. See "Unity CLI" below.

OxGFrame uses several OxGKit utility APIs internally (LoggingSystem, SaverSystem, TimeSystem, Utilities) — the skill briefly covers those. Dedicated skills for the individual OxGKit systems are provided separately by the OxGKit project (`https://github.com/cout-m6o/OxGKit`).

## How to install

1. Import this sample through **Package Manager > OxGFrame > Samples > AI Agent Skills**.
   The files are copied to `Assets/Samples/OxGFrame/<version>/AI Agent Skills/`.
2. Copy (or symlink) the skill folder to wherever your agent discovers skills:

   | Agent | Location |
   |---|---|
   | Claude Code | `<project>/.claude/skills/oxgframe-unity-skill/` (or `~/.claude/skills/` for all projects) |
   | OpenAI Codex CLI | `<project>/.agents/skills/oxgframe-unity-skill/` (or `~/.codex/skills/`) |
   | Other agents | Reference the folder from your agent context file (`AGENTS.md`, `CLAUDE.md`, `.cursorrules`, ...), e.g. "Read Assets/Samples/.../oxgframe-unity-skill/SKILL.md before working with OxGFrame APIs." |

3. Optionally delete the imported copy under `Assets/Samples/` afterwards — the skill files are plain Markdown and do not need to live inside the Unity project once copied. (Keeping them under `Assets/` is harmless; they import as TextAssets.)

## Unity CLI

OxGFrame's editor tools are available to the [Unity CLI](https://docs.unity.com/en-us/unity-cli) as commands, so an agent can run the real tool from a terminal instead of asking you to press a menu item.

Requirements (all on your side — the agent is told not to install anything without asking):

1. Unity 6.0 or later, with the project open in the editor.
2. The Unity CLI (`unity`) installed.
3. Unity's Pipeline package (`com.unity.pipeline` 1.x) in the project, e.g. `unity pipeline install`.

OxGFrame needs no setup: its commands live in separate `OxGFrame.<Module>.Editor.UnityCli` assemblies that compile only while the Pipeline package (1.x) is installed, so projects without it — and Unity versions before 6 — are not affected in any way.

```
unity command oxgframe.coreframe                      # list the commands of a group
unity command oxgframe.coreframe get_binding_settings --format json
unity command oxgframe.coreframe create_script --template ui --path Assets/Scripts/UI/ShopUI.cs
unity command oxgframe.coreframe create_prefab --template ui --path Assets/Prefabs/UI/ShopUI.prefab
unity command oxgframe.coreframe generate_bind_codes --path Assets/Prefabs/UI/ShopUI.prefab
```

Groups: `oxgframe.coreframe`, `oxgframe.mediaframe`, `oxgframe.assetloader`, `oxgframe.hotfixer`, `oxgframe.gsiframe`, `oxgframe.centerframe`, `oxgframe.apiupdater`. Every command runs the same code as the matching menu item / window; commands that replace an existing config file or rewrite scripts require `--confirm` and support `--dry-run`. The full list is in `references/editor-tools.md`.

After a command that writes a script (`create_script`, `generate_bind_codes`), let the editor finish compiling and reloading before the next command: `unity command script recompile`, repeat `unity command script recompile_status` until it reports `completed` or `up_to_date`, then check `unity command editor status` (`compiling` is `false`). The commands were verified against `com.unity.pipeline` 1.0.1 through its own command server.

Without the Unity CLI the same tools are reachable as static methods that take plain paths (for other editor bridges and batch mode), and through the menu items.

## Notes

- The skill is written in English so it works well as agent context in any locale; the agent should reply in the user's language.
- API signatures and editor entry points were verified against OxGFrame v3.7.1 (Unity 2021.3 – 6.x, YooAsset 2.x / 3.x, HybridCLR 8.x / 9.x). When in doubt, the skill instructs the agent to re-verify against the installed package source — other OxGFrame versions may differ.
- Triggering editor tools needs a way to reach the editor: the Unity CLI (above) or another editor bridge (for example an MCP server that can execute editor code); without one the agent asks the developer to press the menu item / shortcut.
- `references/source-map.md` lists the exact source files and search commands used for verification.
