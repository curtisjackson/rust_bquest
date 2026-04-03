# BQuest: инструкция по созданию квестов

Языковые версии: [EN](QUEST_AUTHORING.md)

Этот файл описывает, как создавать и редактировать квесты для `BQuest`, которые хранятся в папке `data/BQuest/`.

Документ основан на текущей логике плагина в `plugins/BQuest.cs`.

## Где что хранится

- `data/BQuest/Quest.json` — обычные квесты
- `data/BQuest/Daily.json` — квесты вкладки `Daily`
- `data/BQuest/Questlines.json` — цепочки квестов
- `data/BQuest/PlayerInfo.json` — прогресс игроков
- `data/BQuest/QuestStatistics.json` — агрегированная статистика

Для создания нового квеста обычно редактируются только:

- `data/BQuest/Quest.json`
- `data/BQuest/Daily.json`
- `data/BQuest/Questlines.json` — если квест входит в цепочку

## Структура файлов

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

## Основная логика

- Квест начинает прогрессироваться только после старта игроком, если `AllowManualStart = true`
- Прогресс идёт по массиву `Objectives`
- Награды задаются в `PrizeList`
- Если `IsRepeatable = true`, после завершения включается кулдаун из поля `Cooldown`
- Если `IsRepeatable = false`, после завершения квест остаётся завершённым
- Для вкладки `Daily` нет отдельного midnight-reset механизма: это обычные квесты с `QuestTabType = "Daily"`
- Если квест входит в цепочку, он может дополнительно ограничиваться `QuestlineId`, `QuestlineOrder` и `RequiredQuestIds`

## Поля квеста

Ниже перечислены поля `QuestDefinition`, которые можно задавать в JSON.

### Обязательные на практике

- `QuestID` — уникальный числовой ID квеста
- `QuestDisplayName` — название квеста
- `QuestType` — общий тип квеста
- `QuestCategory` — категория для группировки в UI
- `Objectives` — список целей
- `PrizeList` — список наград

### Часто используемые

- `QuestDescription` — описание квеста
- `QuestMissions` — краткий текст задачи
- `QuestTabType` — вкладка UI, обычно `Quests` или `Daily`
- `Icon` — иконка квеста или связанный токен
- `IsRepeatable` — можно ли проходить квест повторно
- `Cooldown` — кулдаун в секундах после завершения повторяемого квеста
- `AllowManualStart` — можно ли начать квест вручную из UI
- `AllowTrack` — можно ли отслеживать квест
- `AllowCancel` — можно ли отменить активный квест
- `AllowClaim` — можно ли вручную получить награду
- `ResetProgressOnWipe` — показывать предупреждение, что прогресс квеста сбрасывается при вайпе

### Права и ограничения

- `QuestPermission` — суффикс права, будет зарегистрировано как `BQuest.<QuestPermission>`
- `RequiredPermission` — внешнее право, которое уже должно быть у игрока
- `RequiredQuestIds` — список ID квестов, которые должны быть завершены
- `AvailabilityConditions` — дополнительные условия доступности
- `BlockConditions` — условия блокировки

### Поля цепочек

- `QuestlineId` — ID цепочки
- `QuestlineOrder` — порядок этапа внутри цепочки

Если указан `QuestlineId` и `QuestlineOrder > 1`, плагин дополнительно проверит завершение предыдущего этапа цепочки.

### Локализация

- `QuestDisplayNameMultiLanguage`
- `QuestDescriptionMultiLanguage`
- `QuestMissionsMultiLanguage`

Пример:

```json
"QuestDisplayNameMultiLanguage": {
  "ru": "Сборщик камня",
  "en": "Stone Collector"
}
```

Если локализованное поле заполнено, оно имеет приоритет над обычным текстом.

### Служебные и редкие поля

Эти поля есть в модели, но в текущем коде используются слабо или почти не влияют на поведение:

- `Target`
- `ActionCount`
- `IsReturnItemsRequired`
- `IsMultiLanguage`
- `VisibilityState`
- `UIBadges`

`Target` и `ActionCount` важны только как fallback, если массив `Objectives` вообще не задан. В этом случае плагин сам создаст одну цель на основе `QuestType`, `Target` и `ActionCount`.

## Поля цели

Каждая запись в `Objectives` описывает отдельную цель.

### Основные поля

- `ObjectiveId` — уникальный ID цели внутри квеста
- `Type` — тип цели
- `Target` — что именно нужно считать целью
- `TargetCount` — сколько нужно набрать
- `Description` — текст цели

### Поля сопоставления

- `MatchAliases` — список дополнительных вариантов совпадения
- `MatchMode` — способ сравнения: `shortname`, `contains`, `prefab`
- `TargetSkinId` — ограничение по skin ID

### UI-поля

- `Icon` — иконка цели
- `DescriptionMultiLanguage` — локализация описания
- `Hidden` — скрывает цель из основного отображения
- `Order` — порядок показа целей

### Поля сдачи предметов

- `SubmissionRequired` — цель требует сдачи предметов из инвентаря

Если квест состоит только из `SubmissionRequired`-целей, предметы можно сдавать сразу.

Если в квесте есть и обычные цели, и `SubmissionRequired`, то сначала должны быть выполнены обычные цели, и только потом станет доступна сдача предметов.

## Поддерживаемые типы целей

Тип цели задаётся в `Objectives[].Type`. Прогресс работает только если строка совпадает с именем типа, которое ожидает код.

### Стандартные типы Rust

- `Gather` — сбор ресурсов и предметов
- `Loot` — лутание сущностей
- `EntityKill` — убийство игроков/NPC/сущностей
- `Craft` — крафт предметов
- `Research` — исследование предметов
- `Grade` — улучшение строительных блоков
- `Swipe` — использование карт/ридеров
- `Deploy` — установка объектов
- `PurchaseFromNpc` — покупка у NPC/в vending
- `HackCrate` — взлом hackable crate
- `RecycleItem` — переработка предметов
- `Fishing` — ловля рыбы
- `Growseedlings` — сбор урожая/растений
- `Delivery` — сдача предметов через `SubmissionRequired`

### Типы для внешних плагинов и событий

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

### Как выбирать `Target`

Общее правило:

- для большинства типов `Target` должен совпадать с shortname, prefab name или другим токеном, который приходит в хук
- если совпадение нестабильное, добавляйте `MatchAliases`
- если нужна более мягкая проверка, используйте `MatchMode = "contains"`

Практические примеры:

- `Gather`: `wood`, `stones`, `metal.ore`
- `Craft`: `bandage`, `syringe.medical`
- `Deploy`: prefab/token установленного объекта
- `EntityKill`: `player`, `bear`, `wolf`, `scientist`, либо alias-список
- `Delivery`: shortname предмета, который игрок должен сдать
- `BossMonster`: можно использовать `BossMonster` или конкретные alias/token босса
- `AirEvent`, `HarborEvent`, `SupermarketEvent`: обычно `Target` совпадает с названием события

Для `RaidableBases` в `Target` приходит не название плагина, а сложность:

- `Easy`
- `Medium`
- `Hard`
- `Expert`
- `Nightmare`

## Условия доступности

В `AvailabilityConditions` и `BlockConditions` используются объекты вида:

```json
{
  "Type": "QuestCompleted",
  "Value": "1001"
}
```

Сейчас код поддерживает такие типы условий:

- `QuestCompleted`
- `QuestActive`
- `TrackedQuest`

Логика:

- `AvailabilityConditions` — все условия должны быть истинны
- `BlockConditions` — если хотя бы одно условие истинно, квест блокируется

## Поля награды

Каждая запись в `PrizeList` описывает отдельную награду.

### Основные поля

- `PrizeName` — отображаемое имя награды
- `PrizeType` — тип награды
- `ItemShortName` — shortname предмета
- `ItemAmount` — количество

### Дополнительные поля

- `ItemSkinID` — skin ID предмета
- `CustomItemName` — кастомное имя предмета для `CustomItem`
- `PrizeCommand` — команда сервера для `Command`
- `Icon` — иконка награды
- `IsHidden` — скрыть награду в UI

### Служебные поля

Есть в модели, но сейчас слабо влияют на поведение:

- `CommandImageUrl`
- `Description`
- `Rarity`

## Поддерживаемые типы наград

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

### Поведение наград

- `Item` — выдаёт обычный предмет
- `Blueprint` / `BlueprintItem` / `BlueprintReward` — выдаёт blueprint
- `CustomItem` — выдаёт предмет и может переименовать его через `CustomItemName`
- `Command` — выполняет серверную команду, в строке можно использовать `%STEAMID%`
- `RP` — сначала пытается выдать валюту через `Economics`, затем через `ServerRewards`
- `XP` — пытается выдать XP через `SkillTree` и также кладёт сумму в `Economics`
- `BlueprintFragments` / `Fragments` — выдаёт `blueprintfragment`

## Поля цепочек квестов

Файл `Questlines.json` использует структуру:

- `QuestlineId` — уникальный ID цепочки
- `DisplayName` — название цепочки
- `DisplayNameMultiLanguage` — локализация названия
- `Icon` — иконка цепочки
- `StageQuestIds` — список ID квестов-этапов в нужном порядке

Чтобы квест правильно отображался в цепочке:

1. Укажите у самого квеста `QuestlineId`
2. Укажите `QuestlineOrder`
3. Добавьте этот `QuestID` в `StageQuestIds`

## Минимальный пример обычного квеста

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

## Пример квеста на сдачу предметов

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

## Пример staged-submission квеста

Сначала игрок выполняет обычную цель, потом сдаёт предметы:

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

## Рекомендации

- Всегда задавайте уникальный `QuestID`
- Лучше всегда явно заполнять `Objectives`, а не полагаться на fallback через `Target` и `ActionCount`
- Для нестандартных NPC, боссов и сущностей используйте `MatchAliases`
- Для `EntityKill` сначала тестируйте совпадение на одном квесте, затем масштабируйте на серию
- Для цепочек обновляйте и сам квест, и `Questlines.json`
- Для daily-квестов используйте `QuestTabType = "Daily"`
- Если меняете контент, держите в уме, что прогресс игроков хранится отдельно в `PlayerInfo.json`

## Что проверять после добавления квеста

1. Квест виден в нужной вкладке
2. Квест можно начать
3. Прогресс идёт по нужной цели
4. `Target` и `MatchAliases` действительно совпадают с игровыми событиями
5. Награды выдаются корректно
6. Кулдаун работает как ожидается
7. Если это цепочка, следующий этап открывается после завершения предыдущего
