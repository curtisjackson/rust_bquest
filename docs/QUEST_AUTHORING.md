# BQuest: quest authoring guide

Language versions: [RU](QUEST_AUTHORING_RU.md)

This file explains how to create and edit quests for `BQuest` stored in `data/BQuest/`.

The document is based on the current plugin logic in `plugins/BQuest.cs`.

## Where things are stored

- `data/BQuest/Quest.json` - regular quests
- `data/BQuest/Daily.json` - quests shown in the `Daily` tab
- `data/BQuest/Questlines.json` - questlines
- `data/BQuest/PlayerInfo.json` - player progress
- `data/BQuest/QuestStatistics.json` - aggregated statistics

To create a new quest, you usually edit only:

- `data/BQuest/Quest.json`
- `data/BQuest/Daily.json`
- `data/BQuest/Questlines.json` if the quest belongs to a questline

## File structure

### `Quest.json`

```json
{
  "Quests": [
    {
      "QuestID": 1001,
      "QuestDisplayName": "Stone Collector",
      "QuestType": "Gather",
      "QuestCategory": "Starter",
      "QuestTabType": "Quests",
      "Objectives": [],
      "PrizeList": []
    }
  ]
}
```

### `Daily.json`

```json
{
  "DailyQuests": [
    {
      "QuestID": 2001,
      "QuestDisplayName": "Daily Lumberjack",
      "QuestType": "Gather",
      "QuestCategory": "Daily",
      "QuestTabType": "Daily",
      "Objectives": [],
      "PrizeList": []
    }
  ]
}
```

### `Questlines.json`

```json
{
  "Questlines": [
    {
      "QuestlineId": 3001,
      "DisplayName": "Frontier Path",
      "StageQuestIds": [3101, 3102, 3103]
    }
  ]
}
```

## Core behavior

- A quest starts progressing only after the player starts it, if `AllowManualStart = true`
- Progress is driven by the `Objectives` array
- Rewards are defined in `PrizeList`
- If `IsRepeatable = true`, the plugin starts a cooldown using `Cooldown` after completion
- If `IsRepeatable = false`, the quest stays completed
- The `Daily` tab does not have a special midnight reset system; these are regular quests with `QuestTabType = "Daily"`
- If a quest belongs to a questline, it can also be gated by `QuestlineId`, `QuestlineOrder`, and `RequiredQuestIds`

## Quest fields

Below are the `QuestDefinition` fields that can be set in JSON.

### Practically required

- `QuestID` - unique numeric quest ID
- `QuestDisplayName` - quest title
- `QuestType` - general quest type
- `QuestCategory` - category used for grouping in the UI
- `Objectives` - objective list
- `PrizeList` - reward list

### Commonly used

- `QuestDescription` - quest description
- `QuestMissions` - short mission text
- `QuestTabType` - UI tab, usually `Quests` or `Daily`
- `Icon` - quest icon or related token
- `IsRepeatable` - whether the quest can be repeated
- `Cooldown` - cooldown in seconds after finishing a repeatable quest
- `AllowManualStart` - whether the quest can be started manually from the UI
- `AllowTrack` - whether the quest can be tracked
- `AllowCancel` - whether an active quest can be canceled
- `AllowClaim` - whether reward claim is manual
- `ResetProgressOnWipe` - shows the warning that quest progress resets on wipe

### Permissions and restrictions

- `QuestPermission` - permission suffix, registered as `BQuest.<QuestPermission>`
- `RequiredPermission` - external permission the player must already have
- `RequiredQuestIds` - list of quest IDs that must be completed
- `AvailabilityConditions` - extra availability conditions
- `BlockConditions` - blocking conditions

### Questline fields

- `QuestlineId` - questline ID
- `QuestlineOrder` - stage order inside the questline

If `QuestlineId` is set and `QuestlineOrder > 1`, the plugin also checks completion of the previous questline stage.

### Localization

- `QuestDisplayNameMultiLanguage`
- `QuestDescriptionMultiLanguage`
- `QuestMissionsMultiLanguage`

Example:

```json
"QuestDisplayNameMultiLanguage": {
  "ru": "Сборщик камня",
  "en": "Stone Collector"
}
```

If a localized field is set, it has priority over the plain text field.

### Service and rare fields

These fields exist in the model, but are currently weakly used or have little effect:

- `Target`
- `ActionCount`
- `IsReturnItemsRequired`
- `IsMultiLanguage`
- `VisibilityState`
- `UIBadges`

`Target` and `ActionCount` matter only as a fallback when `Objectives` is missing entirely. In that case, the plugin auto-creates one objective from `QuestType`, `Target`, and `ActionCount`.

## Objective fields

Each object in `Objectives` describes one objective.

### Main fields

- `ObjectiveId` - unique objective ID inside the quest
- `Type` - objective type
- `Target` - what should be matched
- `TargetCount` - required amount
- `Description` - objective text

### Matching fields

- `MatchAliases` - list of extra accepted names
- `MatchMode` - comparison mode: `shortname`, `contains`, `prefab`
- `TargetSkinId` - optional restriction by skin ID

### UI fields

- `Icon` - objective icon
- `DescriptionMultiLanguage` - localized objective text
- `Hidden` - hides the objective from the main display
- `Order` - objective display order

### Submission fields

- `SubmissionRequired` - objective requires item hand-in from inventory

If a quest only has `SubmissionRequired` objectives, items can be submitted immediately.

If a quest has both normal objectives and `SubmissionRequired` objectives, the normal objectives must be completed first, and only then item submission becomes available.

## Supported objective types

The objective type is defined by `Objectives[].Type`. Progress works only if the string matches the type expected by the code.

### Standard Rust types

- `Gather`
- `Loot`
- `EntityKill`
- `Craft`
- `Research`
- `Grade`
- `Swipe`
- `Deploy`
- `PurchaseFromNpc`
- `HackCrate`
- `RecycleItem`
- `Fishing`
- `Growseedlings`
- `Delivery`

### External plugin and event types

- `BossMonster`
- `AirEvent`
- `SupermarketEvent`
- `HarborEvent`
- `RaidableBases`
- `IQDronePatrol`
- `IQDefenderSupply`
- `SatelliteDishEvent`
- `Sputnik`
- `GasStationEvent`
- `Triangulation`
- `FerryTerminalEvent`
- `Convoy`
- `Caravan`

### How to choose `Target`

General rule:

- for most types, `Target` should match the shortname, prefab name, or another token coming from the hook
- if matching is unstable, add `MatchAliases`
- if you need softer matching, use `MatchMode = "contains"`

Practical examples:

- `Gather`: `wood`, `stones`, `metal.ore`
- `Craft`: `bandage`, `syringe.medical`
- `Deploy`: prefab/token of the placed object
- `EntityKill`: `player`, `bear`, `wolf`, `scientist`, or an alias list
- `Delivery`: shortname of the item the player must hand in
- `BossMonster`: `BossMonster` or a specific boss alias/token
- `AirEvent`, `HarborEvent`, `SupermarketEvent`: usually `Target` matches the event name

For `RaidableBases`, `Target` is not the plugin name, but the difficulty:

- `Easy`
- `Medium`
- `Hard`
- `Expert`
- `Nightmare`

## Availability conditions

`AvailabilityConditions` and `BlockConditions` use objects like:

```json
{
  "Type": "QuestCompleted",
  "Value": "1001"
}
```

The code currently supports these condition types:

- `QuestCompleted`
- `QuestActive`
- `TrackedQuest`

Logic:

- `AvailabilityConditions` - all conditions must be true
- `BlockConditions` - if any condition is true, the quest is blocked

## Reward fields

Each object in `PrizeList` describes one reward.

### Main fields

- `PrizeName` - displayed reward name
- `PrizeType` - reward type
- `ItemShortName` - item shortname
- `ItemAmount` - amount

### Extra fields

- `ItemSkinID` - item skin ID
- `CustomItemName` - custom item name for `CustomItem`
- `PrizeCommand` - server command for `Command`
- `Icon` - reward icon
- `IsHidden` - hide reward in the UI

### Service fields

These exist in the model, but currently have little effect:

- `CommandImageUrl`
- `Description`
- `Rarity`

## Supported reward types

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

### Reward behavior

- `Item` - gives a regular item
- `Blueprint` / `BlueprintItem` / `BlueprintReward` - gives a blueprint
- `CustomItem` - gives an item and can rename it through `CustomItemName`
- `Command` - runs a server command, `%STEAMID%` can be used in the command string
- `RP` - first tries `Economics`, then falls back to `ServerRewards`
- `XP` - tries to award XP through `SkillTree` and also deposits the same amount into `Economics`
- `BlueprintFragments` / `Fragments` - gives `blueprintfragment`

## Questline fields

`Questlines.json` uses this structure:

- `QuestlineId` - unique questline ID
- `DisplayName` - questline title
- `DisplayNameMultiLanguage` - localized title
- `Icon` - questline icon
- `StageQuestIds` - ordered list of quest stage IDs

To display a quest correctly in a questline:

1. Set `QuestlineId` on the quest itself
2. Set `QuestlineOrder`
3. Add the `QuestID` to `StageQuestIds`

## Minimal regular quest example

```json
{
  "QuestID": 5001,
  "QuestDisplayName": "Wood Worker",
  "QuestDisplayNameMultiLanguage": {
    "ru": "Дровосек",
    "en": "Wood Worker"
  },
  "QuestDescription": "Gather wood for the camp.",
  "QuestDescriptionMultiLanguage": {
    "ru": "Добудьте дерево для лагеря.",
    "en": "Gather wood for the camp."
  },
  "QuestMissions": "Gather 5000 wood.",
  "QuestMissionsMultiLanguage": {
    "ru": "Соберите 5000 дерева.",
    "en": "Gather 5000 wood."
  },
  "QuestType": "Gather",
  "QuestCategory": "Starter",
  "QuestTabType": "Quests",
  "Objectives": [
    {
      "ObjectiveId": "5001_wood",
      "Type": "Gather",
      "Target": "wood",
      "TargetCount": 5000,
      "Description": "Gather wood",
      "DescriptionMultiLanguage": {
        "ru": "Соберите дерево",
        "en": "Gather wood"
      }
    }
  ],
  "PrizeList": [
    {
      "PrizeName": "Scrap",
      "PrizeType": "Item",
      "ItemShortName": "scrap",
      "ItemAmount": 100
    },
    {
      "PrizeName": "XP",
      "PrizeType": "XP",
      "ItemAmount": 2500
    }
  ],
  "IsRepeatable": true,
  "Cooldown": 3600,
  "AllowManualStart": true,
  "AllowTrack": true,
  "AllowCancel": true,
  "AllowClaim": true
}
```

## Item submission quest example

```json
{
  "QuestID": 5002,
  "QuestDisplayName": "Supply Delivery",
  "QuestType": "Delivery",
  "QuestCategory": "Starter",
  "QuestTabType": "Quests",
  "Objectives": [
    {
      "ObjectiveId": "5002_submit_cloth",
      "Type": "Delivery",
      "Target": "cloth",
      "TargetCount": 200,
      "Description": "Submit cloth",
      "SubmissionRequired": true
    }
  ],
  "PrizeList": [
    {
      "PrizeName": "Medical Syringe",
      "PrizeType": "Item",
      "ItemShortName": "syringe.medical",
      "ItemAmount": 5
    }
  ],
  "IsRepeatable": true,
  "Cooldown": 600
}
```

## Staged submission quest example

The player first completes a normal objective, then submits items:

```json
{
  "QuestID": 5003,
  "QuestDisplayName": "Farm And Deliver",
  "QuestType": "Delivery",
  "QuestCategory": "Farming",
  "Objectives": [
    {
      "ObjectiveId": "5003_harvest",
      "Type": "Growseedlings",
      "Target": "hemp",
      "TargetCount": 20,
      "Description": "Harvest hemp"
    },
    {
      "ObjectiveId": "5003_submit",
      "Type": "Delivery",
      "Target": "cloth",
      "TargetCount": 100,
      "Description": "Submit cloth",
      "SubmissionRequired": true
    }
  ],
  "PrizeList": [
    {
      "PrizeName": "Scrap",
      "PrizeType": "Item",
      "ItemShortName": "scrap",
      "ItemAmount": 75
    }
  ]
}
```

## Recommendations

- Always use a unique `QuestID`
- It is better to define `Objectives` explicitly instead of relying on the fallback `Target` and `ActionCount`
- For unusual NPCs, bosses, and entities, use `MatchAliases`
- For `EntityKill`, test matching on one quest before scaling it to a whole set
- For questlines, update both the quest and `Questlines.json`
- For daily quests, use `QuestTabType = "Daily"`
- If you change content, remember that player progress is stored separately in `PlayerInfo.json`

## What to verify after adding a quest

1. The quest appears in the correct tab
2. The quest can be started
3. Progress updates on the intended objective
4. `Target` and `MatchAliases` actually match real in-game events
5. Rewards are granted correctly
6. Cooldown behaves as expected
7. If it is a questline stage, the next stage unlocks after the previous one is completed
