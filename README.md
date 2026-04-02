# BQuest

Modern full-screen quest plugin for Rust servers on Oxide/uMod.

`BQuest` provides:

- regular quests
- daily-tab quests
- quest chains
- submission quests with item hand-in
- repeatable quests with per-quest cooldown from JSON
- in-game full-screen CUI
- mini quest list
- progress toasts and chat notifications
- optional integrations with common Rust plugins

## Repository Layout

- `plugins/BQuest.cs` - main plugin source
- `data/BQuest/Quest.json` - regular quest definitions
- `data/BQuest/Daily.json` - daily-tab quest definitions
- `data/BQuest/Questlines.json` - chain definitions
- `data/BQuest/PlayerInfo.json` - player progress, active quests, cooldowns
- `data/BQuest/QuestStatistics.json` - aggregate analytics only
- `docs/INSTALL.md` - installation and operations
- `docs/DATA_FORMATS.md` - JSON schema overview
- `docs/DEVELOPER.md` - developer notes and architecture
- `docs/BQuest.config.example.json` - example config

## Current Behavior

- Main UI opens with `/q`
- Quest cooldown is always taken from the quest's own `Cooldown` field in JSON
- Daily-tab quests do not have any hidden midnight reset logic
- Submission quests can auto-complete and auto-claim when `Submit` closes the objective
- `XP` rewards are awarded to `SkillTree` and also deposited into `Economics`
- `RP` rewards are deposited into `Economics`, with `ServerRewards` used as fallback if `Economics` is missing
- NPC matching can be configured in JSON through `Target` and `MatchAliases`

## Quick Start

1. Copy `plugins/BQuest.cs` into `oxide/plugins/`
2. Copy `data/BQuest/*.json` into `oxide/data/BQuest/`
3. Load the plugin with `oxide.load BQuest`
4. Grant access:
   `oxide.grant group default BQuest.default`
5. Open the UI in game with `/q`

## Admin Notes

- Edit quest rewards, cooldowns and aliases directly in JSON
- If you want to reset player cooldowns or active/completed quest state, clear `data/BQuest/PlayerInfo.json` while the plugin is unloaded
- `QuestStatistics.json` is analytics only and does not affect quest availability

## More Docs

- [Installation](docs/INSTALL.md)
- [Data Formats](docs/DATA_FORMATS.md)
- [Developer Notes](docs/DEVELOPER.md)
