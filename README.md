# BQuest

`BQuest` is a fullscreen quest plugin for Rust servers on Oxide/uMod.

The current repository contains:

- the main plugin in `plugins/BQuest.cs`
- quest data in `data/BQuest/`
- config in `config/BQuest.json`
- localization in `lang/en/BQuest.json` and `lang/ru/BQuest.json`

This README reflects the code and data currently present in this folder.

## What The Plugin Does

- Opens the main quest UI with `/q`
- Shows three tabs in the UI: `Quests`, `Daily`, `Questlines`
- Supports tracked quests and a movable mini quest list
- Supports chat messages and toast-style progress notifications
- Stores player progress, cooldowns, tracked quests and UI preferences in JSON
- Supports repeatable quests with per-quest cooldown from quest JSON
- Supports quest chains through `QuestlineId`, `QuestlineOrder` and `RequiredQuestIds`
- Supports item submission objectives through `SubmissionRequired`
- Supports multilingual quest titles, descriptions and objective text

![BQuest UI](pic1.png)

## Repository Layout

- `plugins/BQuest.cs`: main plugin source and fallback builders
- `config/BQuest.json`: runtime configuration
- `data/BQuest/Quest.json`: standard quests
- `data/BQuest/Daily.json`: daily-tab quests
- `data/BQuest/Questlines.json`: questline catalog
- `data/BQuest/PlayerInfo.json`: per-player progress and cooldown data
- `data/BQuest/QuestStatistics.json`: aggregated quest analytics
- `lang/en/BQuest.json`: English localization
- `lang/ru/BQuest.json`: Russian localization

## Current Bundled Content

- `55` standard quests in `data/BQuest/Quest.json`
- `2` daily quests in `data/BQuest/Daily.json`
- `2` questlines in `data/BQuest/Questlines.json`

The bundled quest data currently uses these objective types:

- `Gather`
- `Craft`
- `Delivery`
- `Deploy`
- `EntityKill`
- `Growseedlings`
- `BossMonster`
- `AirEvent`
- `SupermarketEvent`
- `HarborEvent`

## Objective Matching

Objective matching is data-driven.

- `Target` is the primary match token
- `MatchAliases` adds extra accepted names without changing C#
- `MatchMode` supports `shortname`, `contains` and `prefab`
- `TargetSkinId` can restrict progress to a specific skin

For NPCs or entities coming from other plugins, prefer extending `MatchAliases` in JSON instead of adding hardcoded exceptions.

## Quest Data Notes

Each quest definition can use fields such as:

- `QuestID`
- `QuestDisplayName`, `QuestDescription`, `QuestMissions`
- `QuestDisplayNameMultiLanguage`, `QuestDescriptionMultiLanguage`, `QuestMissionsMultiLanguage`
- `QuestType`, `QuestCategory`, `QuestTabType`
- `Objectives`
- `PrizeList`
- `QuestPermission`
- `RequiredPermission`
- `RequiredQuestIds`
- `AvailabilityConditions`
- `BlockConditions`
- `QuestlineId`, `QuestlineOrder`
- `IsRepeatable`, `Cooldown`
- `AllowManualStart`, `AllowTrack`, `AllowCancel`, `AllowClaim`
- `ResetProgressOnWipe`

Each objective can use fields such as:

- `ObjectiveId`
- `Type`
- `Target`
- `TargetCount`
- `Description`, `DescriptionMultiLanguage`
- `MatchAliases`
- `MatchMode`
- `TargetSkinId`
- `Hidden`
- `Order`
- `SubmissionRequired`

## Rewards

The current code supports these reward types:

- `Item`
- `Blueprint`
- `BlueprintItem`
- `BlueprintReward`
- `CustomItem`
- `Command`
- `RP`
- `XP`
- `BlueprintFragments`
- `Fragments`

Current reward behavior:

- `XP` uses `SkillTree` when available and also deposits the same amount into `Economics`
- `RP` uses `Economics` first, then falls back to `ServerRewards`
- `BlueprintFragments` creates `blueprintfragment`
- `Command` rewards replace `%STEAMID%` with the player's SteamID

## Commands

Public player command:

- `/q`

Admin covalence commands:

- `bquest.player.reset <steamid64>`
- `bquest.stat`

Deprecated chat commands are not registered in the current codebase:

- `/quest`
- `/qlist`

## Permissions

Base permission:

- `BQuest.default`

Quest-specific permissions can also be generated from quest JSON through `QuestPermission`, and a quest can additionally require any external permission through `RequiredPermission`.

## Integrations

The code currently contains three different integration levels.

Direct API integrations actually called through `PluginReference`:

- `Economics`
- `ServerRewards`
- `SkillTree`
- `Notify`
- `IQChat`
- `Friends`
- `Clans`
- `EventHelper`
- `Battles`
- `Duel`
- `Duelist`
- `ArenaTournament`

Hook-only compatibility for external plugins or event systems:

- `CustomVendingSetup` via `OnCustomVendingSetupGiveSoldItem`
- vanilla vending via `OnNpcGiveSoldItem`
- `RaidableBases` via `OnRaidableBaseCompleted`
- `BossMonster`-style boss kill hook via `OnBossKilled`
- `IQDronePatrol` via `OnDroneKilled`
- `IQDefenderSupply` via `OnLootedDefenderSupply`
- event winner hooks such as `OnAirEventWinner`, `OnSupermarketEventWinner`, `OnHarborEventWinner`, `OnSatDishEventWinner`, `OnSputnikEventWin`, `OnGasStationEventWinner`, `OnTriangulationWinner`, `OnFerryTerminalEventWinner`, `OnConvoyEventWin`, `OnCaravanEventWin`

Declared plugin references that are currently not used directly in code:

- `MarkerManager`
- `BossMonster`
- `CustomVendingSetup`
- `RaidableBases`
- `IQDronePatrol`
- `IQDefenderSupply`

PvP kill progress excludes friendly/event contexts when the corresponding integrations are available:

- `Friends`
- `Clans`
- `EventHelper`
- `Battles`
- `Duel`
- `Duelist`
- `ArenaTournament`

## Persistence

Player runtime state is stored in `data/BQuest/PlayerInfo.json`, including:

- active quests
- completed quests
- cooldowns
- tracked quests
- notification settings
- mini UI position
- reward claim state

Analytics are stored separately in `data/BQuest/QuestStatistics.json`.

Do not confuse analytics resets with player progress resets.

## Wipes And Resets

- If `settings.useWipe` is `true`, player quest progress is cleared on `OnNewSave`
- If `settings.useWipePermission` is `true`, quest-specific granted permissions are revoked on wipe
- Individual quests can mark `ResetProgressOnWipe`
- Repeatable quest cooldowns are always taken from each quest's own `Cooldown` field

Daily quests are a separate UI/data category. In the current code they do not use a special midnight reset system; availability is still controlled by normal completion and cooldown rules.

## Configuration Highlights

Notable options from `config/BQuest.json`:

- active quest limit: `settings.questCount`
- tracked quest limit: `settings.TrackedQuestLimit`
- cooldown widget limit: `settings.CooldownListLimit`
- default notification mode: `settings.DefaultNotificationMode`
- notification position: `settings.NotificationPosition`
- statistics publishing: `statisticsCollectionSettings.*`
- list pagination and visibility: `ui.*`
- notification defaults: `notifications.*`
- UI colors: `theme.*`

## Installation

1. Put `plugins/BQuest.cs` into `oxide/plugins/`
2. Put the contents of `data/BQuest/` into `oxide/data/BQuest/`
3. Put `config/BQuest.json` into `oxide/config/`
4. Put `lang/en/BQuest.json` and `lang/ru/BQuest.json` into the matching `oxide/lang/` folders
5. Load the plugin with `oxide.load BQuest`
6. Grant the base permission if needed:

```text
oxide.grant group default BQuest.default
```

## Editing Guidance

When changing content in this repository:

- prefer updating JSON quest data instead of hardcoding content rules in C#
- keep `data/BQuest/Quest.json` and `data/BQuest/Daily.json` aligned with any default/fallback builders in `plugins/BQuest.cs`
- keep `data/BQuest/Questlines.json` aligned with any questline defaults in `plugins/BQuest.cs`
- update README if command behavior, reward routing, persistence or data format changes materially

## Additional Docs

- quest authoring guide in `docs/QUEST_AUTHORING_RU.md`
