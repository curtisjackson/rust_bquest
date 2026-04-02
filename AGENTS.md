# AGENTS.md

## Scope

These notes apply to the whole `bdquest` repository.

## Project Type

- Oxide/uMod plugin for Rust
- Main runtime file: `plugins/BQuest.cs`
- Quest content lives in JSON under `data/BQuest/`

## Source Of Truth

- Quest logic implementation lives in `plugins/BQuest.cs`
- Quest content, rewards, cooldowns and aliases should live in JSON whenever possible
- Avoid adding hardcoded content-specific rules to C# if the same behavior can be expressed in quest data

## Files To Update Together

When adding or changing quest content, update both:

- `data/BQuest/Quest.json` or `data/BQuest/Daily.json`
- default builders in `plugins/BQuest.cs`

When changing questline membership, also update:

- `data/BQuest/Questlines.json`
- default questline catalog in `plugins/BQuest.cs`

## Persistence

Player runtime state is stored in:

- `data/BQuest/PlayerInfo.json`

Analytics are stored in:

- `data/BQuest/QuestStatistics.json`

Do not confuse analytics resets with player-progress resets.

## UI Guidance

- The main `/q` UI is heavily customized to match a Rust quest reference screen
- Prefer targeted refreshes instead of full UI rebuilds
- Be careful with anything that runs every second; refresh the smallest possible subtree
- Keep reward icons square and reward quantities overlaid on the icon

## Matching Rules

- Objective matching should prefer `Target`, `MatchMode` and `MatchAliases`
- For NPCs modified by other plugins like `BetterNpc`, put aliases into JSON rather than hardcoding C# exceptions

## Rewards

Current reward behavior:

- `XP` -> `SkillTree` and `Economics`
- `RP` -> `Economics`, fallback `ServerRewards`

If reward provider behavior changes, update both code and docs.

## Commands

Public:

- `/q`
- `bquest.player.reset <steamid64>`
- `bquest.stat`

Deprecated for this repo state:

- `/quest`
- `/qlist`

Do not reintroduce those deprecated chat commands unless explicitly requested.

## Documentation

If behavior changes materially, keep these files in sync:

- `README.md`
- `docs/INSTALL.md`
- `docs/DATA_FORMATS.md`
- `docs/DEVELOPER.md`
- `docs/BQuest.config.example.json`

## Environment Note

This workspace does not currently provide `mcs`, `csc` or `dotnet`, so local compilation may be unavailable.
