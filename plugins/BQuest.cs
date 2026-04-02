using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Plugins
{
    [Info("BQuest", "Codex", "1.0.0")]
    [Description("Modern quest system for Rust servers with quests, dailies, questlines, submission quests and safe optional integrations.")]
    public class BQuest : RustPlugin
    {
        [PluginReference] private Plugin IQChat;
        [PluginReference] private Plugin Notify;
        [PluginReference] private Plugin Friends;
        [PluginReference] private Plugin Clans;
        [PluginReference] private Plugin EventHelper;
        [PluginReference] private Plugin Battles;
        [PluginReference] private Plugin Duel;
        [PluginReference] private Plugin Duelist;
        [PluginReference] private Plugin ArenaTournament;
        [PluginReference] private Plugin SkillTree;
        [PluginReference] private Plugin CustomVendingSetup;
        [PluginReference] private Plugin MarkerManager;
        [PluginReference] private Plugin RaidableBases;
        [PluginReference] private Plugin IQDronePatrol;
        [PluginReference] private Plugin IQDefenderSupply;
        [PluginReference] private Plugin BossMonster;
        [PluginReference] private Plugin ServerRewards;
        [PluginReference] private Plugin Economics;

        private const string PermissionDefault = "BQuest.default";
        private const string MainUiName = "BQuest.UI.Main";
        private const string MiniUiName = "BQuest.UI.Mini";
        private const string ToastUiName = "BQuest.UI.Toast";
        private const string SettingsUiName = "BQuest.UI.Settings";
        private const string DataDir = "BQuest";
        private const string QuestlinesFile = "BQuest/Questlines";
        private const string DailyFile = "BQuest/Daily";
        private const string PlayerFile = "BQuest/PlayerInfo";
        private const string StatsFile = "BQuest/QuestStatistics";

        private PluginConfig _config;
        private StoredData _storedData;
        private QuestStatistics _statistics;

        private QuestCatalog _questCatalog;
        private DailyCatalog _dailyCatalog;
        private QuestlineCatalog _questlineCatalog;

        private readonly Dictionary<ulong, PlayerUiState> _uiStates = new Dictionary<ulong, PlayerUiState>();
        private readonly Dictionary<ulong, Timer> _toastTimers = new Dictionary<ulong, Timer>();

        private readonly Dictionary<long, QuestDefinition> _allQuestsById = new Dictionary<long, QuestDefinition>();
        private readonly Dictionary<long, QuestlineDefinition> _questlinesById = new Dictionary<long, QuestlineDefinition>();

        private Timer _cooldownTimer;
        private Timer _uiRefreshTimer;
        private Timer _statisticsTimer;

        private enum QuestStatus
        {
            Locked = 0,
            Available = 1,
            Started = 2,
            ReadyToClaim = 3,
            OnCooldown = 4,
            Completed = 5
        }

        private sealed class PluginConfig
        {
            [JsonProperty(PropertyName = "settings")]
            public GeneralSettings Settings = new GeneralSettings();

            [JsonProperty(PropertyName = "settingsIQChat")]
            public IQChatSettings SettingsIQChat = new IQChatSettings();

            [JsonProperty(PropertyName = "settingsNotify")]
            public NotifySettings SettingsNotify = new NotifySettings();

            [JsonProperty(PropertyName = "statisticsCollectionSettings")]
            public StatisticsSettings StatisticsCollectionSettings = new StatisticsSettings();

            [JsonProperty(PropertyName = "ui")]
            public UiSettings Ui = new UiSettings();

            [JsonProperty(PropertyName = "notifications")]
            public NotificationDefaults Notifications = new NotificationDefaults();

            [JsonProperty(PropertyName = "theme")]
            public ThemeSettings Theme = new ThemeSettings();
        }

        private sealed class GeneralSettings
        {
            public bool SoundEffect = true;
            public string Effect = "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab";
            public bool useWipe = true;
            public bool useWipePermission = true;
            public string questListDataName = "Quest";
            public List<string> questListProgress = new List<string> { "qlist", "quest" };
            public bool sandNotifyAllPlayer = false;
            public bool UseSkillTreeIgnoreHooks = false;
            public int questCount = 3;
            public bool EnableAnimations = true;
            public string DefaultNotificationMode = "ToastAndChat";
            public string NotificationPosition = "TopRight";
            public int CooldownListLimit = 5;
            public int TrackedQuestLimit = 1;
            public string UITheme = "Golden";
        }

        private sealed class IQChatSettings
        {
            public string CustomPrefix = "Quest";
            public string CustomAvatar = "0";
            public bool UIAlertUse = false;
        }

        private sealed class NotifySettings
        {
            public bool useNotify = false;
            public int typeNotify = 0;
        }

        private sealed class StatisticsSettings
        {
            public bool useStatistics = false;
            public string discordWebhookUrl = "";
            public int publishFrequency = 21600;
        }

        private sealed class UiSettings
        {
            public int QuestsPerPage = 9;
            public int MiniListPerPage = 8;
            public bool ShowLockedQuests = true;
            public bool ShowCompletedInList = true;
        }

        private sealed class NotificationDefaults
        {
            public bool ProgressToasts = true;
            public bool ChatMessages = true;
            public bool AutoOpenMiniList = false;
        }

        private sealed class ThemeSettings
        {
            public string Background = "0.07 0.08 0.10 0.97";
            public string Panel = "0.11 0.12 0.15 0.96";
            public string PanelAlt = "0.16 0.17 0.21 0.96";
            public string Accent = "0.90 0.72 0.23 1.0";
            public string AccentSoft = "0.72 0.58 0.22 0.65";
            public string Danger = "0.76 0.26 0.23 1.0";
            public string Warning = "0.93 0.57 0.20 1.0";
            public string Success = "0.24 0.71 0.36 1.0";
            public string Text = "0.95 0.95 0.95 1.0";
            public string TextMuted = "0.70 0.72 0.76 1.0";
            public string Locked = "0.32 0.33 0.37 1.0";
            public string Overlay = "0 0 0 0.45";
        }

        private sealed class QuestCatalog
        {
            public List<QuestDefinition> Quests = new List<QuestDefinition>();
        }

        private sealed class DailyCatalog
        {
            public List<QuestDefinition> DailyQuests = new List<QuestDefinition>();
        }

        private sealed class QuestlineCatalog
        {
            public List<QuestlineDefinition> Questlines = new List<QuestlineDefinition>();
        }

        private sealed class QuestDefinition
        {
            public long QuestID;
            public string QuestDisplayName = "";
            public Dictionary<string, string> QuestDisplayNameMultiLanguage = new Dictionary<string, string>();
            public string QuestDescription = "";
            public Dictionary<string, string> QuestDescriptionMultiLanguage = new Dictionary<string, string>();
            public string QuestMissions = "";
            public Dictionary<string, string> QuestMissionsMultiLanguage = new Dictionary<string, string>();
            public string QuestType = "Generic";
            public string QuestCategory = "General";
            public string QuestTabType = "Quests";
            public string Icon = "";
            public string Target = "";
            public int ActionCount = 1;
            public List<ObjectiveDefinition> Objectives = new List<ObjectiveDefinition>();
            public List<PrizeDefinition> PrizeList = new List<PrizeDefinition>();
            public string QuestPermission = "";
            public bool IsRepeatable;
            public int Cooldown;
            public bool IsReturnItemsRequired;
            public bool ResetProgressOnWipe = true;
            public bool AllowManualStart = true;
            public bool AllowTrack = true;
            public bool AllowCancel = true;
            public bool AllowClaim = true;
            public bool IsMultiLanguage = true;
            public long QuestlineId;
            public int QuestlineOrder;
            public string VisibilityState = "Visible";
            public List<ConditionDefinition> AvailabilityConditions = new List<ConditionDefinition>();
            public List<ConditionDefinition> BlockConditions = new List<ConditionDefinition>();
            public List<long> RequiredQuestIds = new List<long>();
            public string RequiredPermission = "";
            public List<string> UIBadges = new List<string>();
        }

        private sealed class ObjectiveDefinition
        {
            public string ObjectiveId = "";
            public string Type = "Gather";
            public string Target = "";
            public List<string> MatchAliases = new List<string>();
            public ulong TargetSkinId;
            public int TargetCount = 1;
            public string Icon = "";
            public string Description = "";
            public Dictionary<string, string> DescriptionMultiLanguage = new Dictionary<string, string>();
            public bool Hidden;
            public int Order;
            public string MatchMode = "shortname";
            public bool SubmissionRequired;
        }

        private sealed class PrizeDefinition
        {
            public string PrizeName = "";
            public string PrizeType = "Item";
            public string ItemShortName = "";
            public int ItemAmount = 1;
            public ulong ItemSkinID;
            public string CustomItemName = "";
            public string PrizeCommand = "";
            public string CommandImageUrl = "";
            public string Icon = "";
            public string Description = "";
            public bool IsHidden;
            public string Rarity = "Common";
        }

        private sealed class QuestlineDefinition
        {
            public long QuestlineId;
            public string DisplayName = "";
            public Dictionary<string, string> DisplayNameMultiLanguage = new Dictionary<string, string>();
            public string Icon = "";
            public List<long> StageQuestIds = new List<long>();
        }

        private sealed class ConditionDefinition
        {
            public string Type = "";
            public string Value = "";
        }

        private sealed class StoredData
        {
            public Dictionary<string, PlayerProgress> Players = new Dictionary<string, PlayerProgress>();
        }

        private sealed class PlayerProgress
        {
            public Dictionary<long, ActiveQuestRecord> ActiveQuests = new Dictionary<long, ActiveQuestRecord>();
            public HashSet<long> CompletedQuests = new HashSet<long>();
            public Dictionary<long, double> Cooldowns = new Dictionary<long, double>();
            public long TrackedQuestId;
            public NotificationSettings NotificationSettings = new NotificationSettings();
            public Dictionary<long, bool> ClaimedRewards = new Dictionary<long, bool>();
        }

        private sealed class NotificationSettings
        {
            public bool ProgressToasts = true;
            public bool ChatMessages = true;
            public bool MiniListAutoShow;
        }

        private sealed class ActiveQuestRecord
        {
            public long QuestID;
            public double StartedAt;
            public Dictionary<string, int> ObjectiveProgress = new Dictionary<string, int>();
        }

        private sealed class QuestStatistics
        {
            public int CompletedTasks;
            public int TakenTasks;
            public int DeclinedTasks;
            public Dictionary<long, int> TaskExecutionCounts = new Dictionary<long, int>();
        }

        private sealed class PlayerUiState
        {
            public string Tab = "Quests";
            public string Filter = "All";
            public bool FilterExpanded;
            public int Page;
            public int MiniPage;
            public long SelectedQuestId;
            public long SelectedQuestlineId;
            public bool MainOpen;
            public bool MiniOpen;
            public bool SettingsOpen;
            public HashSet<string> CollapsedGroups = new HashSet<string>();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
        }

        private void Init()
        {
            LoadConfigValues();
            LoadData();
            LoadQuestData();
            NormalizeQuestData();
            RegisterLang();
            permission.RegisterPermission(PermissionDefault, this);
            RegisterQuestPermissions();

            cmd.AddChatCommand("q", this, nameof(CmdOpenMain));
            AddCovalenceCommand("bquest.player.reset", nameof(CmdResetPlayer));
            AddCovalenceCommand("bquest.stat", nameof(CmdPushStatistics));
            cmd.AddConsoleCommand("UI_Handler", this, nameof(ConsoleUiHandler));
            cmd.AddConsoleCommand("CloseMiniQuestList", this, nameof(ConsoleCloseMini));
            cmd.AddConsoleCommand("CloseMainUI", this, nameof(ConsoleCloseMain));
        }

        private void OnServerInitialized()
        {
            ValidateQuestData();
            StartTimers();
            foreach (var player in BasePlayer.activePlayerList)
            {
                EnsurePlayerData(player.userID);
            }
        }

        private void OnServerSave()
        {
            SaveAllData();
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyMainUi(player);
                DestroyMiniUi(player);
                DestroyToast(player);
                DestroySettingsUi(player);
            }

            if (_cooldownTimer != null)
            {
                _cooldownTimer.Destroy();
            }

            if (_uiRefreshTimer != null)
            {
                _uiRefreshTimer.Destroy();
            }

            if (_statisticsTimer != null)
            {
                _statisticsTimer.Destroy();
            }

            SaveAllData();
        }

        private void OnNewSave(string filename)
        {
            if (_config.Settings.useWipe)
            {
                _storedData = new StoredData();
                SavePlayerData();
                Puts("Player quest progress has been wiped because useWipe=true.");
            }

            if (_config.Settings.useWipePermission)
            {
                RevokeQuestPermissions();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DestroyMainUi(player);
            DestroyMiniUi(player);
            DestroyToast(player);
            DestroySettingsUi(player);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            EnsurePlayerData(player.userID);
        }

        private void StartTimers()
        {
            if (_cooldownTimer != null)
            {
                _cooldownTimer.Destroy();
            }

            _cooldownTimer = timer.Every(70f, TickCooldowns);

            if (_uiRefreshTimer != null)
            {
                _uiRefreshTimer.Destroy();
            }

            _uiRefreshTimer = timer.Every(1f, TickRealtimeUi);

            if (_statisticsTimer != null)
            {
                _statisticsTimer.Destroy();
            }

            if (_config.StatisticsCollectionSettings.useStatistics && !string.IsNullOrEmpty(_config.StatisticsCollectionSettings.discordWebhookUrl))
            {
                _statisticsTimer = timer.Every(_config.StatisticsCollectionSettings.publishFrequency, PublishStatistics);
            }
        }

        private void LoadConfigValues()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
            }
            catch
            {
                PrintWarning("Configuration file is invalid. Regenerating default config.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void LoadData()
        {
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(PlayerFile);
            _statistics = Interface.Oxide.DataFileSystem.ReadObject<QuestStatistics>(StatsFile);

            if (_storedData == null)
            {
                _storedData = new StoredData();
            }

            if (_statistics == null)
            {
                _statistics = new QuestStatistics();
            }
        }

        private void LoadQuestData()
        {
            var questPath = string.Format("{0}/{1}", DataDir, _config.Settings.questListDataName);
            _questCatalog = LoadDataFile<QuestCatalog>(questPath, BuildDefaultQuestCatalog());
            _dailyCatalog = LoadDataFile<DailyCatalog>(DailyFile, BuildDefaultDailyCatalog());
            _questlineCatalog = LoadDataFile<QuestlineCatalog>(QuestlinesFile, BuildDefaultQuestlineCatalog());
        }

        private T LoadDataFile<T>(string path, T fallback) where T : class
        {
            T data = null;
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<T>(path);
            }
            catch (Exception ex)
            {
                PrintWarning(string.Format("Failed to load data file {0}: {1}", path, ex.Message));
            }

            if (data == null)
            {
                data = fallback;
                Interface.Oxide.DataFileSystem.WriteObject(path, fallback);
            }

            return data;
        }

        private void SaveAllData()
        {
            SavePlayerData();
            SaveStatistics();
        }

        private void SavePlayerData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(PlayerFile, _storedData);
        }

        private void SaveStatistics()
        {
            Interface.Oxide.DataFileSystem.WriteObject(StatsFile, _statistics);
        }

        private void NormalizeQuestData()
        {
            _allQuestsById.Clear();
            _questlinesById.Clear();

            foreach (var quest in _questCatalog.Quests)
            {
                NormalizeQuest(quest, "Quests");
            }

            foreach (var quest in _dailyCatalog.DailyQuests)
            {
                NormalizeQuest(quest, "Daily");
            }

            foreach (var line in _questlineCatalog.Questlines)
            {
                if (!_questlinesById.ContainsKey(line.QuestlineId))
                {
                    _questlinesById.Add(line.QuestlineId, line);
                }
            }
        }

        private void NormalizeQuest(QuestDefinition quest, string tabFallback)
        {
            if (quest == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(quest.QuestTabType))
            {
                quest.QuestTabType = tabFallback;
            }

            if (quest.Objectives == null || quest.Objectives.Count == 0)
            {
                quest.Objectives = new List<ObjectiveDefinition>
                {
                    new ObjectiveDefinition
                    {
                        ObjectiveId = string.Format("{0}_default", quest.QuestID),
                        Type = quest.QuestType,
                        Target = quest.Target,
                        TargetCount = quest.ActionCount > 0 ? quest.ActionCount : 1,
                        Description = quest.QuestMissions
                    }
                };
            }

            if (quest.PrizeList == null)
            {
                quest.PrizeList = new List<PrizeDefinition>();
            }

            for (var i = 0; i < quest.Objectives.Count; i++)
            {
                var objective = quest.Objectives[i];
                if (string.IsNullOrEmpty(objective.ObjectiveId))
                {
                    objective.ObjectiveId = string.Format("{0}_{1}", quest.QuestID, i);
                }

                if (objective.TargetCount <= 0)
                {
                    objective.TargetCount = 1;
                }
            }

            _allQuestsById[quest.QuestID] = quest;
        }

        private void RegisterLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Title"] = "BQuest",
                ["Tab.Quests"] = "Quests",
                ["Tab.Daily"] = "Daily",
                ["Tab.Questlines"] = "Chains",
                ["Filter.All"] = "All",
                ["Filter.Available"] = "Available",
                ["Filter.Started"] = "Started",
                ["Filter.ReadyToClaim"] = "Ready to Claim",
                ["Filter.Completed"] = "Completed",
                ["Filter.Locked"] = "Locked",
                ["Filter.OnCooldown"] = "On Cooldown",
                ["Status.Available"] = "Available",
                ["Status.Started"] = "Started",
                ["Status.ReadyToClaim"] = "Ready to Claim",
                ["Status.Completed"] = "Completed",
                ["Status.Locked"] = "Locked",
                ["Status.OnCooldown"] = "On Cooldown",
                ["Action.Start"] = "Start Quest",
                ["Action.Cancel"] = "Cancel Quest",
                ["Action.Track"] = "Track Quest",
                ["Action.Untrack"] = "Untrack",
                ["Action.Claim"] = "Claim Reward",
                ["Action.Submit"] = "Submit Items",
                ["Action.Settings"] = "Settings",
                ["Action.Close"] = "Close",
                ["Action.OpenMini"] = "Mini List",
                ["Action.SettingsShort"] = "Settings",
                ["Action.CloseShort"] = "Close",
                ["Action.OpenMiniShort"] = "Mini",
                ["Action.Back"] = "Back",
                ["Label.Objectives"] = "Objectives",
                ["Label.Rewards"] = "Rewards",
                ["Label.QuestRewards"] = "Quest Rewards",
                ["Label.FilterState"] = "Click To Filter By State",
                ["Label.Tracked"] = "Tracked Quest",
                ["Label.Cooldowns"] = "Upcoming Cooldowns",
                ["Label.Questlines"] = "Chain Progress",
                ["Label.Settings"] = "Notification Settings",
                ["Label.ProgressToasts"] = "Progress Toasts",
                ["Label.ChatMessages"] = "Chat Messages",
                ["Label.MiniAuto"] = "Auto-open Mini List",
                ["Label.Overview"] = "Back to Overview",
                ["Label.NotInQuestline"] = "Not part of a Chain",
                ["Label.ProgressResetOnWipe"] = "Warning! This Quest's Progress resets on Wipe",
                ["Label.NoRewards"] = "No rewards configured.",
                ["Label.NoObjectives"] = "No objectives configured.",
                ["Label.RewardCount"] = "{0} Rewards",
                ["Msg.FooterHint"] = "Start the Quest to begin progressing through the Objectives",
                ["Label.ActiveSummary"] = "Active",
                ["Label.CompletedSummary"] = "Completed",
                ["Label.TrackedSummary"] = "Tracked",
                ["Label.Stages"] = "Chain Stages",
                ["Badge.DailyQuest"] = "Daily Quest",
                ["Badge.Questline"] = "Chain",
                ["Badge.StandardQuest"] = "Quest",
                ["Msg.NoPermission"] = "You do not have permission for this quest.",
                ["Msg.QuestStarted"] = "Quest started: {0}",
                ["Msg.QuestCancelled"] = "Quest cancelled: {0}",
                ["Msg.QuestClaimed"] = "Quest completed: {0}",
                ["Msg.QuestTracked"] = "Tracked quest updated: {0}",
                ["Msg.QuestReady"] = "Quest is ready to claim: {0}",
                ["Msg.CooldownFinished"] = "Quest is available again: {0}",
                ["Msg.NothingToSubmit"] = "You do not have the required items to submit.",
                ["Msg.Submitted"] = "Submitted progress for quest: {0}",
                ["Msg.MaxActive"] = "You have reached the active quest limit.",
                ["Msg.AlreadyActive"] = "This quest is already active.",
                ["Msg.CannotStart"] = "This quest cannot be started right now.",
                ["Msg.NotReady"] = "Quest is not ready to claim.",
                ["Msg.ResetDone"] = "Quest progress has been reset for player {0}.",
                ["Msg.StatQueued"] = "Statistics publishing started.",
                ["Msg.UnknownQuest"] = "Quest not found.",
                ["Msg.NoActiveQuests"] = "No active quests."
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Title"] = "BQuest",
                ["Tab.Quests"] = "Квесты",
                ["Tab.Daily"] = "Ежедневные",
                ["Tab.Questlines"] = "Цепочки",
                ["Filter.All"] = "Все",
                ["Filter.Available"] = "Доступные",
                ["Filter.Started"] = "Активные",
                ["Filter.ReadyToClaim"] = "Готовы",
                ["Filter.Completed"] = "Завершенные",
                ["Filter.Locked"] = "Заблокированные",
                ["Filter.OnCooldown"] = "Откат",
                ["Status.Available"] = "Доступен",
                ["Status.Started"] = "Активен",
                ["Status.ReadyToClaim"] = "Готов к сдаче",
                ["Status.Completed"] = "Завершен",
                ["Status.Locked"] = "Заблокирован",
                ["Status.OnCooldown"] = "На откате",
                ["Action.Start"] = "Начать квест",
                ["Action.Cancel"] = "Отменить квест",
                ["Action.Track"] = "Отслеживать",
                ["Action.Untrack"] = "Убрать из трекинга",
                ["Action.Claim"] = "Получить награду",
                ["Action.Submit"] = "Сдать предметы",
                ["Action.Settings"] = "Настройки",
                ["Action.Close"] = "Закрыть",
                ["Action.OpenMini"] = "Мини-список",
                ["Action.SettingsShort"] = "Настр",
                ["Action.CloseShort"] = "Закрыть",
                ["Action.OpenMiniShort"] = "Мини",
                ["Action.Back"] = "Назад",
                ["Label.Objectives"] = "Цели",
                ["Label.Rewards"] = "Награды",
                ["Label.QuestRewards"] = "Quest Rewards",
                ["Label.FilterState"] = "Нажмите, чтобы фильтровать по статусу",
                ["Label.Tracked"] = "Отслеживаемый квест",
                ["Label.Cooldowns"] = "Ближайшие кулдауны",
                ["Label.Questlines"] = "Прогресс цепочки",
                ["Label.Settings"] = "Настройки уведомлений",
                ["Label.ProgressToasts"] = "Всплывающий прогресс",
                ["Label.ChatMessages"] = "Сообщения в чат",
                ["Label.MiniAuto"] = "Автооткрытие мини-списка",
                ["Label.Overview"] = "Назад к обзору",
                ["Label.NotInQuestline"] = "Не входит в цепочку",
                ["Label.ProgressResetOnWipe"] = "Внимание! Прогресс этого квеста сбрасывается при вайпе",
                ["Label.NoRewards"] = "Награды не настроены.",
                ["Label.NoObjectives"] = "Цели не настроены.",
                ["Label.RewardCount"] = "{0} Rewards",
                ["Msg.FooterHint"] = "Start the Quest to begin progressing through the Objectives",
                ["Label.ActiveSummary"] = "Активно",
                ["Label.CompletedSummary"] = "Завершено",
                ["Label.TrackedSummary"] = "Трекинг",
                ["Label.Stages"] = "Этапы цепочки",
                ["Badge.DailyQuest"] = "Daily Quest",
                ["Badge.Questline"] = "Цепочка",
                ["Badge.StandardQuest"] = "Квест",
                ["Msg.NoPermission"] = "У вас нет прав на этот квест.",
                ["Msg.QuestStarted"] = "Квест начат: {0}",
                ["Msg.QuestCancelled"] = "Квест отменен: {0}",
                ["Msg.QuestClaimed"] = "Квест завершен: {0}",
                ["Msg.QuestTracked"] = "Отслеживаемый квест обновлен: {0}",
                ["Msg.QuestReady"] = "Квест готов к получению награды: {0}",
                ["Msg.CooldownFinished"] = "Квест снова доступен: {0}",
                ["Msg.NothingToSubmit"] = "У вас нет подходящих предметов для сдачи.",
                ["Msg.Submitted"] = "Прогресс по сдаче обновлен: {0}",
                ["Msg.MaxActive"] = "Достигнут лимит активных квестов.",
                ["Msg.AlreadyActive"] = "Этот квест уже активен.",
                ["Msg.CannotStart"] = "Сейчас нельзя начать этот квест.",
                ["Msg.NotReady"] = "Квест еще не готов к сдаче.",
                ["Msg.ResetDone"] = "Прогресс квестов сброшен для игрока {0}.",
                ["Msg.StatQueued"] = "Публикация статистики запущена.",
                ["Msg.UnknownQuest"] = "Квест не найден.",
                ["Msg.NoActiveQuests"] = "Нет активных квестов."
            }, this, "ru");
        }

        private void RegisterQuestPermissions()
        {
            foreach (var quest in _allQuestsById.Values)
            {
                if (!string.IsNullOrEmpty(quest.QuestPermission))
                {
                    permission.RegisterPermission(GetQuestPermission(quest.QuestPermission), this);
                }
            }
        }

        private void RevokeQuestPermissions()
        {
            foreach (var quest in _allQuestsById.Values)
            {
                if (string.IsNullOrEmpty(quest.QuestPermission))
                {
                    continue;
                }

                var perm = GetQuestPermission(quest.QuestPermission);
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (permission.UserHasPermission(player.UserIDString, perm))
                    {
                        permission.RevokeUserPermission(player.UserIDString, perm);
                    }
                }
            }
        }

        private void ValidateQuestData()
        {
            var duplicateIds = _allQuestsById.Values
                .GroupBy(x => x.QuestID)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToList();

            if (duplicateIds.Count > 0)
            {
                PrintWarning(string.Format("Duplicate quest ids found: {0}", string.Join(", ", duplicateIds.ToArray())));
            }

            foreach (var line in _questlineCatalog.Questlines)
            {
                foreach (var stageId in line.StageQuestIds)
                {
                    if (!_allQuestsById.ContainsKey(stageId))
                    {
                        PrintWarning(string.Format("Questline {0} references missing quest id {1}.", line.QuestlineId, stageId));
                    }
                }
            }
        }

        private void CmdOpenMain(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }

            OpenMainUi(player);
        }

        private void CmdToggleMini(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }

            var state = GetUiState(player.userID);
            state.MiniOpen = !state.MiniOpen;

            if (state.MiniOpen)
            {
                RenderMiniUi(player);
            }
            else
            {
                DestroyMiniUi(player);
            }
        }

        private void CmdResetPlayer(IPlayer caller, string command, string[] args)
        {
            if (!caller.IsAdmin)
            {
                caller.Reply("Admin only.");
                return;
            }

            if (args.Length == 0)
            {
                caller.Reply("Usage: bquest.player.reset <steamid64>");
                return;
            }

            PlayerProgress progress;
            if (_storedData.Players.TryGetValue(args[0], out progress))
            {
                _storedData.Players[args[0]] = BuildDefaultPlayerProgress();
                SavePlayerData();
            }

            caller.Reply(string.Format(GetMsg("Msg.ResetDone", null), args[0]));
        }

        private void CmdPushStatistics(IPlayer caller, string command, string[] args)
        {
            if (!caller.IsAdmin)
            {
                caller.Reply("Admin only.");
                return;
            }

            PublishStatistics();
            caller.Reply(GetMsg("Msg.StatQueued", null));
        }

        private void ConsoleCloseMini(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                return;
            }

            var state = GetUiState(player.userID);
            state.MiniOpen = false;
            DestroyMiniUi(player);
        }

        private void ConsoleCloseMain(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                return;
            }

            DestroyMainUi(player);
            DestroySettingsUi(player);
            GetUiState(player.userID).MainOpen = false;
        }

        private void ConsoleUiHandler(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                return;
            }

            var action = arg.GetString(0, string.Empty);
            var value = arg.GetString(1, string.Empty);
            HandleUiAction(player, action, value);
        }

        private void HandleUiAction(BasePlayer player, string action, string value)
        {
            var state = GetUiState(player.userID);
            var refreshSidebar = true;
            var refreshDetails = true;

            switch (action)
            {
                case "tab":
                    state.Tab = value;
                    state.Page = 0;
                    state.FilterExpanded = false;
                    if (value == "Questlines")
                    {
                        state.SelectedQuestlineId = _questlineCatalog.Questlines.Count > 0 ? _questlineCatalog.Questlines[0].QuestlineId : 0;
                        QuestlineDefinition firstLine;
                        if (_questlinesById.TryGetValue(state.SelectedQuestlineId, out firstLine) && firstLine.StageQuestIds.Count > 0)
                        {
                            state.SelectedQuestId = firstLine.StageQuestIds[0];
                        }
                        else
                        {
                            state.SelectedQuestId = 0;
                        }
                    }
                    else
                    {
                        state.SelectedQuestlineId = 0;
                        state.SelectedQuestId = GetFirstSelectableId(player, value);
                    }
                    break;
                case "filter":
                    state.Filter = value;
                    state.Page = 0;
                    state.FilterExpanded = false;
                    break;
                case "togglefilter":
                    state.FilterExpanded = !state.FilterExpanded;
                    refreshDetails = false;
                    break;
                case "filterstep":
                    state.Filter = GetNextFilterValue(state.Filter, value == "next");
                    state.Page = 0;
                    state.FilterExpanded = false;
                    break;
                case "togglegroup":
                    ToggleQuestGroup(state, value);
                    state.FilterExpanded = false;
                    refreshDetails = false;
                    break;
                case "select":
                    state.SelectedQuestId = ToLong(value);
                    state.FilterExpanded = false;
                    break;
                case "selectline":
                    state.SelectedQuestlineId = ToLong(value);
                    state.FilterExpanded = false;
                    QuestlineDefinition selectedLine;
                    if (_questlinesById.TryGetValue(state.SelectedQuestlineId, out selectedLine) && selectedLine.StageQuestIds.Count > 0)
                    {
                        state.SelectedQuestId = selectedLine.StageQuestIds[0];
                    }
                    break;
                case "linequest":
                    state.Tab = "Questlines";
                    state.SelectedQuestId = ToLong(value);
                    state.FilterExpanded = false;
                    QuestDefinition selectedQuest;
                    if (_allQuestsById.TryGetValue(state.SelectedQuestId, out selectedQuest))
                    {
                        state.SelectedQuestlineId = selectedQuest.QuestlineId;
                    }
                    break;
                case "overview":
                    state.FilterExpanded = false;
                    if (state.Tab == "Questlines")
                    {
                        QuestlineDefinition overviewLine;
                        if (_questlinesById.TryGetValue(state.SelectedQuestlineId, out overviewLine) && overviewLine.StageQuestIds.Count > 0)
                        {
                            state.SelectedQuestId = overviewLine.StageQuestIds[0];
                        }
                    }
                    else
                    {
                        state.SelectedQuestId = GetFirstSelectableId(player, state.Tab);
                    }
                    break;
                case "page":
                    state.Page = Math.Max(0, state.Page + (value == "next" ? 1 : -1));
                    state.FilterExpanded = false;
                    break;
                case "minipage":
                    state.MiniPage = Math.Max(0, state.MiniPage + (value == "next" ? 1 : -1));
                    RenderMiniUi(player);
                    return;
                case "start":
                    StartQuest(player, ToLong(value));
                    break;
                case "cancel":
                    CancelQuest(player, ToLong(value));
                    break;
                case "track":
                    SetTrackedQuest(player, ToLong(value));
                    break;
                case "untrack":
                    SetTrackedQuest(player, 0L);
                    break;
                case "claim":
                    ClaimQuest(player, ToLong(value));
                    break;
                case "submit":
                    SubmitQuestItems(player, ToLong(value));
                    break;
                case "settings":
                    state.SettingsOpen = !state.SettingsOpen;
                    refreshSidebar = false;
                    refreshDetails = false;
                    break;
                case "toggle_minilist":
                    state.MiniOpen = !state.MiniOpen;
                    refreshSidebar = false;
                    refreshDetails = false;
                    if (state.MiniOpen)
                    {
                        RenderMiniUi(player);
                    }
                    else
                    {
                        DestroyMiniUi(player);
                    }
                    break;
                case "toggle_toast":
                    GetPlayerProgress(player.userID).NotificationSettings.ProgressToasts = !GetPlayerProgress(player.userID).NotificationSettings.ProgressToasts;
                    refreshSidebar = false;
                    refreshDetails = false;
                    break;
                case "toggle_chat":
                    GetPlayerProgress(player.userID).NotificationSettings.ChatMessages = !GetPlayerProgress(player.userID).NotificationSettings.ChatMessages;
                    refreshSidebar = false;
                    refreshDetails = false;
                    break;
                case "toggle_miniauto":
                    GetPlayerProgress(player.userID).NotificationSettings.MiniListAutoShow = !GetPlayerProgress(player.userID).NotificationSettings.MiniListAutoShow;
                    refreshSidebar = false;
                    refreshDetails = false;
                    break;
            }

            RefreshMainUi(player, refreshSidebar, refreshDetails);
            if (state.MiniOpen)
            {
                RenderMiniUi(player);
            }
        }

        private void RefreshMainUi(BasePlayer player, bool refreshSidebar = true, bool refreshDetails = true)
        {
            var state = GetUiState(player.userID);
            if (!state.MainOpen)
            {
                OpenMainUi(player);
                return;
            }

            EnsurePlayerData(player.userID);
            EnsureValidMainSelection(player);

            var rootName = MainUiName + ".Root";
            var container = new CuiElementContainer();
            if (refreshSidebar)
            {
                CuiHelper.DestroyUi(player, rootName + ".Sidebar");
                RenderQuestList(container, player, rootName);
            }

            if (refreshDetails)
            {
                CuiHelper.DestroyUi(player, rootName + ".Details");
                RenderQuestDetails(container, player, rootName);
            }

            CuiHelper.AddUi(player, container);

            if (state.SettingsOpen)
            {
                RenderSettingsUi(player);
            }
            else
            {
                DestroySettingsUi(player);
            }
        }

        private void RefreshFooterUi(BasePlayer player)
        {
            var state = GetUiState(player.userID);
            if (!state.MainOpen)
            {
                return;
            }

            EnsurePlayerData(player.userID);
            EnsureValidMainSelection(player);

            QuestDefinition quest;
            if (!_allQuestsById.TryGetValue(state.SelectedQuestId, out quest))
            {
                return;
            }

            var detailName = MainUiName + ".Root.Details";
            CuiHelper.DestroyUi(player, detailName + ".Footer");

            var container = new CuiElementContainer();
            RenderFooterStrip(container, player, detailName, quest, GetQuestStatus(player, quest), null);
            CuiHelper.AddUi(player, container);
        }

        private void OpenMainUi(BasePlayer player)
        {
            EnsurePlayerData(player.userID);

            var state = GetUiState(player.userID);
            state.MainOpen = true;
            EnsureValidMainSelection(player);

            DestroyMainUi(player);
            DestroySettingsUi(player);

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = _config.Theme.Overlay },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true
            }, "Hud", MainUiName);

            var rootName = MainUiName + ".Root";
            AddPanel(container, MainUiName, rootName, GetUiWindowColor(), "0.05 0.10", "0.95 0.96");
            AddLine(container, MainUiName, GetUiGoldFrameColor(), "0.05 0.959", "0.95 0.960");
            AddLine(container, MainUiName, GetUiGoldFrameColor(), "0.05 0.100", "0.95 0.101");
            AddLine(container, MainUiName, GetUiGoldFrameColor(), "0.050 0.10", "0.051 0.96");
            AddLine(container, MainUiName, GetUiGoldFrameColor(), "0.949 0.10", "0.950 0.96");
            AddLabel(container, rootName, GetMsg("Title", player.UserIDString), 15, _config.Theme.Text, "0.015 0.94", "0.16 0.98", TextAnchor.MiddleLeft);
            AddStyledButton(container, rootName, GetUiSoftColor(), "X", 12, GetUiGoldColor(), "0.94 0.94", "0.98 0.98", "CloseMainUI");

            RenderQuestList(container, player, rootName);
            RenderQuestDetails(container, player, rootName);

            CuiHelper.AddUi(player, container);

            if (state.SettingsOpen)
            {
                RenderSettingsUi(player);
            }
        }

        private void RenderTabs(CuiElementContainer container, BasePlayer player, string parent)
        {
            var state = GetUiState(player.userID);
            var tabs = new[] { "Quests", "Daily", "Questlines" };
            var sidebar = parent + ".Sidebar";
            var startX = 0.08f;

            for (var i = 0; i < tabs.Length; i++)
            {
                var isActive = string.Equals(state.Tab, tabs[i], StringComparison.OrdinalIgnoreCase);
                var tabMin = startX + (i * 0.29f);
                var tabMax = tabMin + 0.22f;
                AddLabel(container, sidebar, GetMsg("Tab." + tabs[i], player.UserIDString), 12, isActive ? GetUiGoldColor() : _config.Theme.TextMuted, string.Format(CultureInfo.InvariantCulture, "{0} 0.72", tabMin), string.Format(CultureInfo.InvariantCulture, "{0} 0.78", tabMax), TextAnchor.MiddleCenter);
                AddOverlayButton(container, sidebar, "UI_Handler tab " + tabs[i], string.Format(CultureInfo.InvariantCulture, "{0} 0.72", tabMin), string.Format(CultureInfo.InvariantCulture, "{0} 0.78", tabMax));
                if (isActive)
                {
                    AddLine(container, sidebar, GetUiGoldColor(), string.Format(CultureInfo.InvariantCulture, "{0} 0.705", tabMin + 0.02f), string.Format(CultureInfo.InvariantCulture, "{0} 0.708", tabMax - 0.02f));
                }
            }

            AddThinPanel(container, sidebar, sidebar + ".Filter", GetUiSoftColor(), GetUiBorderColor(), "0.05 0.63", "0.95 0.68");
            AddSquarePanel(container, sidebar + ".Filter", sidebar + ".Filter.Icon", "0 0 0 0", 0.05f, 0.50f, 22f);
            AddFilterIcon(container, sidebar + ".Filter.Icon", state.Filter, GetUiGoldColor());
            AddLabel(container, sidebar + ".Filter", GetFilterDisplayText(player, state.Filter), 10, _config.Theme.TextMuted, "0.10 0.10", "0.78 0.90", TextAnchor.MiddleLeft);
            AddLabel(container, sidebar + ".Filter", state.FilterExpanded ? "^" : "v", 11, _config.Theme.TextMuted, "0.88 0.08", "0.96 0.92", TextAnchor.MiddleCenter);
            AddOverlayButton(container, sidebar + ".Filter", "UI_Handler togglefilter 1");

            if (state.FilterExpanded)
            {
                RenderFilterDropdown(container, player, sidebar);
            }
        }

        private void RenderFilterDropdown(CuiElementContainer container, BasePlayer player, string parent)
        {
            var state = GetUiState(player.userID);
            var filters = GetFilterValues();
            var height = filters.Length * 0.048f;
            var minY = Math.Max(0.26f, 0.63f - height);
            AddThinPanel(container, parent, parent + ".FilterDropdown", GetUiRowColor(), GetUiBorderColor(), string.Format(CultureInfo.InvariantCulture, "0.05 {0}", minY), "0.95 0.63");

            var y = 0.96f;
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                var rowName = parent + ".FilterDropdown." + filter;
                var maxY = y;
                var minRowY = y - 0.12f;
                AddPanel(container, parent + ".FilterDropdown", rowName, string.Equals(state.Filter, filter, StringComparison.OrdinalIgnoreCase) ? GetUiSelectedColor() : GetUiSoftColor(), string.Format(CultureInfo.InvariantCulture, "0.03 {0}", minRowY), string.Format(CultureInfo.InvariantCulture, "0.97 {0}", maxY));
                AddSquarePanel(container, rowName, rowName + ".Icon", "0 0 0 0", 0.07f, 0.50f, 22f);
                AddFilterIcon(container, rowName + ".Icon", filter, string.Equals(state.Filter, filter, StringComparison.OrdinalIgnoreCase) ? GetUiGoldColor() : _config.Theme.TextMuted);
                AddLabel(container, rowName, GetFilterOptionText(player, filter), 11, string.Equals(state.Filter, filter, StringComparison.OrdinalIgnoreCase) ? GetUiGoldColor() : _config.Theme.Text, "0.14 0.14", "0.94 0.86", TextAnchor.MiddleLeft);
                AddOverlayButton(container, rowName, "UI_Handler filter " + filter);
                y -= 0.13f;
            }
        }

        private void RenderQuestList(CuiElementContainer container, BasePlayer player, string parent)
        {
            var state = GetUiState(player.userID);
            var sidebar = parent + ".Sidebar";
            AddPanel(container, parent, sidebar, GetUiSidebarColor(), "0.03 0.04", "0.33 0.92");
            AddPanel(container, sidebar, sidebar + ".List", GetUiSidebarColor(), "0.04 0.07", "0.96 0.61");
            RenderProfileBlock(container, player, sidebar);

            if (state.Tab == "Questlines")
            {
                RenderQuestlineList(container, player, sidebar + ".List");
                RenderTabs(container, player, parent);
                return;
            }

            var quests = GetFilteredQuests(player, state.Tab, state.Filter).ToList();
            AddLine(container, sidebar, GetUiGoldLineColor(), "0.04 0.615", "0.96 0.618");
            AddLabel(container, sidebar + ".List", string.Format("{0} ({1})", GetMsg("Tab." + state.Tab, player.UserIDString), quests.Count), 13, _config.Theme.Text, "0.02 0.93", "0.96 0.99", TextAnchor.MiddleLeft);

            var viewport = sidebar + ".List.Viewport";
            AddPanel(container, sidebar + ".List", viewport, "0 0 0 0", "0.02 0.03", "0.98 0.91");

            var groups = quests.GroupBy(x => x.QuestCategory).ToList();
            var contentHeight = 14f;
            foreach (var group in groups)
            {
                contentHeight += 26f;
                if (!IsQuestGroupCollapsed(state, state.Tab, group.Key))
                {
                    contentHeight += group.Count() * 60f;
                }
                contentHeight += 8f;
            }

            contentHeight = Mathf.Max(contentHeight, 360f);
            var scrollName = viewport + ".Scroll";
            AddVerticalScrollView(container, viewport, scrollName, contentHeight, 6f);

            var y = 10f;
            var groupIndex = 0;
            foreach (var group in groups)
            {
                var isCollapsed = IsQuestGroupCollapsed(state, state.Tab, group.Key);
                var groupStateKey = GetQuestGroupStateKey(state.Tab, group.Key);
                var headerName = scrollName + ".Group." + groupIndex;
                AddPanel(container, scrollName, headerName, "0 0 0 0", "0 1", "1 1", string.Format(CultureInfo.InvariantCulture, "0 {0}", -(y + 20f)), string.Format(CultureInfo.InvariantCulture, "0 {0}", -y));
                AddLabel(container, headerName, isCollapsed ? "▸" : "▾", 12, _config.Theme.TextMuted, "0.00 0.05", "0.06 0.95", TextAnchor.MiddleCenter);
                AddLabel(container, headerName, group.Key, 13, _config.Theme.TextMuted, "0.06 0.00", "0.94 1.00", TextAnchor.MiddleLeft);
                AddOverlayButton(container, headerName, "UI_Handler togglegroup " + groupStateKey);
                y += 26f;

                if (isCollapsed)
                {
                    groupIndex++;
                    continue;
                }

                foreach (var quest in group)
                {
                    var status = GetQuestStatus(player, quest);
                    var isSelected = state.SelectedQuestId == quest.QuestID;
                    var rowName = scrollName + ".Quest." + quest.QuestID;
                    AddPanel(container, scrollName, rowName, GetQuestListRowColor(status, isSelected), "0 1", "1 1", string.Format(CultureInfo.InvariantCulture, "6 {0}", -(y + 52f)), string.Format(CultureInfo.InvariantCulture, "-10 {0}", -y));
                    AddLine(container, rowName, GetUiBorderColor(), "0 0.985", "1 1");
                    AddLine(container, rowName, GetUiBorderColor(), "0 0", "1 0.015");
                    AddLine(container, rowName, GetUiBorderColor(), "0 0", "0.004 1");
                    AddLine(container, rowName, GetUiBorderColor(), "0.996 0", "1 1");
                    AddSquarePanel(container, rowName, rowName + ".Icon", "0 0 0 0", 0.09f, 0.50f, 42f);
                    AddLabel(container, rowName + ".Icon", GetQuestListGlyph(quest), 16, isSelected ? GetUiGoldColor() : _config.Theme.TextMuted, "0 0", "1 1", TextAnchor.MiddleCenter);
                    AddLabel(container, rowName, GetQuestTitle(player, quest), 13, isSelected ? _config.Theme.Text : "0.88 0.88 0.88 1", "0.19 0.42", "0.72 0.86", TextAnchor.MiddleLeft);
                    AddLabel(container, rowName, GetQuestListSubtitle(player, quest), 10, _config.Theme.TextMuted, "0.19 0.10", "0.72 0.40", TextAnchor.MiddleLeft);
                    AddLabel(container, rowName, GetMsg("Status." + status, player.UserIDString), 9, _config.Theme.TextMuted, "0.72 0.54", "0.95 0.84", TextAnchor.MiddleRight);
                    var rowMeta = GetQuestRowMeta(player, quest);
                    if (!string.IsNullOrEmpty(rowMeta))
                    {
                        AddLabel(container, rowName, rowMeta, 9, _config.Theme.TextMuted, "0.72 0.12", "0.95 0.40", TextAnchor.MiddleRight);
                    }
                    AddOverlayButton(container, rowName, "UI_Handler select " + quest.QuestID);
                    y += 60f;
                }

                groupIndex++;
            }

            RenderTabs(container, player, parent);
        }

        private void RenderQuestlineList(CuiElementContainer container, BasePlayer player, string parent)
        {
            var state = GetUiState(player.userID);
            AddLabel(container, parent, GetMsg("Tab.Questlines", player.UserIDString), 13, _config.Theme.Text, "0.04 0.92", "0.96 0.98", TextAnchor.MiddleLeft);

            if (state.SelectedQuestlineId == 0 && _questlineCatalog.Questlines.Count > 0)
            {
                state.SelectedQuestlineId = _questlineCatalog.Questlines[0].QuestlineId;
            }

            var lines = _questlineCatalog.Questlines.ToList();
            var viewport = parent + ".Viewport";
            AddPanel(container, parent, viewport, "0 0 0 0", "0.02 0.03", "0.98 0.91");
            var contentHeight = 12f;
            foreach (var line in lines)
            {
                contentHeight += 28f;
                contentHeight += Math.Max(1, line.StageQuestIds.Count) * 60f;
                contentHeight += 8f;
            }

            contentHeight = Mathf.Max(360f, contentHeight);
            var scrollName = viewport + ".Scroll";
            AddVerticalScrollView(container, viewport, scrollName, contentHeight, 6f);

            var y = 10f;
            foreach (var line in lines)
            {
                var headerName = scrollName + ".LineHeader." + line.QuestlineId;
                AddPanel(container, scrollName, headerName, "0 0 0 0", "0 1", "1 1", string.Format(CultureInfo.InvariantCulture, "0 {0}", -(y + 20f)), string.Format(CultureInfo.InvariantCulture, "0 {0}", -y));
                AddLabel(container, headerName, "▾", 12, _config.Theme.TextMuted, "0.00 0.05", "0.06 0.95", TextAnchor.MiddleCenter);
                AddLabel(container, headerName, GetQuestlineTitle(player, line), 13, _config.Theme.TextMuted, "0.06 0.00", "0.70 1.00", TextAnchor.MiddleLeft);
                AddLabel(container, headerName, GetQuestlineProgress(player, line), 11, state.SelectedQuestlineId == line.QuestlineId ? GetUiGoldColor() : _config.Theme.TextMuted, "0.72 0.00", "0.94 1.00", TextAnchor.MiddleRight);
                y += 28f;

                for (var i = 0; i < line.StageQuestIds.Count; i++)
                {
                    var quest = FindQuest(line.StageQuestIds[i]);
                    if (quest == null)
                    {
                        continue;
                    }

                    var status = GetQuestStatus(player, quest);
                    var isSelected = state.SelectedQuestId == quest.QuestID;
                    var indent = i == 0 ? 6f : 16f;
                    var rightInset = i == 0 ? -10f : -20f;
                    var rowName = scrollName + ".Line." + line.QuestlineId + "." + quest.QuestID;
                    AddPanel(container, scrollName, rowName, GetQuestListRowColor(status, isSelected), "0 1", "1 1", string.Format(CultureInfo.InvariantCulture, "{0} {1}", indent, -(y + 52f)), string.Format(CultureInfo.InvariantCulture, "{0} {1}", rightInset, -y));
                    AddLine(container, rowName, GetUiBorderColor(), "0 0.985", "1 1");
                    AddLine(container, rowName, GetUiBorderColor(), "0 0", "1 0.015");
                    AddLine(container, rowName, GetUiBorderColor(), "0 0", "0.004 1");
                    AddLine(container, rowName, GetUiBorderColor(), "0.996 0", "1 1");
                    AddSquarePanel(container, rowName, rowName + ".Icon", "0 0 0 0", 0.09f, 0.50f, 42f);
                    AddLabel(container, rowName + ".Icon", GetQuestListGlyph(quest), 16, isSelected ? GetUiGoldColor() : _config.Theme.TextMuted, "0 0", "1 1", TextAnchor.MiddleCenter);
                    AddLabel(container, rowName, GetQuestTitle(player, quest), 13, isSelected ? _config.Theme.Text : "0.88 0.88 0.88 1", "0.19 0.42", "0.72 0.86", TextAnchor.MiddleLeft);
                    AddLabel(container, rowName, GetQuestListSubtitle(player, quest), 10, _config.Theme.TextMuted, "0.19 0.10", "0.72 0.40", TextAnchor.MiddleLeft);
                    AddLabel(container, rowName, GetMsg("Status." + status, player.UserIDString), 9, _config.Theme.TextMuted, "0.72 0.54", "0.95 0.84", TextAnchor.MiddleRight);
                    var rowMeta = GetQuestRowMeta(player, quest);
                    if (!string.IsNullOrEmpty(rowMeta))
                    {
                        AddLabel(container, rowName, rowMeta, 9, _config.Theme.TextMuted, "0.72 0.12", "0.95 0.40", TextAnchor.MiddleRight);
                    }
                    AddOverlayButton(container, rowName, "UI_Handler linequest " + quest.QuestID);
                    y += 60f;
                }

                y += 8f;
            }
        }

        private void RenderQuestDetails(CuiElementContainer container, BasePlayer player, string parent)
        {
            AddPanel(container, parent, parent + ".Details", GetUiWindowColor(), "0.37 0.04", "0.97 0.92");

            var state = GetUiState(player.userID);
            if (state.Tab == "Questlines")
            {
                RenderQuestlineDetails(container, player, parent + ".Details");
                return;
            }

            QuestDefinition quest;
            if (!_allQuestsById.TryGetValue(state.SelectedQuestId, out quest))
            {
                AddLabel(container, parent + ".Details", GetMsg("Msg.UnknownQuest", player.UserIDString), 14, _config.Theme.TextMuted, "0.03 0.40", "0.97 0.60", TextAnchor.MiddleCenter);
                return;
            }

            RenderQuestCard(container, player, parent + ".Details", quest);
        }

        private void RenderQuestlineDetails(CuiElementContainer container, BasePlayer player, string parent)
        {
            var state = GetUiState(player.userID);
            QuestlineDefinition line;
            if (!_questlinesById.TryGetValue(state.SelectedQuestlineId, out line))
            {
                AddLabel(container, parent, GetMsg("Msg.UnknownQuest", player.UserIDString), 14, _config.Theme.TextMuted, "0.03 0.40", "0.97 0.60", TextAnchor.MiddleCenter);
                return;
            }

            QuestDefinition selectedQuest;
            if (!_allQuestsById.TryGetValue(state.SelectedQuestId, out selectedQuest) || selectedQuest.QuestlineId != line.QuestlineId)
            {
                selectedQuest = line.StageQuestIds.Select(FindQuest).FirstOrDefault(x => x != null);
                if (selectedQuest != null)
                {
                    state.SelectedQuestId = selectedQuest.QuestID;
                }
            }

            if (selectedQuest == null)
            {
                AddLabel(container, parent, GetMsg("Msg.UnknownQuest", player.UserIDString), 14, _config.Theme.TextMuted, "0.03 0.40", "0.97 0.60", TextAnchor.MiddleCenter);
                return;
            }

            RenderQuestCard(container, player, parent, selectedQuest, line);
        }

        private void RenderQuestCard(CuiElementContainer container, BasePlayer player, string parent, QuestDefinition quest, QuestlineDefinition line = null)
        {
            var status = GetQuestStatus(player, quest);
            var headerName = parent + ".Header";
            AddPanel(container, parent, headerName, GetUiWindowColor(), "0.03 0.72", "0.97 0.95");
            AddLine(container, headerName, GetUiGoldLineColor(), "0.00 0.00", "1.00 0.003");
            AddLabel(container, headerName, "< " + GetMsg("Label.Overview", player.UserIDString), 10, _config.Theme.TextMuted, "0.01 0.86", "0.30 0.98", TextAnchor.MiddleLeft);
            AddOverlayButton(container, headerName, "UI_Handler overview", "0.01 0.84", "0.30 0.98");
            AddSquareThinPanel(container, headerName, headerName + ".Icon", GetUiSoftColor(), GetUiBorderColor(), 0.06f, 0.65f, 48f);
            AddLabel(container, headerName + ".Icon", GetQuestListGlyph(quest), 20, GetUiGoldColor(), "0 0", "1 1", TextAnchor.MiddleCenter);
            AddLabel(container, headerName, GetQuestTitle(player, quest), 22, _config.Theme.Text, "0.12 0.56", "0.70 0.84", TextAnchor.MiddleLeft);
            var subtitle = GetQuestHeaderSubtitle(player, quest, line);
            if (!string.IsNullOrEmpty(subtitle))
            {
                AddLabel(container, headerName, subtitle, 11, _config.Theme.TextMuted, "0.12 0.42", "0.70 0.58", TextAnchor.MiddleLeft);
            }
            AddThinPanel(container, headerName, headerName + ".Badge", GetUiSoftColor(), GetUiBorderColor(), "0.84 0.72", "0.97 0.86");
            AddLabel(container, headerName + ".Badge", GetQuestBadgeText(player, quest, line), 10, _config.Theme.Text, "0.05 0.10", "0.95 0.90", TextAnchor.MiddleCenter);
            AddLine(container, headerName, GetUiGoldLineColor(), "0.02 0.38", "0.98 0.383");
            AddLabel(container, headerName, GetQuestDescription(player, quest), 12, _config.Theme.Text, "0.03 0.08", "0.97 0.34", TextAnchor.UpperLeft);

            RenderQuestObjectivesBlock(container, player, parent, quest);
            RenderQuestRewardsBlock(container, player, parent, quest);
            RenderFooterStrip(container, player, parent, quest, status, line);
        }

        private void RenderQuestObjectivesBlock(CuiElementContainer container, BasePlayer player, string parent, QuestDefinition quest)
        {
            AddLabel(container, parent, GetMsg("Label.Objectives", player.UserIDString), 18, _config.Theme.Text, "0.04 0.60", "0.34 0.66", TextAnchor.MiddleLeft);

            var objective = quest.Objectives.Where(x => !x.Hidden).OrderBy(x => x.Order).FirstOrDefault();
            if (objective == null)
            {
                AddLabel(container, parent, GetMsg("Label.NoObjectives", player.UserIDString), 11, _config.Theme.TextMuted, "0.04 0.46", "0.40 0.54", TextAnchor.MiddleLeft);
                return;
            }

            AddThinPanel(container, parent, parent + ".ObjectiveRow", GetUiSoftColor(), GetUiBorderColor(), "0.04 0.48", "0.40 0.56");
            AddSquarePanel(container, parent + ".ObjectiveRow", parent + ".ObjectiveRow.Icon", "0 0 0 0", 0.103f, 0.50f, 38f);
            AddObjectiveIcon(container, parent + ".ObjectiveRow.Icon", objective);
            AddLabel(container, parent + ".ObjectiveRow", GetObjectiveTitle(player, objective), 11, _config.Theme.Text, "0.235 0.18", "0.72 0.82", TextAnchor.MiddleLeft);
            AddLabel(container, parent + ".ObjectiveRow", string.Format("{0} / {1}", GetObjectiveDisplayProgress(player.userID, quest.QuestID, objective), objective.TargetCount), 11, GetUiGoldColor(), "0.74 0.18", "0.96 0.82", TextAnchor.MiddleRight);

            if (quest.ResetProgressOnWipe)
            {
                AddLabel(container, parent, "Warning!", 11, GetUiGoldColor(), "0.04 0.42", "0.11 0.47", TextAnchor.MiddleLeft);
                AddLabel(container, parent, "This Quest's Progress resets on Wipe", 11, _config.Theme.Text, "0.11 0.42", "0.42 0.47", TextAnchor.MiddleLeft);
            }
        }

        private void RenderQuestRewardsBlock(CuiElementContainer container, BasePlayer player, string parent, QuestDefinition quest)
        {
            var allPrizes = quest.PrizeList.Where(x => !x.IsHidden).ToList();
            var prizes = allPrizes
                .OrderByDescending(IsPinnedRewardPrize)
                .ThenBy(x => x.PrizeType ?? string.Empty)
                .Take(6)
                .ToList();
            AddPanel(container, parent, parent + ".RewardsArea", "0 0 0 0", "0.58 0.14", "0.92 0.62");
            AddCornerFrame(container, parent + ".RewardsArea", GetUiGoldLineColor());

            if (prizes.Count == 0)
            {
                AddLabel(container, parent + ".RewardsArea", GetMsg("Label.NoRewards", player.UserIDString), 11, _config.Theme.TextMuted, "0.08 0.48", "0.92 0.58", TextAnchor.MiddleCenter);
            }
            else
            {
                var slotSize = prizes.Count > 3 ? 56f : 62f;
                for (var i = 0; i < prizes.Count; i++)
                {
                    var prize = prizes[i];
                    var col = i % 3;
                    var row = i / 3;
                    var slotName = parent + ".RewardsArea.Slot." + i;
                    var centerX = 0.18f + (col * 0.32f);
                    var centerY = row == 0 ? 0.82f : 0.52f;
                    AddSquarePanel(container, parent + ".RewardsArea", slotName, "0 0 0 0", centerX, centerY, slotSize);
                    AddCornerFrame(container, slotName, GetUiGoldLineColor());
                    AddPrizeIcon(container, slotName, prize);
                    AddPanel(container, slotName, slotName + ".AmountBg", "0 0 0 0.45", "0.06 0.02", "0.94 0.24");
                    AddLabel(container, slotName, GetPrizeAmountText(prize), 11, GetUiGoldColor(), "0.08 0.02", "0.92 0.24", TextAnchor.MiddleCenter);
                }
            }

            AddPanel(container, parent, parent + ".RewardsFooter", GetUiFooterPanelColor(), "0.58 0.14", "0.92 0.28");
            AddLabel(container, parent + ".RewardsFooter", GetMsg("Label.QuestRewards", player.UserIDString), 22, GetUiGoldColor(), "0.06 0.42", "0.94 0.86", TextAnchor.MiddleCenter);
            AddLabel(container, parent + ".RewardsFooter", string.Format(GetMsg("Label.RewardCount", player.UserIDString), allPrizes.Count), 10, _config.Theme.Text, "0.06 0.08", "0.94 0.42", TextAnchor.MiddleCenter);
        }

        private void RenderQuestlineStageRail(CuiElementContainer container, BasePlayer player, string parent, QuestlineDefinition line, long selectedQuestId)
        {
            AddLine(container, parent, GetUiGoldLineColor(), "0.04 0.67", "0.58 0.672");
            AddLabel(container, parent, GetMsg("Label.Stages", player.UserIDString), 10, _config.Theme.TextMuted, "0.04 0.635", "0.20 0.67", TextAnchor.MiddleLeft);
            AddLabel(container, parent, GetQuestlineProgress(player, line), 10, GetUiGoldColor(), "0.50 0.635", "0.58 0.67", TextAnchor.MiddleRight);

            var count = Math.Max(1, line.StageQuestIds.Count);
            var width = Math.Min(0.09f, 0.46f / count);
            var gap = 0.015f;
            var startX = 0.07f;
            for (var i = 0; i < line.StageQuestIds.Count; i++)
            {
                var quest = FindQuest(line.StageQuestIds[i]);
                if (quest == null)
                {
                    continue;
                }

                var minX = startX + (i * (width + gap));
                var maxX = minX + width;
                var stageName = parent + ".Stages." + quest.QuestID;
                AddThinPanel(container, parent, stageName, quest.QuestID == selectedQuestId ? GetUiSelectedColor() : GetUiSoftColor(), GetUiBorderColor(), string.Format(CultureInfo.InvariantCulture, "{0} 0.58", minX), string.Format(CultureInfo.InvariantCulture, "{0} 0.64", maxX));
                AddLabel(container, stageName, string.Format("{0}", i + 1), 10, quest.QuestID == selectedQuestId ? GetUiGoldColor() : _config.Theme.TextMuted, "0 0", "1 1", TextAnchor.MiddleCenter);
                AddOverlayButton(container, stageName, "UI_Handler linequest " + quest.QuestID);
            }
        }

        private void RenderFooterStrip(CuiElementContainer container, BasePlayer player, string parent, QuestDefinition quest, QuestStatus status, QuestlineDefinition line)
        {
            AddLine(container, parent, GetUiGoldLineColor(), "0.03 0.125", "0.97 0.128");
            AddPanel(container, parent, parent + ".Footer", GetUiFooterPanelColor(), "0.03 0.03", "0.97 0.12");
            RenderFooterButtons(container, player, parent + ".Footer", quest, status);
        }

        private void RenderFooterButtons(CuiElementContainer container, BasePlayer player, string parent, QuestDefinition quest, QuestStatus status)
        {
            var tracked = GetPlayerProgress(player.userID).TrackedQuestId == quest.QuestID;
            var right = 0.98f;

            if (status == QuestStatus.OnCooldown)
            {
                AddThinPanel(container, parent, parent + ".Cooldown", GetUiSoftColor(), GetUiBorderColor(), "0.80 0.18", "0.98 0.82");
                AddLabel(container, parent + ".Cooldown", GetFooterTimerText(player, quest, status), 12, GetUiGoldColor(), "0.06 0.10", "0.94 0.90", TextAnchor.MiddleCenter);
                return;
            }

            if (status == QuestStatus.Available && quest.AllowManualStart)
            {
                AddStyledButton(container, parent, GetUiSoftColor(), GetMsg("Action.Start", player.UserIDString), 12, GetUiGoldColor(), "0.80 0.18", "0.98 0.82", "UI_Handler start " + quest.QuestID);
                return;
            }

            if (status == QuestStatus.ReadyToClaim && quest.AllowClaim)
            {
                AddStyledButton(container, parent, GetUiSoftColor(), GetMsg("Action.Claim", player.UserIDString), 12, GetUiGoldColor(), "0.78 0.18", "0.98 0.82", "UI_Handler claim " + quest.QuestID);
                right = 0.76f;
            }
            else if (status == QuestStatus.Started)
            {
                var hasSubmission = HasSubmissionObjective(quest);

                if (quest.AllowCancel)
                {
                    AddStyledButton(container, parent, GetUiSoftColor(), GetMsg("Action.Cancel", player.UserIDString), 11, _config.Theme.Text, hasSubmission ? "0.42 0.18" : "0.60 0.18", hasSubmission ? "0.60 0.82" : "0.78 0.82", "UI_Handler cancel " + quest.QuestID);
                }

                if (quest.AllowTrack)
                {
                    AddStyledButton(container, parent, GetUiSoftColor(), tracked ? GetMsg("Action.Untrack", player.UserIDString) : GetMsg("Action.Track", player.UserIDString), 11, hasSubmission ? _config.Theme.Text : GetUiGoldColor(), hasSubmission ? "0.61 0.18" : "0.80 0.18", hasSubmission ? "0.79 0.82" : "0.98 0.82", tracked ? "UI_Handler untrack 0" : "UI_Handler track " + quest.QuestID);
                }

                if (hasSubmission)
                {
                    AddStyledButton(container, parent, GetUiSoftColor(), GetMsg("Action.Submit", player.UserIDString), 12, GetUiGoldColor(), "0.80 0.18", "0.98 0.82", "UI_Handler submit " + quest.QuestID);
                }
                return;
            }

            if (quest.AllowTrack && status != QuestStatus.Locked && status != QuestStatus.OnCooldown)
            {
                AddStyledButton(container, parent, GetUiSoftColor(), tracked ? GetMsg("Action.Untrack", player.UserIDString) : GetMsg("Action.Track", player.UserIDString), 11, _config.Theme.Text, string.Format(CultureInfo.InvariantCulture, "{0} 0.18", right - 0.18f), string.Format(CultureInfo.InvariantCulture, "{0} 0.82", right), tracked ? "UI_Handler untrack 0" : "UI_Handler track " + quest.QuestID);
            }
        }

        private void RenderProfileBlock(CuiElementContainer container, BasePlayer player, string parent)
        {
            AddPanel(container, parent, parent + ".Profile", GetUiProfileColor(), "0.05 0.80", "0.95 0.95");
            AddSquareThinPanel(container, parent + ".Profile", parent + ".Profile.Avatar", GetUiSoftColor(), GetUiBorderColor(), 0.115f, 0.50f, 52f);
            AddSteamAvatar(container, parent + ".Profile.Avatar", player.userID);
            var initial = string.IsNullOrEmpty(player.displayName) ? "P" : player.displayName.Substring(0, 1).ToUpperInvariant();
            AddLabel(container, parent + ".Profile.Avatar", initial, 18, "1 1 1 0.08", "0 0", "1 1", TextAnchor.MiddleCenter);
            AddStyledButton(container, parent + ".Profile", GetUiSoftColor(), "≡", 12, GetUiGoldColor(), "0.82 0.66", "0.89 0.86", "UI_Handler toggle_minilist");
            AddStyledButton(container, parent + ".Profile", GetUiSoftColor(), "*", 13, GetUiGoldColor(), "0.90 0.66", "0.97 0.86", "UI_Handler settings");
            AddLabel(container, parent + ".Profile", player.displayName, 18, _config.Theme.Text, "0.24 0.54", "0.76 0.80", TextAnchor.MiddleLeft);

            var progress = GetPlayerProgress(player.userID);
            AddLabel(container, parent + ".Profile", string.Format("{0} {1}", GetMsg("Label.ActiveSummary", player.UserIDString), progress.ActiveQuests.Count), 10, _config.Theme.TextMuted, "0.24 0.28", "0.48 0.44", TextAnchor.MiddleLeft);
            AddLabel(container, parent + ".Profile", string.Format("{0} {1}", GetMsg("Label.CompletedSummary", player.UserIDString), progress.CompletedQuests.Count), 10, _config.Theme.TextMuted, "0.52 0.28", "0.80 0.44", TextAnchor.MiddleLeft);
            AddLabel(container, parent + ".Profile", string.Format("{0}: {1}", GetMsg("Label.TrackedSummary", player.UserIDString), GetProfileTrackedQuestText(player)), 10, GetUiGoldColor(), "0.24 0.12", "0.84 0.26", TextAnchor.MiddleLeft);
        }

        private void RenderMiniUi(BasePlayer player)
        {
            DestroyMiniUi(player);
            EnsurePlayerData(player.userID);

            var progress = GetPlayerProgress(player.userID);
            var activeQuests = progress.ActiveQuests.Keys
                .Select(FindQuest)
                .Where(x => x != null)
                .OrderByDescending(x => IsQuestReadyToClaim(player, x))
                .ThenBy(x => x.QuestID)
                .ToList();

            if (activeQuests.Count == 0)
            {
                DestroyMiniUi(player);
                GetUiState(player.userID).MiniOpen = false;
                return;
            }

            var state = GetUiState(player.userID);
            state.MiniOpen = true;

            var pageSize = Math.Max(1, _config.Ui.MiniListPerPage);
            var pageCount = Math.Max(1, Mathf.CeilToInt(activeQuests.Count / (float)pageSize));
            state.MiniPage = Mathf.Clamp(state.MiniPage, 0, pageCount - 1);
            var paged = activeQuests.Skip(state.MiniPage * pageSize).Take(pageSize).ToList();

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = _config.Theme.Panel },
                RectTransform = { AnchorMin = "0.74 0.22", AnchorMax = "0.97 0.55" },
                CursorEnabled = false
            }, "Hud", MiniUiName);

            AddLabel(container, MiniUiName, GetMsg("Title", player.UserIDString), 13, _config.Theme.Text, "0.05 0.90", "0.85 0.98", TextAnchor.MiddleLeft);
            AddButton(container, MiniUiName, _config.Theme.Danger, "X", 11, "0.87 0.90", "0.96 0.98", "CloseMiniQuestList");

            var y = 0.82f;
            foreach (var quest in paged)
            {
                var status = IsQuestReadyToClaim(player, quest) ? GetMsg("Status.ReadyToClaim", player.UserIDString) : GetQuestCompletionText(player, quest);
                AddLabel(container, MiniUiName, GetQuestTitle(player, quest), 11, _config.Theme.Accent, string.Format(CultureInfo.InvariantCulture, "0.05 {0}", y - 0.08f), string.Format(CultureInfo.InvariantCulture, "0.95 {0}", y), TextAnchor.MiddleLeft);
                AddLabel(container, MiniUiName, status, 10, _config.Theme.TextMuted, string.Format(CultureInfo.InvariantCulture, "0.05 {0}", y - 0.16f), string.Format(CultureInfo.InvariantCulture, "0.95 {0}", y - 0.08f), TextAnchor.MiddleLeft);
                y -= 0.18f;
            }

            AddButton(container, MiniUiName, _config.Theme.PanelAlt, "<", 11, "0.05 0.04", "0.16 0.12", "UI_Handler minipage prev");
            AddLabel(container, MiniUiName, string.Format("{0}/{1}", state.MiniPage + 1, pageCount), 11, _config.Theme.Text, "0.18 0.04", "0.36 0.12", TextAnchor.MiddleCenter);
            AddButton(container, MiniUiName, _config.Theme.PanelAlt, ">", 11, "0.38 0.04", "0.49 0.12", "UI_Handler minipage next");

            CuiHelper.AddUi(player, container);
        }

        private void RenderSettingsUi(BasePlayer player)
        {
            DestroySettingsUi(player);

            var progress = GetPlayerProgress(player.userID);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = _config.Theme.Panel },
                RectTransform = { AnchorMin = "0.68 0.55", AnchorMax = "0.92 0.83" }
            }, MainUiName + ".Root", SettingsUiName);

            AddLabel(container, SettingsUiName, GetMsg("Label.Settings", player.UserIDString), 13, _config.Theme.Text, "0.05 0.84", "0.95 0.96", TextAnchor.MiddleLeft);
            AddButton(container, SettingsUiName, progress.NotificationSettings.ProgressToasts ? _config.Theme.Success : _config.Theme.Locked, GetMsg("Label.ProgressToasts", player.UserIDString), 11, "0.05 0.56", "0.95 0.76", "UI_Handler toggle_toast 1");
            AddButton(container, SettingsUiName, progress.NotificationSettings.ChatMessages ? _config.Theme.Success : _config.Theme.Locked, GetMsg("Label.ChatMessages", player.UserIDString), 11, "0.05 0.30", "0.95 0.50", "UI_Handler toggle_chat 1");
            AddButton(container, SettingsUiName, progress.NotificationSettings.MiniListAutoShow ? _config.Theme.Success : _config.Theme.Locked, GetMsg("Label.MiniAuto", player.UserIDString), 11, "0.05 0.04", "0.95 0.24", "UI_Handler toggle_miniauto 1");
            CuiHelper.AddUi(player, container);
        }

        private void DestroyMainUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MainUiName);
        }

        private void DestroyMiniUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MiniUiName);
        }

        private void DestroyToast(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, ToastUiName);

            Timer toastTimer;
            if (_toastTimers.TryGetValue(player.userID, out toastTimer))
            {
                toastTimer.Destroy();
                _toastTimers.Remove(player.userID);
            }
        }

        private void DestroySettingsUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, SettingsUiName);
        }

        private void ShowToast(BasePlayer player, string message)
        {
            var settings = GetPlayerProgress(player.userID).NotificationSettings;
            if (!settings.ProgressToasts)
            {
                return;
            }

            DestroyToast(player);

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = _config.Theme.PanelAlt },
                RectTransform = { AnchorMin = "0.73 0.84", AnchorMax = "0.97 0.92" }
            }, "Hud", ToastUiName);
            AddLabel(container, ToastUiName, message, 12, _config.Theme.Text, "0.04 0.12", "0.96 0.88", TextAnchor.MiddleCenter);
            CuiHelper.AddUi(player, container);
            _toastTimers[player.userID] = timer.Once(3.5f, delegate
            {
                CuiHelper.DestroyUi(player, ToastUiName);
                _toastTimers.Remove(player.userID);
            });
        }

        private void StartQuest(BasePlayer player, long questId)
        {
            QuestDefinition quest = FindQuest(questId);
            if (quest == null)
            {
                SendLocalizedMessage(player, "Msg.UnknownQuest");
                return;
            }

            var progress = GetPlayerProgress(player.userID);
            if (progress.ActiveQuests.ContainsKey(questId))
            {
                SendLocalizedMessage(player, "Msg.AlreadyActive");
                return;
            }

            if (progress.ActiveQuests.Count >= _config.Settings.questCount)
            {
                SendLocalizedMessage(player, "Msg.MaxActive");
                return;
            }

            if (!CanStartQuest(player, quest, true))
            {
                if (!HasQuestPermission(player, quest))
                {
                    SendLocalizedMessage(player, "Msg.NoPermission");
                }
                SendLocalizedMessage(player, "Msg.CannotStart");
                return;
            }

            var record = new ActiveQuestRecord
            {
                QuestID = questId,
                StartedAt = CurrentTime()
            };
            foreach (var objective in quest.Objectives)
            {
                record.ObjectiveProgress[objective.ObjectiveId] = 0;
            }

            progress.ActiveQuests[questId] = record;
            progress.ClaimedRewards[questId] = false;
            _statistics.TakenTasks++;
            SaveAllData();

            if (progress.NotificationSettings.MiniListAutoShow)
            {
                GetUiState(player.userID).MiniOpen = true;
                RenderMiniUi(player);
            }

            SendLocalizedMessage(player, "Msg.QuestStarted", GetQuestTitle(player, quest));
            OpenMainUi(player);
        }

        private void CancelQuest(BasePlayer player, long questId)
        {
            var progress = GetPlayerProgress(player.userID);
            ActiveQuestRecord record;
            if (!progress.ActiveQuests.TryGetValue(questId, out record))
            {
                return;
            }

            progress.ActiveQuests.Remove(questId);
            if (progress.TrackedQuestId == questId)
            {
                progress.TrackedQuestId = 0;
            }

            _statistics.DeclinedTasks++;
            SaveAllData();

            var quest = FindQuest(questId);
            if (quest != null)
            {
                SendLocalizedMessage(player, "Msg.QuestCancelled", GetQuestTitle(player, quest));
            }

            OpenMainUi(player);
            RenderMiniUiIfNeeded(player);
        }

        private void ClaimQuest(BasePlayer player, long questId)
        {
            var quest = FindQuest(questId);
            if (quest == null)
            {
                SendLocalizedMessage(player, "Msg.UnknownQuest");
                return;
            }

            if (!IsQuestReadyToClaim(player, quest))
            {
                SendLocalizedMessage(player, "Msg.NotReady");
                return;
            }

            if (!GiveRewards(player, quest))
            {
                return;
            }

            var progress = GetPlayerProgress(player.userID);
            progress.ActiveQuests.Remove(questId);
            progress.CompletedQuests.Add(questId);
            progress.ClaimedRewards[questId] = true;

            if (quest.IsRepeatable)
            {
                progress.Cooldowns[questId] = GetQuestCooldownExpiry(quest);
            }

            if (progress.TrackedQuestId == questId)
            {
                progress.TrackedQuestId = 0;
            }

            _statistics.CompletedTasks++;
            IncrementQuestExecutionCount(questId);
            SaveAllData();

            Interface.CallHook("OnQuestCompleted", player, GetQuestTitle(player, quest));
            SendLocalizedMessage(player, "Msg.QuestClaimed", GetQuestTitle(player, quest));

            OpenMainUi(player);
            RenderMiniUiIfNeeded(player);
        }

        private void SetTrackedQuest(BasePlayer player, long questId)
        {
            var progress = GetPlayerProgress(player.userID);
            if (questId != 0 && !progress.ActiveQuests.ContainsKey(questId))
            {
                return;
            }

            progress.TrackedQuestId = questId;
            SavePlayerData();

            var quest = FindQuest(questId);
            SendLocalizedMessage(player, "Msg.QuestTracked", quest == null ? "-" : GetQuestTitle(player, quest));
            OpenMainUi(player);
            RenderMiniUiIfNeeded(player);
        }

        private void SubmitQuestItems(BasePlayer player, long questId)
        {
            var quest = FindQuest(questId);
            if (quest == null)
            {
                return;
            }

            var progress = GetPlayerProgress(player.userID);
            ActiveQuestRecord record;
            if (!progress.ActiveQuests.TryGetValue(questId, out record))
            {
                return;
            }

            var changed = false;
            foreach (var objective in quest.Objectives.Where(x => x.SubmissionRequired))
            {
                var current = GetObjectiveProgress(player.userID, questId, objective.ObjectiveId);
                var missing = objective.TargetCount - current;
                if (missing <= 0)
                {
                    continue;
                }

                var submitted = TakeItemsForObjective(player, objective, missing);
                if (submitted <= 0)
                {
                    continue;
                }

                changed = true;
                ApplyObjectiveProgress(player, quest, objective, submitted, true);
            }

            if (!changed)
            {
                SendLocalizedMessage(player, "Msg.NothingToSubmit");
                return;
            }

            if (IsQuestReadyToClaim(player, quest))
            {
                ClaimQuest(player, questId);
                return;
            }

            SendLocalizedMessage(player, "Msg.Submitted", GetQuestTitle(player, quest));
            OpenMainUi(player);
            RenderMiniUiIfNeeded(player);
        }

        private int TakeItemsForObjective(BasePlayer player, ObjectiveDefinition objective, int maxTake)
        {
            var taken = 0;
            var containers = new[]
            {
                player.inventory.containerMain,
                player.inventory.containerBelt,
                player.inventory.containerWear
            };

            foreach (var container in containers)
            {
                if (container == null || taken >= maxTake)
                {
                    continue;
                }

                var items = container.itemList.ToList();
                foreach (var item in items)
                {
                    if (taken >= maxTake)
                    {
                        break;
                    }

                    if (!IsObjectiveMatch(objective, item.info.shortname, item.skin))
                    {
                        continue;
                    }

                    var remove = Math.Min(item.amount, maxTake - taken);
                    item.UseItem(remove);
                    taken += remove;
                }
            }

            return taken;
        }

        private bool GiveRewards(BasePlayer player, QuestDefinition quest)
        {
            foreach (var prize in quest.PrizeList)
            {
                switch ((prize.PrizeType ?? string.Empty).ToLowerInvariant())
                {
                    case "item":
                        GiveItemReward(player, prize);
                        break;
                    case "blueprint":
                    case "blueprintitem":
                    case "blueprintreward":
                        GiveBlueprintReward(player, prize);
                        break;
                    case "customitem":
                        GiveCustomItemReward(player, prize);
                        break;
                    case "command":
                        RunPrizeCommand(player, prize);
                        break;
                    case "rp":
                        GiveServerRewardPoints(player, prize.ItemAmount);
                        break;
                    case "xp":
                        GiveExperience(player, prize.ItemAmount);
                        break;
                    case "blueprintfragments":
                    case "fragments":
                        GiveBlueprintFragments(player, prize.ItemAmount);
                        break;
                    default:
                        PrintWarning(string.Format("Unknown prize type '{0}' on quest {1}", prize.PrizeType, quest.QuestID));
                        break;
                }
            }

            return true;
        }

        private void GiveItemReward(BasePlayer player, PrizeDefinition prize)
        {
            var item = ItemManager.CreateByName(prize.ItemShortName, Math.Max(1, prize.ItemAmount), prize.ItemSkinID);
            if (item == null)
            {
                PrintWarning(string.Format("Unable to create item reward '{0}'", prize.ItemShortName));
                return;
            }

            player.GiveItem(item);
        }

        private void GiveBlueprintReward(BasePlayer player, PrizeDefinition prize)
        {
            var target = ItemManager.FindItemDefinition(prize.ItemShortName);
            if (target == null)
            {
                PrintWarning(string.Format("Unknown blueprint item '{0}'", prize.ItemShortName));
                return;
            }

            var item = ItemManager.CreateByName("blueprintbase", Math.Max(1, prize.ItemAmount));
            if (item == null)
            {
                return;
            }

            item.blueprintTarget = target.itemid;
            player.GiveItem(item);
        }

        private void GiveCustomItemReward(BasePlayer player, PrizeDefinition prize)
        {
            var item = ItemManager.CreateByName(prize.ItemShortName, Math.Max(1, prize.ItemAmount), prize.ItemSkinID);
            if (item == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(prize.CustomItemName))
            {
                item.name = prize.CustomItemName;
            }

            player.GiveItem(item);
        }

        private void RunPrizeCommand(BasePlayer player, PrizeDefinition prize)
        {
            if (string.IsNullOrEmpty(prize.PrizeCommand))
            {
                return;
            }

            var command = prize.PrizeCommand.Replace("%STEAMID%", player.UserIDString);
            Server.Command(command);
        }

        private void GiveServerRewardPoints(BasePlayer player, int amount)
        {
            var rewarded = false;

            if (Economics != null && Economics.IsLoaded)
            {
                Economics.Call("Deposit", player.userID, Convert.ToDouble(Math.Max(1, amount)));
                rewarded = true;
            }

            if (!rewarded && ServerRewards != null && ServerRewards.IsLoaded)
            {
                ServerRewards.Call("AddPoints", player.userID, amount);
                rewarded = true;
            }

            if (!rewarded)
            {
                PrintWarning("RP reward requested but neither Economics nor ServerRewards plugin is available.");
            }
        }

        private void GiveExperience(BasePlayer player, int amount)
        {
            var rewarded = false;

            if (SkillTree != null && SkillTree.IsLoaded)
            {
                SkillTree.Call("AwardXP", player, Convert.ToDouble(Math.Max(1, amount)), Name, _config.Settings.UseSkillTreeIgnoreHooks);
                rewarded = true;
            }

            if (Economics != null && Economics.IsLoaded)
            {
                Economics.Call("Deposit", player.userID, Convert.ToDouble(Math.Max(1, amount)));
                rewarded = true;
            }

            if (!rewarded)
            {
                PrintWarning("XP reward requested but neither SkillTree nor Economics plugin is available.");
            }
        }

        private void GiveBlueprintFragments(BasePlayer player, int amount)
        {
            var item = ItemManager.CreateByName("blueprintfragment", Math.Max(1, amount));
            if (item != null)
            {
                player.GiveItem(item);
            }
        }

        private void TickCooldowns()
        {
            var now = CurrentTime();
            var expiredByPlayer = new Dictionary<ulong, List<long>>();

            foreach (var entry in _storedData.Players)
            {
                ulong userId;
                if (!ulong.TryParse(entry.Key, out userId))
                {
                    continue;
                }

                var removed = entry.Value.Cooldowns
                    .Where(x => x.Value <= now + 30d)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var questId in removed)
                {
                    entry.Value.Cooldowns.Remove(questId);
                }

                if (removed.Count > 0)
                {
                    expiredByPlayer[userId] = removed;
                }
            }

            foreach (var kvp in expiredByPlayer)
            {
                var player = BasePlayer.FindByID(kvp.Key);
                if (player == null)
                {
                    continue;
                }

                foreach (var questId in kvp.Value)
                {
                    var quest = FindQuest(questId);
                    if (quest != null)
                    {
                        SendLocalizedMessage(player, "Msg.CooldownFinished", GetQuestTitle(player, quest));
                    }
                }

                if (GetUiState(player.userID).MainOpen)
                {
                    OpenMainUi(player);
                }
                RenderMiniUiIfNeeded(player);
            }

            SavePlayerData();
        }

        private void TickRealtimeUi()
        {
            var now = CurrentTime();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected)
                {
                    continue;
                }

                var state = GetUiState(player.userID);
                if (!state.MainOpen)
                {
                    continue;
                }

                QuestDefinition quest;
                if (!_allQuestsById.TryGetValue(state.SelectedQuestId, out quest))
                {
                    continue;
                }

                var progress = GetPlayerProgress(player.userID);
                double cooldownExpiry;
                if (!progress.Cooldowns.TryGetValue(quest.QuestID, out cooldownExpiry))
                {
                    continue;
                }

                if (cooldownExpiry > now)
                {
                    RefreshFooterUi(player);
                    continue;
                }

                progress.Cooldowns.Remove(quest.QuestID);
                SendLocalizedMessage(player, "Msg.CooldownFinished", GetQuestTitle(player, quest));
                RefreshMainUi(player, true, true);
                SavePlayerData();
            }
        }

        private bool CanStartQuest(BasePlayer player, QuestDefinition quest, bool checkLimit)
        {
            var progress = GetPlayerProgress(player.userID);

            if (checkLimit && progress.ActiveQuests.Count >= _config.Settings.questCount)
            {
                return false;
            }

            if (progress.ActiveQuests.ContainsKey(quest.QuestID))
            {
                return false;
            }

            if (!HasQuestPermission(player, quest))
            {
                return false;
            }

            if (IsQuestBlockedByConditions(player, quest))
            {
                return false;
            }

            if (!AreQuestRequirementsMet(player.userID, quest))
            {
                return false;
            }

            if (!quest.IsRepeatable && progress.CompletedQuests.Contains(quest.QuestID))
            {
                return false;
            }

            double cooldownExpiry;
            if (progress.Cooldowns.TryGetValue(quest.QuestID, out cooldownExpiry) && cooldownExpiry > CurrentTime())
            {
                return false;
            }

            return true;
        }

        private bool HasQuestPermission(BasePlayer player, QuestDefinition quest)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionDefault))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(quest.QuestPermission) && !permission.UserHasPermission(player.UserIDString, GetQuestPermission(quest.QuestPermission)))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(quest.RequiredPermission) && !permission.UserHasPermission(player.UserIDString, quest.RequiredPermission))
            {
                return false;
            }

            return true;
        }

        private bool AreQuestRequirementsMet(ulong userId, QuestDefinition quest)
        {
            var progress = GetPlayerProgress(userId);
            foreach (var requiredId in quest.RequiredQuestIds)
            {
                if (!progress.CompletedQuests.Contains(requiredId))
                {
                    return false;
                }
            }

            if (quest.QuestlineId > 0 && quest.QuestlineOrder > 1)
            {
                var previous = _allQuestsById.Values
                    .Where(x => x.QuestlineId == quest.QuestlineId && x.QuestlineOrder == quest.QuestlineOrder - 1)
                    .Select(x => x.QuestID)
                    .FirstOrDefault();

                if (previous > 0 && !progress.CompletedQuests.Contains(previous))
                {
                    return false;
                }
            }

            foreach (var condition in quest.AvailabilityConditions)
            {
                if (!EvaluateCondition(progress, condition))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsQuestBlockedByConditions(BasePlayer player, QuestDefinition quest)
        {
            var progress = GetPlayerProgress(player.userID);
            foreach (var condition in quest.BlockConditions)
            {
                if (EvaluateCondition(progress, condition))
                {
                    return true;
                }
            }

            return false;
        }

        private bool EvaluateCondition(PlayerProgress progress, ConditionDefinition condition)
        {
            if (condition == null || string.IsNullOrEmpty(condition.Type))
            {
                return false;
            }

            switch (condition.Type.ToLowerInvariant())
            {
                case "questcompleted":
                    long completedId;
                    if (long.TryParse(condition.Value, out completedId))
                    {
                        return progress.CompletedQuests.Contains(completedId);
                    }
                    break;
                case "questactive":
                    long activeId;
                    if (long.TryParse(condition.Value, out activeId))
                    {
                        return progress.ActiveQuests.ContainsKey(activeId);
                    }
                    break;
                case "trackedquest":
                    long trackedId;
                    if (long.TryParse(condition.Value, out trackedId))
                    {
                        return progress.TrackedQuestId == trackedId;
                    }
                    break;
            }

            return false;
        }

        private QuestStatus GetQuestStatus(BasePlayer player, QuestDefinition quest)
        {
            var progress = GetPlayerProgress(player.userID);
            if (progress.ActiveQuests.ContainsKey(quest.QuestID))
            {
                return IsQuestReadyToClaim(player, quest) ? QuestStatus.ReadyToClaim : QuestStatus.Started;
            }

            double cooldown;
            if (progress.Cooldowns.TryGetValue(quest.QuestID, out cooldown) && cooldown > CurrentTime())
            {
                return QuestStatus.OnCooldown;
            }

            if (!CanStartQuest(player, quest, false))
            {
                if (!quest.IsRepeatable && progress.CompletedQuests.Contains(quest.QuestID))
                {
                    return QuestStatus.Completed;
                }

                return QuestStatus.Locked;
            }

            if (progress.CompletedQuests.Contains(quest.QuestID) && !quest.IsRepeatable)
            {
                return QuestStatus.Completed;
            }

            return QuestStatus.Available;
        }

        private bool IsQuestReadyToClaim(BasePlayer player, QuestDefinition quest)
        {
            var progress = GetPlayerProgress(player.userID);
            ActiveQuestRecord record;
            if (!progress.ActiveQuests.TryGetValue(quest.QuestID, out record))
            {
                return false;
            }

            foreach (var objective in quest.Objectives)
            {
                var value = 0;
                record.ObjectiveProgress.TryGetValue(objective.ObjectiveId, out value);
                if (value < objective.TargetCount)
                {
                    return false;
                }
            }

            return true;
        }

        private void IncrementQuestExecutionCount(long questId)
        {
            int count;
            _statistics.TaskExecutionCounts.TryGetValue(questId, out count);
            _statistics.TaskExecutionCounts[questId] = count + 1;
        }

        private double GetQuestCooldownExpiry(QuestDefinition quest)
        {
            return CurrentTime() + Math.Max(0, quest.Cooldown);
        }

        private void ProcessProgress(BasePlayer player, string objectiveType, string target, int amount, ulong skinId, Item item, string entityName)
        {
            if (player == null || amount <= 0)
            {
                return;
            }

            var progress = GetPlayerProgress(player.userID);
            if (progress.ActiveQuests.Count == 0)
            {
                return;
            }

            foreach (var active in progress.ActiveQuests.ToList())
            {
                QuestDefinition quest;
                if (!_allQuestsById.TryGetValue(active.Key, out quest))
                {
                    continue;
                }

                foreach (var objective in quest.Objectives)
                {
                    if (!string.Equals(objective.Type, objectiveType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!IsObjectiveMatch(objective, target, skinId, entityName))
                    {
                        continue;
                    }

                    ApplyObjectiveProgress(player, quest, objective, amount, false, item, entityName);
                }
            }
        }

        private void ApplyObjectiveProgress(BasePlayer player, QuestDefinition quest, ObjectiveDefinition objective, int amount, bool fromSubmission, Item item = null, string entityName = null)
        {
            var progress = GetPlayerProgress(player.userID);
            ActiveQuestRecord record;
            if (!progress.ActiveQuests.TryGetValue(quest.QuestID, out record))
            {
                return;
            }

            int current;
            record.ObjectiveProgress.TryGetValue(objective.ObjectiveId, out current);
            var next = Mathf.Clamp(current + amount, 0, objective.TargetCount);
            if (next == current)
            {
                return;
            }

            record.ObjectiveProgress[objective.ObjectiveId] = next;
            SavePlayerData();

            var toast = string.Format("{0}: {1} {2}/{3}", GetQuestTitle(player, quest), GetObjectiveTitle(player, objective), next, objective.TargetCount);
            ShowToast(player, toast);
            Interface.CallHook("OnQuestProgress", player.userID, GetQuestTypeCode(quest), entityName ?? objective.Target, objective.TargetSkinId.ToString(), item == null ? null : new List<Item> { item }, amount);

            if (IsQuestReadyToClaim(player, quest))
            {
                SendLocalizedMessage(player, "Msg.QuestReady", GetQuestTitle(player, quest));
            }

            if (GetUiState(player.userID).MainOpen)
            {
                OpenMainUi(player);
            }
            RenderMiniUiIfNeeded(player);
        }

        private int GetQuestTypeCode(QuestDefinition quest)
        {
            switch ((quest.QuestType ?? string.Empty).ToLowerInvariant())
            {
                case "gather": return 1;
                case "loot": return 2;
                case "entitykill": return 3;
                case "craft": return 4;
                case "research": return 5;
                case "grade": return 6;
                case "swipe": return 7;
                case "deploy": return 8;
                case "purchasefromnpc": return 9;
                case "hackcrate": return 10;
                case "recycleitem": return 11;
                case "fishing": return 12;
                case "growseedlings": return 13;
                case "delivery": return 14;
                default: return 0;
            }
        }

        private bool IsObjectiveMatch(ObjectiveDefinition objective, string target, ulong skinId, string entityName = null)
        {
            if (objective.TargetSkinId != 0 && objective.TargetSkinId != skinId)
            {
                return false;
            }

            var tokens = GetObjectiveMatchTokens(objective).ToList();
            if (tokens.Count == 0)
            {
                return true;
            }

            var candidates = GetObjectiveMatchCandidates(objective, target, entityName);
            if (string.Equals(objective.MatchMode, "contains", StringComparison.OrdinalIgnoreCase))
            {
                return candidates.Any(candidate => tokens.Any(token => !string.IsNullOrEmpty(candidate) && candidate.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (string.Equals(objective.MatchMode, "prefab", StringComparison.OrdinalIgnoreCase))
            {
                return candidates.Any(candidate => tokens.Any(token =>
                    !string.IsNullOrEmpty(candidate) &&
                    (candidate.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     candidate.StartsWith(token, StringComparison.OrdinalIgnoreCase))));
            }

            return candidates.Any(candidate => tokens.Any(token => string.Equals(token, candidate, StringComparison.OrdinalIgnoreCase)));
        }

        private IEnumerable<string> GetObjectiveMatchCandidates(ObjectiveDefinition objective, string target, string entityName)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddObjectiveMatchCandidate(candidates, target);
            AddObjectiveMatchCandidate(candidates, entityName);
            return candidates;
        }

        private IEnumerable<string> GetObjectiveMatchTokens(ObjectiveDefinition objective)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (objective == null)
            {
                return tokens;
            }

            AddObjectiveMatchCandidate(tokens, objective.Target);
            if (objective.MatchAliases != null)
            {
                foreach (var alias in objective.MatchAliases)
                {
                    AddObjectiveMatchCandidate(tokens, alias);
                }
            }

            return tokens;
        }

        private void AddObjectiveMatchCandidate(HashSet<string> candidates, string value)
        {
            if (candidates == null || string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (var chunk in value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = chunk.Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    candidates.Add(token);
                }
            }
        }

        private int GetObjectiveProgress(ulong userId, long questId, string objectiveId)
        {
            var progress = GetPlayerProgress(userId);
            ActiveQuestRecord record;
            if (!progress.ActiveQuests.TryGetValue(questId, out record))
            {
                return 0;
            }

            int value;
            return record.ObjectiveProgress.TryGetValue(objectiveId, out value) ? value : 0;
        }

        private int GetObjectiveDisplayProgress(ulong userId, long questId, ObjectiveDefinition objective)
        {
            if (objective == null)
            {
                return 0;
            }

            return Mathf.Clamp(GetObjectiveProgress(userId, questId, objective.ObjectiveId), 0, Math.Max(1, objective.TargetCount));
        }

        private PlayerProgress GetPlayerProgress(ulong userId)
        {
            return EnsurePlayerData(userId);
        }

        private PlayerProgress EnsurePlayerData(ulong userId)
        {
            PlayerProgress progress;
            if (!_storedData.Players.TryGetValue(userId.ToString(), out progress))
            {
                progress = BuildDefaultPlayerProgress();
                _storedData.Players[userId.ToString()] = progress;
            }

            return progress;
        }

        private PlayerProgress BuildDefaultPlayerProgress()
        {
            return new PlayerProgress
            {
                NotificationSettings = new NotificationSettings
                {
                    ProgressToasts = _config.Notifications.ProgressToasts,
                    ChatMessages = _config.Notifications.ChatMessages,
                    MiniListAutoShow = _config.Notifications.AutoOpenMiniList
                }
            };
        }

        private PlayerUiState GetUiState(ulong userId)
        {
            PlayerUiState state;
            if (!_uiStates.TryGetValue(userId, out state))
            {
                state = new PlayerUiState();
                _uiStates[userId] = state;
            }

            return state;
        }

        private void EnsureValidMainSelection(BasePlayer player)
        {
            var state = GetUiState(player.userID);
            if (state.Tab == "Questlines")
            {
                if (state.SelectedQuestlineId == 0 && _questlineCatalog.Questlines.Count > 0)
                {
                    state.SelectedQuestlineId = _questlineCatalog.Questlines[0].QuestlineId;
                }

                QuestlineDefinition line;
                if (!_questlinesById.TryGetValue(state.SelectedQuestlineId, out line))
                {
                    state.SelectedQuestId = 0;
                    return;
                }

                if (!line.StageQuestIds.Contains(state.SelectedQuestId))
                {
                    state.SelectedQuestId = line.StageQuestIds.FirstOrDefault();
                }

                return;
            }

            var filtered = GetFilteredQuests(player, state.Tab, state.Filter).ToList();
            if (filtered.Count == 0)
            {
                filtered = GetFilteredQuests(player, state.Tab, "All").ToList();
            }

            if (filtered.Count == 0)
            {
                state.SelectedQuestId = 0;
                return;
            }

            if (!filtered.Any(x => x.QuestID == state.SelectedQuestId))
            {
                state.SelectedQuestId = filtered[0].QuestID;
            }
        }

        private QuestDefinition FindQuest(long questId)
        {
            QuestDefinition quest;
            return _allQuestsById.TryGetValue(questId, out quest) ? quest : null;
        }

        private bool HasSubmissionObjective(QuestDefinition quest)
        {
            return quest.Objectives.Any(x => x.SubmissionRequired);
        }

        private IEnumerable<QuestDefinition> GetFilteredQuests(BasePlayer player, string tab, string filter)
        {
            IEnumerable<QuestDefinition> source = tab == "Daily" ? _dailyCatalog.DailyQuests : _questCatalog.Quests;
            foreach (var quest in source.OrderBy(x => x.QuestCategory).ThenBy(x => x.QuestID))
            {
                if (!_config.Ui.ShowLockedQuests && GetQuestStatus(player, quest) == QuestStatus.Locked)
                {
                    continue;
                }

                if (!_config.Ui.ShowCompletedInList && GetQuestStatus(player, quest) == QuestStatus.Completed)
                {
                    continue;
                }

                if (filter != "All" && !string.Equals(GetQuestStatus(player, quest).ToString(), filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return quest;
            }
        }

        private long GetFirstSelectableId(BasePlayer player, string tab)
        {
            if (tab == "Questlines")
            {
                var firstLine = _questlineCatalog.Questlines.FirstOrDefault();
                return firstLine == null ? 0 : firstLine.StageQuestIds.FirstOrDefault();
            }

            var first = GetFilteredQuests(player, tab, "All").FirstOrDefault();
            return first == null ? 0 : first.QuestID;
        }

        private string GetNextFilterValue(string current, bool forward)
        {
            var filters = GetFilterValues();
            var index = Array.IndexOf(filters, current);
            if (index < 0)
            {
                index = 0;
            }

            index += forward ? 1 : -1;
            if (index < 0)
            {
                index = filters.Length - 1;
            }
            else if (index >= filters.Length)
            {
                index = 0;
            }

            return filters[index];
        }

        private string[] GetFilterValues()
        {
            return new[] { "All", "Available", "Started", "ReadyToClaim", "Completed", "Locked", "OnCooldown" };
        }

        private bool IsDailyQuest(QuestDefinition quest)
        {
            return string.Equals(quest.QuestTabType, "Daily", StringComparison.OrdinalIgnoreCase);
        }

        private string GetQuestTitle(BasePlayer player, QuestDefinition quest)
        {
            return GetLocalizedQuestText(player.UserIDString, quest.QuestDisplayName, quest.QuestDisplayNameMultiLanguage);
        }

        private string GetQuestDescription(BasePlayer player, QuestDefinition quest)
        {
            return GetLocalizedQuestText(player.UserIDString, quest.QuestDescription, quest.QuestDescriptionMultiLanguage);
        }

        private string GetQuestMissionText(BasePlayer player, QuestDefinition quest)
        {
            return GetLocalizedQuestText(player.UserIDString, quest.QuestMissions, quest.QuestMissionsMultiLanguage);
        }

        private string GetObjectiveTitle(BasePlayer player, ObjectiveDefinition objective)
        {
            return GetLocalizedQuestText(player.UserIDString, objective.Description, objective.DescriptionMultiLanguage);
        }

        private string GetQuestlineTitle(BasePlayer player, QuestlineDefinition line)
        {
            return GetLocalizedQuestText(player.UserIDString, line.DisplayName, line.DisplayNameMultiLanguage);
        }

        private int GetObjectiveIconItemId(ObjectiveDefinition objective)
        {
            var resolved = ResolveItemIconId(objective.Icon, objective.Target);
            return resolved ?? 0;
        }

        private string GetLocalizedQuestText(string userId, string fallback, Dictionary<string, string> localized)
        {
            if (localized != null && localized.Count > 0)
            {
                var language = GetPlayerLanguage(userId);
                string value;
                if (!string.IsNullOrEmpty(language) && localized.TryGetValue(language, out value) && !string.IsNullOrEmpty(value))
                {
                    return value;
                }

                if (localized.TryGetValue("ru", out value) && !string.IsNullOrEmpty(value))
                {
                    return value;
                }

                if (localized.TryGetValue("en", out value) && !string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return string.IsNullOrEmpty(fallback) ? "-" : fallback;
        }

        private string GetPlayerLanguage(string userId)
        {
            try
            {
                return string.IsNullOrEmpty(userId) ? "ru" : lang.GetLanguage(userId);
            }
            catch
            {
                return "ru";
            }
        }

        private string GetQuestCompletionText(BasePlayer player, QuestDefinition quest)
        {
            var chunks = new List<string>();
            foreach (var objective in quest.Objectives.OrderBy(x => x.Order))
            {
                var value = GetObjectiveDisplayProgress(player.userID, quest.QuestID, objective);
                chunks.Add(string.Format("{0}: {1}/{2}", GetObjectiveTitle(player, objective), value, objective.TargetCount));
            }

            return string.Join(" | ", chunks.ToArray());
        }

        private string GetQuestListSubtitle(BasePlayer player, QuestDefinition quest)
        {
            return TrimUiText(GetQuestMissionText(player, quest), 42);
        }

        private string GetFilterDisplayText(BasePlayer player, string filter)
        {
            return string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase)
                ? GetMsg("Label.FilterState", player.UserIDString)
                : GetFilterOptionText(player, filter);
        }

        private string GetFilterOptionText(BasePlayer player, string filter)
        {
            return string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase)
                ? GetMsg("Filter.All", player.UserIDString)
                : GetMsg("Status." + filter, player.UserIDString);
        }

        private void AddFilterIcon(CuiElementContainer container, string parent, string filter, string color)
        {
            switch ((filter ?? string.Empty).ToLowerInvariant())
            {
                case "available":
                    AddPixelBlock(container, parent, parent + ".v", color, 10f, 4f, 12f, 18f);
                    AddPixelBlock(container, parent, parent + ".h", color, 4f, 10f, 18f, 12f);
                    return;
                case "started":
                    AddPixelBlock(container, parent, parent + ".shaft", color, 4f, 10f, 13f, 12f);
                    AddPixelBlock(container, parent, parent + ".head1", color, 11f, 10f, 17f, 16f);
                    AddPixelBlock(container, parent, parent + ".head2", color, 11f, 6f, 17f, 12f);
                    return;
                case "readytoclaim":
                    AddPixelBlock(container, parent, parent + ".boxTop", color, 5f, 14f, 17f, 16f);
                    AddPixelBlock(container, parent, parent + ".boxBot", color, 5f, 6f, 17f, 8f);
                    AddPixelBlock(container, parent, parent + ".boxLeft", color, 5f, 6f, 7f, 16f);
                    AddPixelBlock(container, parent, parent + ".boxRight", color, 15f, 6f, 17f, 16f);
                    AddPixelBlock(container, parent, parent + ".mid", color, 7f, 10f, 15f, 12f);
                    return;
                case "completed":
                    AddPixelBlock(container, parent, parent + ".tick1", color, 5f, 9f, 9f, 13f);
                    AddPixelBlock(container, parent, parent + ".tick2", color, 8f, 8f, 17f, 17f);
                    return;
                case "locked":
                    AddPixelBlock(container, parent, parent + ".bodyTop", color, 6f, 12f, 16f, 14f);
                    AddPixelBlock(container, parent, parent + ".bodyLeft", color, 6f, 6f, 8f, 14f);
                    AddPixelBlock(container, parent, parent + ".bodyRight", color, 14f, 6f, 16f, 14f);
                    AddPixelBlock(container, parent, parent + ".bodyBot", color, 6f, 6f, 16f, 8f);
                    AddPixelBlock(container, parent, parent + ".arcLeft", color, 8f, 14f, 10f, 18f);
                    AddPixelBlock(container, parent, parent + ".arcRight", color, 12f, 14f, 14f, 18f);
                    AddPixelBlock(container, parent, parent + ".arcTop", color, 9f, 17f, 13f, 19f);
                    return;
                case "oncooldown":
                    AddPixelBlock(container, parent, parent + ".top", color, 6f, 15f, 16f, 17f);
                    AddPixelBlock(container, parent, parent + ".bot", color, 6f, 5f, 16f, 7f);
                    AddPixelBlock(container, parent, parent + ".diag1", color, 7f, 13f, 15f, 9f);
                    AddPixelBlock(container, parent, parent + ".diag2", color, 7f, 9f, 15f, 13f);
                    return;
                default:
                    AddPixelBlock(container, parent, parent + ".line1", color, 4f, 15f, 18f, 17f);
                    AddPixelBlock(container, parent, parent + ".line2", color, 4f, 10f, 18f, 12f);
                    AddPixelBlock(container, parent, parent + ".line3", color, 4f, 5f, 18f, 7f);
                    return;
            }
        }

        private string GetQuestGroupStateKey(string tab, string group)
        {
            return string.Format("{0}|{1}", NormalizeGroupStateToken(tab), NormalizeGroupStateToken(group));
        }

        private string NormalizeGroupStateToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "general";
            }

            var chars = value
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();

            return new string(chars);
        }

        private bool IsQuestGroupCollapsed(PlayerUiState state, string tab, string group)
        {
            return state != null && state.CollapsedGroups.Contains(GetQuestGroupStateKey(tab, group));
        }

        private void ToggleQuestGroup(PlayerUiState state, string key)
        {
            if (state == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!state.CollapsedGroups.Add(key))
            {
                state.CollapsedGroups.Remove(key);
            }
        }

        private string GetQuestListGlyph(QuestDefinition quest)
        {
            switch ((quest.QuestType ?? string.Empty).ToLowerInvariant())
            {
                case "gather":
                case "loot":
                    return "//";
                case "craft":
                    return "[]";
                case "delivery":
                    return "->";
                case "entitykill":
                    return "XX";
                case "deploy":
                    return "##";
                default:
                    return "Q";
            }
        }

        private string GetQuestRowMeta(BasePlayer player, QuestDefinition quest)
        {
            if (quest.RequiredQuestIds.Count > 0 || quest.QuestlineId > 0)
            {
                return TrimUiText(GetQuestStageHint(player, quest), 24);
            }

            return string.Empty;
        }

        private string GetQuestlineProgress(BasePlayer player, QuestlineDefinition line)
        {
            var progress = GetPlayerProgress(player.userID);
            var total = line.StageQuestIds.Count;
            var completed = line.StageQuestIds.Count(progress.CompletedQuests.Contains);
            return string.Format("{0}/{1}", completed, total);
        }

        private string GetQuestHeaderSubtitle(BasePlayer player, QuestDefinition quest, QuestlineDefinition line)
        {
            if (line != null)
            {
                return string.Format("{0} | {1}", GetQuestlineTitle(player, line), GetQuestlineProgress(player, line));
            }

            QuestlineDefinition linkedLine;
            if (quest.QuestlineId > 0 && _questlinesById.TryGetValue(quest.QuestlineId, out linkedLine))
            {
                return string.Format("{0} | {1}", GetQuestlineTitle(player, linkedLine), GetQuestlineProgress(player, linkedLine));
            }

            return string.Empty;
        }

        private string GetQuestBadgeText(BasePlayer player, QuestDefinition quest, QuestlineDefinition line)
        {
            if (line != null)
            {
                return GetMsg("Badge.Questline", player.UserIDString);
            }

            if (IsDailyQuest(quest))
            {
                return GetMsg("Badge.DailyQuest", player.UserIDString);
            }

            return GetMsg("Badge.StandardQuest", player.UserIDString);
        }

        private string GetQuestStageHint(BasePlayer player, QuestDefinition quest)
        {
            if (quest.RequiredQuestIds.Count > 0)
            {
                var requiredQuest = FindQuest(quest.RequiredQuestIds[0]);
                if (requiredQuest != null)
                {
                    return "Requires " + GetQuestTitle(player, requiredQuest);
                }
            }

            if (quest.QuestlineId > 0 && quest.QuestlineOrder > 1)
            {
                return "Stage " + quest.QuestlineOrder;
            }

            return GetQuestCompletionText(player, quest);
        }

        private string GetPrizeTitle(PrizeDefinition prize)
        {
            if (!string.IsNullOrEmpty(prize.PrizeName))
            {
                return prize.PrizeName;
            }

            switch ((prize.PrizeType ?? string.Empty).ToLowerInvariant())
            {
                case "item":
                case "customitem":
                    return string.Format("{0} x{1}", prize.ItemShortName, prize.ItemAmount);
                case "command":
                    return "Command reward";
                case "rp":
                    return string.Format("RP x{0}", prize.ItemAmount);
                case "xp":
                    return string.Format("XP x{0}", prize.ItemAmount);
                default:
                    return prize.PrizeType;
            }
        }

        private bool IsPinnedRewardPrize(PrizeDefinition prize)
        {
            if (prize == null)
            {
                return false;
            }

            var type = (prize.PrizeType ?? string.Empty).ToLowerInvariant();
            return type == "xp" || type == "rp";
        }

        private int GetPrizeIconItemId(PrizeDefinition prize)
        {
            string primary;
            switch ((prize.PrizeType ?? string.Empty).ToLowerInvariant())
            {
                case "item":
                case "customitem":
                case "blueprint":
                    primary = prize.ItemShortName;
                    break;
                case "blueprintfragments":
                    primary = "blueprintfragment";
                    break;
                default:
                    primary = prize.Icon;
                    break;
            }

            var resolved = ResolveItemIconId(prize.Icon, primary);
            return resolved ?? 0;
        }

        private string GetPrizeCardCode(PrizeDefinition prize)
        {
            switch ((prize.PrizeType ?? string.Empty).ToLowerInvariant())
            {
                case "item":
                case "customitem":
                    var itemToken = string.IsNullOrEmpty(prize.ItemShortName) ? "ITEM" : prize.ItemShortName.Split('.', '_')[0].ToUpperInvariant();
                    return itemToken.Length <= 4 ? itemToken : itemToken.Substring(0, 4);
                case "blueprint":
                    return "BP";
                case "command":
                    return "CMD";
                case "rp":
                    return "RP";
                case "xp":
                    return "XP";
                default:
                    return TrimUiText((prize.PrizeType ?? "RWD").ToUpperInvariant(), 4);
            }
        }

        private int? ResolveItemIconId(params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var itemId = TryGetItemId(candidate);
                if (itemId.HasValue)
                {
                    return itemId.Value;
                }
            }

            return null;
        }

        private int? TryGetItemId(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            var tokens = new[]
            {
                raw,
                raw.ToLowerInvariant(),
                raw.Replace(" ", string.Empty).ToLowerInvariant(),
                raw.Replace("_", ".").ToLowerInvariant(),
                raw.Replace("-", ".").ToLowerInvariant()
            };

            foreach (var token in tokens.Distinct())
            {
                var definition = ItemManager.FindItemDefinition(token);
                if (definition != null)
                {
                    return definition.itemid;
                }
            }

            return null;
        }

        private string GetPrizeAmountText(PrizeDefinition prize)
        {
            if (prize.ItemAmount > 0)
            {
                return "x" + prize.ItemAmount;
            }

            return "-";
        }

        private string GetProfileTrackedQuestText(BasePlayer player)
        {
            var trackedId = GetPlayerProgress(player.userID).TrackedQuestId;
            var quest = FindQuest(trackedId);
            return quest == null ? "-" : TrimUiText(GetQuestTitle(player, quest), 20);
        }

        private string GetFooterPrimaryText(BasePlayer player, QuestDefinition quest, QuestlineDefinition line)
        {
            if (line != null)
            {
                return string.Format("{0} | {1}", GetQuestlineTitle(player, line), GetMsg("Label.TrackedSummary", player.UserIDString) + ": " + GetProfileTrackedQuestText(player));
            }

            return string.Format("{0} | {1}: {2}", quest.QuestCategory, GetMsg("Label.TrackedSummary", player.UserIDString), GetProfileTrackedQuestText(player));
        }

        private string GetFooterSecondaryText(BasePlayer player, QuestDefinition quest)
        {
            return TrimUiText(GetQuestCompletionText(player, quest), 96);
        }

        private string GetFooterTimerText(BasePlayer player, QuestDefinition quest, QuestStatus status)
        {
            var progress = GetPlayerProgress(player.userID);
            double cooldown;
            if (progress.Cooldowns.TryGetValue(quest.QuestID, out cooldown) && cooldown > CurrentTime())
            {
                return FormatDuration(cooldown - CurrentTime());
            }

            if (status == QuestStatus.ReadyToClaim)
            {
                return "READY";
            }

            if (status == QuestStatus.Started)
            {
                return GetQuestProgressPercent(player, quest);
            }

            var nextCooldown = progress.Cooldowns
                .Where(x => x.Key >= 0)
                .Where(x => x.Value > CurrentTime())
                .OrderBy(x => x.Value)
                .FirstOrDefault();

            return nextCooldown.Equals(default(KeyValuePair<long, double>)) ? "-" : FormatDuration(nextCooldown.Value - CurrentTime());
        }

        private string GetQuestProgressPercent(BasePlayer player, QuestDefinition quest)
        {
            if (quest.Objectives.Count == 0)
            {
                return "0%";
            }

            var total = 0f;
            foreach (var objective in quest.Objectives)
            {
                var target = Math.Max(1, objective.TargetCount);
                total += Mathf.Clamp01(GetObjectiveDisplayProgress(player.userID, quest.QuestID, objective) / (float)target);
            }

            return Mathf.RoundToInt((total / quest.Objectives.Count) * 100f) + "%";
        }

        private string TrimUiText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private string GetMsg(string key, string userId)
        {
            return lang.GetMessage(key, this, userId);
        }

        private void SendLocalizedMessage(BasePlayer player, string key, params object[] args)
        {
            var text = string.Format(GetMsg(key, player.UserIDString), args);
            var settings = GetPlayerProgress(player.userID).NotificationSettings;

            if (settings.ChatMessages)
            {
                player.ChatMessage(text);
            }

            if (_config.SettingsNotify.useNotify && Notify != null)
            {
                Notify.Call("SendNotify", player, text, _config.SettingsNotify.typeNotify);
            }
            else if (_config.SettingsIQChat.UIAlertUse && IQChat != null)
            {
                IQChat.Call("API_ALERT_PLAYER", player, _config.SettingsIQChat.CustomAvatar, _config.SettingsIQChat.CustomPrefix, text);
            }

            ShowToast(player, text);
        }

        private void PublishStatistics()
        {
            if (!_config.StatisticsCollectionSettings.useStatistics || string.IsNullOrEmpty(_config.StatisticsCollectionSettings.discordWebhookUrl))
            {
                return;
            }

            var top = _statistics.TaskExecutionCounts
                .OrderByDescending(x => x.Value)
                .Take(5)
                .Select(x =>
                {
                    var quest = FindQuest(x.Key);
                    return string.Format("#{0} {1}: {2}", x.Key, quest == null ? "Unknown" : quest.QuestDisplayName, x.Value);
                })
                .ToArray();

            var payload = new
            {
                embeds = new object[]
                {
                    new
                    {
                        title = "BQuest statistics",
                        color = 15374885,
                        fields = new object[]
                        {
                            new { name = "Taken", value = _statistics.TakenTasks.ToString(), inline = true },
                            new { name = "Completed", value = _statistics.CompletedTasks.ToString(), inline = true },
                            new { name = "Declined", value = _statistics.DeclinedTasks.ToString(), inline = true }
                        }
                    },
                    new
                    {
                        title = "Top quests",
                        color = 15105570,
                        description = top.Length == 0 ? "No data." : string.Join("\n", top)
                    }
                }
            };

            webrequest.Enqueue(_config.StatisticsCollectionSettings.discordWebhookUrl, JsonConvert.SerializeObject(payload), delegate(int code, string response)
            {
                if (code >= 200 && code < 300)
                {
                    Puts("BQuest statistics published to Discord.");
                    return;
                }

                PrintWarning(string.Format("Discord statistics publish failed. Code: {0}", code));
            }, this, RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        }

        private void RenderMiniUiIfNeeded(BasePlayer player)
        {
            if (GetUiState(player.userID).MiniOpen)
            {
                RenderMiniUi(player);
            }
        }

        private string GetQuestPermission(string suffix)
        {
            return "BQuest." + suffix;
        }

        private string GetStatusColor(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.Available:
                    return _config.Theme.PanelAlt;
                case QuestStatus.Started:
                    return "0.20 0.28 0.38 0.95";
                case QuestStatus.ReadyToClaim:
                    return "0.20 0.36 0.25 0.95";
                case QuestStatus.OnCooldown:
                    return "0.38 0.26 0.12 0.95";
                case QuestStatus.Completed:
                    return "0.24 0.24 0.24 0.95";
                default:
                    return "0.15 0.15 0.17 0.95";
            }
        }

        private string GetChromePanelColor()
        {
            return "0.08 0.08 0.08 0.97";
        }

        private string GetChromeHeaderColor()
        {
            return "0.18 0.16 0.13 0.98";
        }

        private string GetChromeInsetColor()
        {
            return "0.12 0.11 0.10 0.98";
        }

        private string GetChromeMutedColor()
        {
            return "0.20 0.18 0.15 0.98";
        }

        private string GetChromeBorderColor()
        {
            return "0.83 0.70 0.36 0.85";
        }

        private string GetUiWindowColor()
        {
            return "0.08 0.08 0.09 0.95";
        }

        private string GetUiSidebarColor()
        {
            return "0.10 0.10 0.11 0.94";
        }

        private string GetUiProfileColor()
        {
            return "0.14 0.14 0.15 0.96";
        }

        private string GetUiSoftColor()
        {
            return "0.15 0.15 0.16 0.96";
        }

        private string GetUiRowColor()
        {
            return "0.12 0.12 0.13 0.98";
        }

        private string GetUiSelectedColor()
        {
            return "0.18 0.17 0.15 0.98";
        }

        private string GetUiReadyColor()
        {
            return "0.17 0.18 0.15 0.98";
        }

        private string GetQuestListRowColor(QuestStatus status, bool isSelected)
        {
            if (isSelected)
            {
                return GetUiSelectedColor();
            }

            return status == QuestStatus.ReadyToClaim ? GetUiReadyColor() : GetUiRowColor();
        }

        private string GetUiBorderColor()
        {
            return "0.22 0.22 0.24 0.95";
        }

        private string GetUiGoldColor()
        {
            return "0.90 0.76 0.36 1.0";
        }

        private string GetUiGoldLineColor()
        {
            return "0.80 0.66 0.30 0.70";
        }

        private string GetUiGoldFrameColor()
        {
            return "0.90 0.76 0.36 0.50";
        }

        private string GetUiFooterPanelColor()
        {
            return "0.16 0.15 0.14 0.92";
        }

        private string Rect(float x, float y, float w, float h)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", x, y, x + w, y + h);
        }

        private string FormatDuration(double seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            var time = TimeSpan.FromSeconds(seconds);
            if (time.TotalHours >= 1)
            {
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)time.TotalHours, time.Minutes, time.Seconds);
            }

            return string.Format("{0:D2}:{1:D2}", time.Minutes, time.Seconds);
        }

        private double CurrentTime()
        {
            return Interface.Oxide.Now;
        }

        private long ToLong(string value)
        {
            long result;
            return long.TryParse(value, out result) ? result : 0;
        }

        private void AddPanel(CuiElementContainer container, string parent, string name, string color, string anchorMin, string anchorMax)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = color },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
            }, parent, name);
        }

        private void AddPanel(CuiElementContainer container, string parent, string name, string color, string anchorMin, string anchorMax, string offsetMin, string offsetMax)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = color },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax
                }
            }, parent, name);
        }

        private void AddLine(CuiElementContainer container, string parent, string color, string anchorMin, string anchorMax)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = color },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
            }, parent);
        }

        private void AddPixelBlock(CuiElementContainer container, string parent, string name, string color, float xMin, float yMin, float xMax, float yMax)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = color },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "0 0",
                    OffsetMin = string.Format(CultureInfo.InvariantCulture, "{0} {1}", xMin, yMin),
                    OffsetMax = string.Format(CultureInfo.InvariantCulture, "{0} {1}", xMax, yMax)
                }
            }, parent, name);
        }

        private void AddVerticalScrollView(CuiElementContainer container, string parent, string name, float contentHeight, float scrollbarSize = 6f)
        {
            container.Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiScrollViewComponent
                    {
                        ContentTransform = new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = string.Format(CultureInfo.InvariantCulture, "0 {0}", -contentHeight),
                            OffsetMax = string.Format(CultureInfo.InvariantCulture, "{0} 0", -scrollbarSize - 4f)
                        },
                        Elasticity = float.MinValue,
                        Horizontal = false,
                        MovementType = ScrollRect.MovementType.Clamped,
                        ScrollSensitivity = 26f,
                        Vertical = true,
                        VerticalScrollbar = new CuiScrollbar
                        {
                            AutoHide = false,
                            HandleColor = GetUiGoldColor(),
                            HighlightColor = GetUiGoldColor(),
                            Invert = false,
                            PressedColor = GetUiGoldColor(),
                            Size = scrollbarSize,
                            TrackColor = "0 0 0 0"
                        }
                    },
                    new CuiNeedsCursorComponent()
                }
            });
        }

        private void AddThinPanel(CuiElementContainer container, string parent, string name, string fillColor, string borderColor, string anchorMin, string anchorMax)
        {
            AddPanel(container, parent, name, fillColor, anchorMin, anchorMax);
            AddLine(container, name, borderColor, "0 0.994", "1 1");
            AddLine(container, name, borderColor, "0 0", "1 0.006");
            AddLine(container, name, borderColor, "0 0", "0.004 1");
            AddLine(container, name, borderColor, "0.996 0", "1 1");
        }

        private void AddSquareThinPanel(CuiElementContainer container, string parent, string name, string fillColor, string borderColor, float anchorX, float anchorY, float size)
        {
            var half = size * 0.5f;
            container.Add(new CuiPanel
            {
                Image = { Color = fillColor },
                RectTransform =
                {
                    AnchorMin = string.Format(CultureInfo.InvariantCulture, "{0} {1}", anchorX, anchorY),
                    AnchorMax = string.Format(CultureInfo.InvariantCulture, "{0} {1}", anchorX, anchorY),
                    OffsetMin = string.Format(CultureInfo.InvariantCulture, "{0} {1}", -half, -half),
                    OffsetMax = string.Format(CultureInfo.InvariantCulture, "{0} {1}", half, half)
                }
            }, parent, name);

            AddLine(container, name, borderColor, "0 0.994", "1 1");
            AddLine(container, name, borderColor, "0 0", "1 0.006");
            AddLine(container, name, borderColor, "0 0", "0.004 1");
            AddLine(container, name, borderColor, "0.996 0", "1 1");
        }

        private void AddSquarePanel(CuiElementContainer container, string parent, string name, string color, float anchorX, float anchorY, float size)
        {
            var half = size * 0.5f;
            container.Add(new CuiPanel
            {
                Image = { Color = color },
                RectTransform =
                {
                    AnchorMin = string.Format(CultureInfo.InvariantCulture, "{0} {1}", anchorX, anchorY),
                    AnchorMax = string.Format(CultureInfo.InvariantCulture, "{0} {1}", anchorX, anchorY),
                    OffsetMin = string.Format(CultureInfo.InvariantCulture, "{0} {1}", -half, -half),
                    OffsetMax = string.Format(CultureInfo.InvariantCulture, "{0} {1}", half, half)
                }
            }, parent, name);
        }

        private void AddChromePanel(CuiElementContainer container, string parent, string name, string color, string anchorMin, string anchorMax)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = color },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
            }, parent, name);

            container.Add(new CuiPanel
            {
                Image = { Color = GetChromeBorderColor() },
                RectTransform = { AnchorMin = "0 0.98", AnchorMax = "1 1" }
            }, name);

            container.Add(new CuiPanel
            {
                Image = { Color = GetChromeBorderColor() },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.02" }
            }, name);

            container.Add(new CuiPanel
            {
                Image = { Color = GetChromeBorderColor() },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.01 1" }
            }, name);

            container.Add(new CuiPanel
            {
                Image = { Color = GetChromeBorderColor() },
                RectTransform = { AnchorMin = "0.99 0", AnchorMax = "1 1" }
            }, name);
        }

        private void AddSquareChromePanel(CuiElementContainer container, string parent, string name, string color, float anchorX, float anchorY, float size)
        {
            var half = size * 0.5f;
            container.Add(new CuiPanel
            {
                Image = { Color = color },
                RectTransform =
                {
                    AnchorMin = string.Format(CultureInfo.InvariantCulture, "{0} {1}", anchorX, anchorY),
                    AnchorMax = string.Format(CultureInfo.InvariantCulture, "{0} {1}", anchorX, anchorY),
                    OffsetMin = string.Format(CultureInfo.InvariantCulture, "{0} {1}", -half, -half),
                    OffsetMax = string.Format(CultureInfo.InvariantCulture, "{0} {1}", half, half)
                }
            }, parent, name);

            container.Add(new CuiPanel
            {
                Image = { Color = GetChromeBorderColor() },
                RectTransform = { AnchorMin = "0 0.97", AnchorMax = "1 1" }
            }, name);

            container.Add(new CuiPanel
            {
                Image = { Color = GetChromeBorderColor() },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.03" }
            }, name);

            container.Add(new CuiPanel
            {
                Image = { Color = GetChromeBorderColor() },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.03 1" }
            }, name);

            container.Add(new CuiPanel
            {
                Image = { Color = GetChromeBorderColor() },
                RectTransform = { AnchorMin = "0.97 0", AnchorMax = "1 1" }
            }, name);
        }

        private void AddCornerFrame(CuiElementContainer container, string parent, string color)
        {
            AddLine(container, parent, color, "0.00 0.992", "0.20 1.00");
            AddLine(container, parent, color, "0.00 0.80", "0.006 1.00");
            AddLine(container, parent, color, "0.80 0.992", "1.00 1.00");
            AddLine(container, parent, color, "0.994 0.80", "1.00 1.00");
            AddLine(container, parent, color, "0.00 0.00", "0.20 0.008");
            AddLine(container, parent, color, "0.00 0.00", "0.006 0.20");
            AddLine(container, parent, color, "0.80 0.00", "1.00 0.008");
            AddLine(container, parent, color, "0.994 0.00", "1.00 0.20");
        }

        private void AddLabel(CuiElementContainer container, string parent, string text, int size, string color, string anchorMin, string anchorMax, TextAnchor align)
        {
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = text,
                    FontSize = size,
                    Align = align,
                    Color = color
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            }, parent);
        }

        private void AddButton(CuiElementContainer container, string parent, string color, string text, int size, string anchorMin, string anchorMax, string command)
        {
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = color,
                    Command = command
                },
                Text =
                {
                    Text = text,
                    FontSize = size,
                    Align = TextAnchor.MiddleCenter,
                    Color = _config.Theme.Text
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            }, parent);
        }

        private void AddButton(CuiElementContainer container, string parent, string color, string text, int size, string rect, string command)
        {
            var parts = rect.Split(' ');
            AddButton(container, parent, color, text, size, parts[0] + " " + parts[1], parts[2] + " " + parts[3], command);
        }

        private void AddStyledButton(CuiElementContainer container, string parent, string color, string text, int size, string textColor, string anchorMin, string anchorMax, string command)
        {
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = color,
                    Command = command
                },
                Text =
                {
                    Text = text,
                    FontSize = size,
                    Align = TextAnchor.MiddleCenter,
                    Color = textColor
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            }, parent);
        }

        private void AddOverlayButton(CuiElementContainer container, string parent, string command)
        {
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0.01",
                    Command = command
                },
                Text =
                {
                    Text = " ",
                    FontSize = 1,
                    Align = TextAnchor.MiddleCenter,
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }, parent);
        }

        private void AddOverlayButton(CuiElementContainer container, string parent, string command, string anchorMin, string anchorMax)
        {
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0.01",
                    Command = command
                },
                Text =
                {
                    Text = " ",
                    FontSize = 1,
                    Align = TextAnchor.MiddleCenter,
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            }, parent);
        }

        private void AddSteamAvatar(CuiElementContainer container, string parent, ulong steamId)
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        SteamId = steamId.ToString(),
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.08 0.08",
                        AnchorMax = "0.92 0.92"
                    }
                }
            });
        }

        private void AddItemIcon(CuiElementContainer container, string parent, int itemId)
        {
            AddItemIcon(container, parent, itemId, "0.08 0.08", "0.92 0.92");
        }

        private void AddItemIcon(CuiElementContainer container, string parent, int itemId, string anchorMin, string anchorMax)
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiImageComponent
                    {
                        ItemId = itemId,
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    }
                }
            });
        }

        private void AddObjectiveIcon(CuiElementContainer container, string parent, ObjectiveDefinition objective)
        {
            var itemId = GetObjectiveIconItemId(objective);
            if (itemId != 0)
            {
                AddItemIcon(container, parent, itemId, "0.02 0.02", "0.98 0.98");
                return;
            }

            AddLabel(container, parent, GetObjectiveFallbackIcon(objective), 14, _config.Theme.Text, "0.02 0.02", "0.98 0.98", TextAnchor.MiddleCenter);
        }

        private void AddPrizeIcon(CuiElementContainer container, string parent, PrizeDefinition prize)
        {
            var itemId = GetPrizeIconItemId(prize);
            if (itemId != 0)
            {
                AddItemIcon(container, parent, itemId);
                return;
            }

            AddLabel(container, parent, GetPrizeCardCode(prize), 18, _config.Theme.Text, "0.05 0.10", "0.95 0.90", TextAnchor.MiddleCenter);
        }

        private string GetObjectiveFallbackIcon(ObjectiveDefinition objective)
        {
            var source = !string.IsNullOrEmpty(objective.Icon) ? objective.Icon : !string.IsNullOrEmpty(objective.Target) ? objective.Target : objective.Type;
            if (string.IsNullOrEmpty(source))
            {
                return "?";
            }

            var token = source.Split('.', '_', '-')[0].ToUpperInvariant();
            return token.Length <= 3 ? token : token.Substring(0, 3);
        }

        private QuestDefinition CreateGatherQuest(long questId, string titleEn, string titleRu, string descriptionEn, string descriptionRu, string missionEn, string missionRu, string category, string target, int targetCount, string objectiveEn, string objectiveRu, string rewardName, string rewardShortName, int rewardAmount)
        {
            return new QuestDefinition
            {
                QuestID = questId,
                QuestDisplayName = titleEn,
                QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = titleRu, ["en"] = titleEn },
                QuestDescription = descriptionEn,
                QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = descriptionRu, ["en"] = descriptionEn },
                QuestMissions = missionEn,
                QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = missionRu, ["en"] = missionEn },
                QuestType = "Gather",
                QuestCategory = category,
                QuestTabType = "Quests",
                Objectives = new List<ObjectiveDefinition>
                {
                    new ObjectiveDefinition
                    {
                        ObjectiveId = questId + "_gather",
                        Type = "Gather",
                        Target = target,
                        TargetCount = targetCount,
                        Description = objectiveEn,
                        DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = objectiveRu, ["en"] = objectiveEn }
                    }
                },
                PrizeList = new List<PrizeDefinition>
                {
                    new PrizeDefinition
                    {
                        PrizeName = rewardName,
                        PrizeType = "Item",
                        ItemShortName = rewardShortName,
                        ItemAmount = rewardAmount
                    }
                },
                IsRepeatable = true,
                Cooldown = 60
            };
        }

        private QuestDefinition CreateDeployQuest(long questId, long questlineId, int order, long requiredQuestId, string titleEn, string titleRu, string descriptionEn, string descriptionRu, string missionEn, string missionRu, string target, int targetCount, string objectiveEn, string objectiveRu, string icon, string rewardName, string rewardShortName, int rewardAmount)
        {
            return new QuestDefinition
            {
                QuestID = questId,
                QuestDisplayName = titleEn,
                QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = titleRu, ["en"] = titleEn },
                QuestDescription = descriptionEn,
                QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = descriptionRu, ["en"] = descriptionEn },
                QuestMissions = missionEn,
                QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = missionRu, ["en"] = missionEn },
                QuestType = "Deploy",
                QuestCategory = "Base Builder",
                QuestTabType = "Quests",
                Icon = icon,
                QuestlineId = questlineId,
                QuestlineOrder = order,
                RequiredQuestIds = requiredQuestId > 0 ? new List<long> { requiredQuestId } : new List<long>(),
                Objectives = new List<ObjectiveDefinition>
                {
                    new ObjectiveDefinition
                    {
                        ObjectiveId = questId + "_deploy",
                        Type = "Deploy",
                        Target = target,
                        TargetCount = targetCount,
                        Description = objectiveEn,
                        DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = objectiveRu, ["en"] = objectiveEn },
                        MatchMode = "contains",
                        Icon = icon
                    }
                },
                PrizeList = new List<PrizeDefinition>
                {
                    new PrizeDefinition
                    {
                        PrizeName = rewardName,
                        PrizeType = "Item",
                        ItemShortName = rewardShortName,
                        ItemAmount = rewardAmount
                    }
                },
                IsRepeatable = true,
                Cooldown = 60
            };
        }

        private QuestCatalog BuildDefaultQuestCatalog()
        {
            return new QuestCatalog
            {
                Quests = new List<QuestDefinition>
                {
                    new QuestDefinition
                    {
                        QuestID = 1001,
                        QuestDisplayName = "Stone Collector",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сборщик камня", ["en"] = "Stone Collector" },
                        QuestDescription = "Gather stone for your first tools.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Добудьте камень для первых инструментов.", ["en"] = "Gather stone for your first tools." },
                        QuestMissions = "Gather 3000 stones.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Соберите 3000 камня.", ["en"] = "Gather 3000 stones." },
                        QuestType = "Gather",
                        QuestCategory = "Starter",
                        QuestTabType = "Quests",
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "1001_gather_stones",
                                Type = "Gather",
                                Target = "stones",
                                TargetCount = 3000,
                                Description = "Gather stones",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Соберите камень", ["en"] = "Gather stones" },
                                MatchMode = "shortname"
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Stone Hatchet", PrizeType = "Item", ItemShortName = "stonehatchet", ItemAmount = 1 },
                            new PrizeDefinition { PrizeName = "Wood", PrizeType = "Item", ItemShortName = "wood", ItemAmount = 1000 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60,
                        AllowManualStart = true,
                        AllowTrack = true,
                        AllowCancel = true,
                        AllowClaim = true
                    },
                    new QuestDefinition
                    {
                        QuestID = 1002,
                        QuestDisplayName = "Barrel Hunter",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Охотник за бочками", ["en"] = "Barrel Hunter" },
                        QuestDescription = "Destroy roadside barrels.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Уничтожьте дорожные бочки.", ["en"] = "Destroy roadside barrels." },
                        QuestMissions = "Destroy 10 loot barrels.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Уничтожьте 10 лутовых бочек.", ["en"] = "Destroy 10 loot barrels." },
                        QuestType = "EntityKill",
                        QuestCategory = "Misc. Quests",
                        QuestTabType = "Quests",
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "1002_barrels",
                                Type = "EntityKill",
                                Target = "loot-barrel",
                                TargetCount = 10,
                                Description = "Destroy roadside barrels",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Ломайте дорожные бочки", ["en"] = "Destroy roadside barrels" },
                                MatchMode = "prefab"
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Scrap", PrizeType = "Item", ItemShortName = "scrap", ItemAmount = 75 },
                            new PrizeDefinition { PrizeName = "Wood", PrizeType = "Item", ItemShortName = "wood", ItemAmount = 1000 },
                            new PrizeDefinition { PrizeName = "Stone", PrizeType = "Item", ItemShortName = "stones", ItemAmount = 1000 },
                            new PrizeDefinition { PrizeName = "HQM", PrizeType = "Item", ItemShortName = "metal.refined", ItemAmount = 100 },
                            new PrizeDefinition { PrizeName = "Stone Hatchet", PrizeType = "Item", ItemShortName = "stonehatchet", ItemAmount = 1 },
                            new PrizeDefinition { PrizeName = "RP", PrizeType = "RP", ItemAmount = 50 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    },
                    new QuestDefinition
                    {
                        QuestID = 3101,
                        QuestDisplayName = "Frontier Step 1",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Путь: этап 1", ["en"] = "Frontier Step 1" },
                        QuestDescription = "Gather wood to begin your journey.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Добудьте дерево, чтобы начать путь.", ["en"] = "Gather wood to begin your journey." },
                        QuestMissions = "Gather 2500 wood.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Соберите 2500 дерева.", ["en"] = "Gather 2500 wood." },
                        QuestType = "Gather",
                        QuestCategory = "Questline",
                        QuestTabType = "Quests",
                        QuestlineId = 3001,
                        QuestlineOrder = 1,
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "3101_wood",
                                Type = "Gather",
                                Target = "wood",
                                TargetCount = 2500,
                                Description = "Gather wood",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Соберите дерево", ["en"] = "Gather wood" }
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Stone Pickaxe", PrizeType = "Item", ItemShortName = "stone.pickaxe", ItemAmount = 1 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    },
                    new QuestDefinition
                    {
                        QuestID = 3102,
                        QuestDisplayName = "Frontier Step 2",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Путь: этап 2", ["en"] = "Frontier Step 2" },
                        QuestDescription = "Craft the first furnace parts.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Скрафтите первые материалы для печи.", ["en"] = "Craft the first furnace parts." },
                        QuestMissions = "Craft 2 furnaces.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Скрафтите 2 печи.", ["en"] = "Craft 2 furnaces." },
                        QuestType = "Craft",
                        QuestCategory = "Questline",
                        QuestTabType = "Quests",
                        QuestlineId = 3001,
                        QuestlineOrder = 2,
                        RequiredQuestIds = new List<long> { 3101 },
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "3102_furnace",
                                Type = "Craft",
                                Target = "furnace",
                                TargetCount = 2,
                                Description = "Craft furnaces",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Скрафтите печи", ["en"] = "Craft furnaces" }
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Metal Fragments", PrizeType = "Item", ItemShortName = "metal.fragments", ItemAmount = 500 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    },
                    new QuestDefinition
                    {
                        QuestID = 3103,
                        QuestDisplayName = "Frontier Step 3",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Путь: этап 3", ["en"] = "Frontier Step 3" },
                        QuestDescription = "Submit metal fragments to the camp engineer.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сдайте фрагменты металла инженеру лагеря.", ["en"] = "Submit metal fragments to the camp engineer." },
                        QuestMissions = "Submit 500 metal fragments.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сдайте 500 фрагментов металла.", ["en"] = "Submit 500 metal fragments." },
                        QuestType = "Delivery",
                        QuestCategory = "Questline",
                        QuestTabType = "Quests",
                        QuestlineId = 3001,
                        QuestlineOrder = 3,
                        RequiredQuestIds = new List<long> { 3102 },
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "3103_submit_metal",
                                Type = "Delivery",
                                Target = "metal.fragments",
                                TargetCount = 500,
                                Description = "Submit metal fragments",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сдайте фрагменты металла", ["en"] = "Submit metal fragments" },
                                SubmissionRequired = true
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Revolver", PrizeType = "Item", ItemShortName = "pistol.revolver", ItemAmount = 1 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    },
                    new QuestDefinition
                    {
                        QuestID = 1003,
                        QuestDisplayName = "Supply Delivery",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Поставка припасов", ["en"] = "Supply Delivery" },
                        QuestDescription = "Submit cloth to the quartermaster.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сдайте ткань квартирмейстеру.", ["en"] = "Submit cloth to the quartermaster." },
                        QuestMissions = "Submit 200 cloth.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сдайте 200 ткани.", ["en"] = "Submit 200 cloth." },
                        QuestType = "Delivery",
                        QuestCategory = "Starter",
                        QuestTabType = "Quests",
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "1003_submit_cloth",
                                Type = "Delivery",
                                Target = "cloth",
                                TargetCount = 200,
                                Description = "Submit cloth",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сдайте ткань", ["en"] = "Submit cloth" },
                                MatchMode = "shortname",
                                SubmissionRequired = true
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Medical Syringe", PrizeType = "Item", ItemShortName = "syringe.medical", ItemAmount = 5 },
                            new PrizeDefinition { PrizeName = "Blueprint Fragments", PrizeType = "BlueprintFragments", ItemAmount = 30 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    },
                    new QuestDefinition
                    {
                        QuestID = 1004,
                        QuestDisplayName = "Wood Delivery",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Поставка дерева", ["en"] = "Wood Delivery" },
                        QuestDescription = "Submit wood to the quartermaster.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сдайте дерево квартирмейстеру.", ["en"] = "Submit wood to the quartermaster." },
                        QuestMissions = "Submit 1000 wood.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сдайте 1000 дерева.", ["en"] = "Submit 1000 wood." },
                        QuestType = "Delivery",
                        QuestCategory = "Starter",
                        QuestTabType = "Quests",
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "1004_submit_wood",
                                Type = "Delivery",
                                Target = "wood",
                                TargetCount = 1000,
                                Description = "Submit wood",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Сдайте дерево", ["en"] = "Submit wood" },
                                SubmissionRequired = true
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Metal Fragments", PrizeType = "Item", ItemShortName = "metal.fragments", ItemAmount = 500 },
                            new PrizeDefinition { PrizeName = "XP", PrizeType = "XP", ItemAmount = 4500 },
                            new PrizeDefinition { PrizeName = "RP", PrizeType = "RP", ItemAmount = 9000 }
                        },
                        IsRepeatable = true,
                        Cooldown = 30
                    },
                    new QuestDefinition
                    {
                        QuestID = 1005,
                        QuestDisplayName = "Tunnel Dweller Hunt",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Охота на Tunnel Dweller", ["en"] = "Tunnel Dweller Hunt" },
                        QuestDescription = "Eliminate tunnel dwellers in the underground tunnels.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Уничтожьте Tunnel Dweller в подземных туннелях.", ["en"] = "Eliminate tunnel dwellers in the underground tunnels." },
                        QuestMissions = "Kill 3 tunnel dwellers.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Убейте 3 Tunnel Dweller.", ["en"] = "Kill 3 tunnel dwellers." },
                        QuestType = "EntityKill",
                        QuestCategory = "Misc. Quests",
                        QuestTabType = "Quests",
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "1005_tunneldweller",
                                Type = "EntityKill",
                                Target = "tunneldweller",
                                MatchAliases = new List<string> { "tunnel dweller", "tunneldweller", "dweller" },
                                TargetCount = 3,
                                Description = "Kill tunnel dwellers",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Убейте Tunnel Dweller", ["en"] = "Kill tunnel dwellers" },
                                MatchMode = "contains"
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "MP5A4", PrizeType = "Item", ItemShortName = "smg.mp5", ItemAmount = 1 },
                            new PrizeDefinition { PrizeName = "XP", PrizeType = "XP", ItemAmount = 7000 },
                            new PrizeDefinition { PrizeName = "RP", PrizeType = "RP", ItemAmount = 12000 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    },
                    new QuestDefinition
                    {
                        QuestID = 1006,
                        QuestDisplayName = "Scientist Sweep",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Зачистка ученых", ["en"] = "Scientist Sweep" },
                        QuestDescription = "Eliminate roaming scientists.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Уничтожьте обычных ученых.", ["en"] = "Eliminate roaming scientists." },
                        QuestMissions = "Kill 3 scientists.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Убейте 3 ученых.", ["en"] = "Kill 3 scientists." },
                        QuestType = "EntityKill",
                        QuestCategory = "Misc. Quests",
                        QuestTabType = "Quests",
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "1006_scientist",
                                Type = "EntityKill",
                                Target = "scientistnpc",
                                MatchAliases = new List<string> { "scientist", "npcscientist" },
                                TargetCount = 3,
                                Description = "Kill scientists",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Убейте ученых", ["en"] = "Kill scientists" },
                                MatchMode = "contains"
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Assault Rifle", PrizeType = "Item", ItemShortName = "rifle.ak", ItemAmount = 1 },
                            new PrizeDefinition { PrizeName = "XP", PrizeType = "XP", ItemAmount = 9000 },
                            new PrizeDefinition { PrizeName = "RP", PrizeType = "RP", ItemAmount = 14000 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    },
                    new QuestDefinition
                    {
                        QuestID = 1007,
                        QuestDisplayName = "Heavy Scientist Hunt",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Охота на Heavy Scientist", ["en"] = "Heavy Scientist Hunt" },
                        QuestDescription = "Eliminate heavy scientists.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Уничтожьте Heavy Scientist.", ["en"] = "Eliminate heavy scientists." },
                        QuestMissions = "Kill 3 heavy scientists.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Убейте 3 Heavy Scientist.", ["en"] = "Kill 3 heavy scientists." },
                        QuestType = "EntityKill",
                        QuestCategory = "Misc. Quests",
                        QuestTabType = "Quests",
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "1007_heavy_scientist",
                                Type = "EntityKill",
                                Target = "scientistnpc_heavy",
                                MatchAliases = new List<string> { "heavyscientist", "heavy scientist", "scientistnpcheavy" },
                                TargetCount = 3,
                                Description = "Kill heavy scientists",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Убейте Heavy Scientist", ["en"] = "Kill heavy scientists" },
                                MatchMode = "contains"
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "M249", PrizeType = "Item", ItemShortName = "lmg.m249", ItemAmount = 1 },
                            new PrizeDefinition { PrizeName = "XP", PrizeType = "XP", ItemAmount = 12000 },
                            new PrizeDefinition { PrizeName = "RP", PrizeType = "RP", ItemAmount = 18000 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    },
                    CreateGatherQuest(1101, "Wood Collector I", "Сбор дерева I", "Gather a starter stockpile of wood.", "Соберите стартовый запас дерева.", "Gather 5000 wood.", "Соберите 5000 дерева.", "Wood", "wood", 5000, "Gather wood", "Соберите дерево", "Scrap", "scrap", 75),
                    CreateGatherQuest(1102, "Wood Collector II", "Сбор дерева II", "Gather more wood for expansion.", "Соберите больше дерева для расширения базы.", "Gather 10000 wood.", "Соберите 10000 дерева.", "Wood", "wood", 10000, "Gather wood", "Соберите дерево", "Scrap", "scrap", 150),
                    CreateGatherQuest(1103, "Wood Collector III", "Сбор дерева III", "Stockpile enough wood for a serious build.", "Подготовьте серьезный запас дерева для стройки.", "Gather 20000 wood.", "Соберите 20000 дерева.", "Wood", "wood", 20000, "Gather wood", "Соберите дерево", "Scrap", "scrap", 300),
                    CreateGatherQuest(1104, "Wood Collector IV", "Сбор дерева IV", "Keep the furnaces and walls fed with wood.", "Поддерживайте стройку и печи большим запасом дерева.", "Gather 50000 wood.", "Соберите 50000 дерева.", "Wood", "wood", 50000, "Gather wood", "Соберите дерево", "Scrap", "scrap", 700),
                    CreateGatherQuest(1105, "Wood Collector V", "Сбор дерева V", "Finish a massive lumber run.", "Завершите огромный лесной забег.", "Gather 100000 wood.", "Соберите 100000 дерева.", "Wood", "wood", 100000, "Gather wood", "Соберите дерево", "Scrap", "scrap", 1500),
                    CreateGatherQuest(1111, "Stone Collector I", "Сбор камня I", "Bring in stone for your base shell.", "Добудьте камень для основы базы.", "Gather 5000 stones.", "Соберите 5000 камня.", "Stone", "stones", 5000, "Gather stone", "Соберите камень", "Metal Fragments", "metal.fragments", 250),
                    CreateGatherQuest(1112, "Stone Collector II", "Сбор камня II", "Expand your quarry run.", "Увеличьте добычу камня.", "Gather 10000 stones.", "Соберите 10000 камня.", "Stone", "stones", 10000, "Gather stone", "Соберите камень", "Metal Fragments", "metal.fragments", 500),
                    CreateGatherQuest(1113, "Stone Collector III", "Сбор камня III", "Keep the stone stockpile growing.", "Продолжайте наращивать запасы камня.", "Gather 20000 stones.", "Соберите 20000 камня.", "Stone", "stones", 20000, "Gather stone", "Соберите камень", "Metal Fragments", "metal.fragments", 1000),
                    CreateGatherQuest(1114, "Stone Collector IV", "Сбор камня IV", "Build a serious reserve of stone.", "Соберите серьезный запас камня.", "Gather 50000 stones.", "Соберите 50000 камня.", "Stone", "stones", 50000, "Gather stone", "Соберите камень", "Metal Fragments", "metal.fragments", 2500),
                    CreateGatherQuest(1115, "Stone Collector V", "Сбор камня V", "Finish a massive stone haul.", "Завершите огромную добычу камня.", "Gather 100000 stones.", "Соберите 100000 камня.", "Stone", "stones", 100000, "Gather stone", "Соберите камень", "Metal Fragments", "metal.fragments", 5000),
                    CreateGatherQuest(1121, "Metal Collector I", "Сбор фрагментов I", "Bring back refined metal fragments for production.", "Добудьте фрагменты металла для производства.", "Gather 5000 metal fragments.", "Соберите 5000 фрагментов металла.", "Metal", "metal.fragments", 5000, "Gather metal fragments", "Соберите фрагменты металла", "Scrap", "scrap", 100),
                    CreateGatherQuest(1122, "Metal Collector II", "Сбор фрагментов II", "Keep the metal reserve moving upward.", "Продолжайте пополнять запас фрагментов металла.", "Gather 10000 metal fragments.", "Соберите 10000 фрагментов металла.", "Metal", "metal.fragments", 10000, "Gather metal fragments", "Соберите фрагменты металла", "Scrap", "scrap", 200),
                    CreateGatherQuest(1123, "Metal Collector III", "Сбор фрагментов III", "Prepare a serious smelting reserve.", "Подготовьте серьезный запас металла.", "Gather 20000 metal fragments.", "Соберите 20000 фрагментов металла.", "Metal", "metal.fragments", 20000, "Gather metal fragments", "Соберите фрагменты металла", "Scrap", "scrap", 400),
                    CreateGatherQuest(1124, "Metal Collector IV", "Сбор фрагментов IV", "Push your metal reserve to industrial scale.", "Разгоните запас металла до промышленного уровня.", "Gather 50000 metal fragments.", "Соберите 50000 фрагментов металла.", "Metal", "metal.fragments", 50000, "Gather metal fragments", "Соберите фрагменты металла", "Scrap", "scrap", 800),
                    CreateGatherQuest(1125, "Metal Collector V", "Сбор фрагментов V", "Finish the ultimate metal stockpile.", "Завершите максимальный запас фрагментов металла.", "Gather 100000 metal fragments.", "Соберите 100000 фрагментов металла.", "Metal", "metal.fragments", 100000, "Gather metal fragments", "Соберите фрагменты металла", "Scrap", "scrap", 1600),
                    CreateGatherQuest(1131, "Scrap Collector I", "Сбор скрапа I", "Bring home a first batch of scrap.", "Принесите первую партию скрапа.", "Gather 5000 scrap.", "Соберите 5000 скрапа.", "Scrap", "scrap", 5000, "Gather scrap", "Соберите скрап", "Low Grade Fuel", "lowgradefuel", 50),
                    CreateGatherQuest(1132, "Scrap Collector II", "Сбор скрапа II", "Expand your scrap route.", "Расширьте маршрут по сбору скрапа.", "Gather 10000 scrap.", "Соберите 10000 скрапа.", "Scrap", "scrap", 10000, "Gather scrap", "Соберите скрап", "Low Grade Fuel", "lowgradefuel", 100),
                    CreateGatherQuest(1133, "Scrap Collector III", "Сбор скрапа III", "Bring in a serious recycler run.", "Сделайте серьезный заход на переработку.", "Gather 20000 scrap.", "Соберите 20000 скрапа.", "Scrap", "scrap", 20000, "Gather scrap", "Соберите скрап", "Low Grade Fuel", "lowgradefuel", 200),
                    CreateGatherQuest(1134, "Scrap Collector IV", "Сбор скрапа IV", "Keep the recycler busy and the scrap flowing.", "Поддерживайте стабильный приток скрапа.", "Gather 50000 scrap.", "Соберите 50000 скрапа.", "Scrap", "scrap", 50000, "Gather scrap", "Соберите скрап", "Low Grade Fuel", "lowgradefuel", 400),
                    CreateGatherQuest(1135, "Scrap Collector V", "Сбор скрапа V", "Complete a giant scrap haul.", "Завершите гигантский сбор скрапа.", "Gather 100000 scrap.", "Соберите 100000 скрапа.", "Scrap", "scrap", 100000, "Gather scrap", "Соберите скрап", "Low Grade Fuel", "lowgradefuel", 800),
                    CreateDeployQuest(3201, 3002, 1, 0, "Base Builder: Foundations", "Строитель базы: фундаменты", "Lay down the first foundations of your base.", "Заложите первые фундаменты своей базы.", "Place 10 foundations.", "Поставьте 10 фундаментов.", "foundation", 10, "Place foundations", "Поставьте фундаменты", "building.planner", "Wood", "wood", 3000),
                    CreateDeployQuest(3202, 3002, 2, 3201, "Base Builder: Tool Cupboard", "Строитель базы: шкаф", "Secure the base with a tool cupboard.", "Защитите базу шкафом с инструментами.", "Place a tool cupboard.", "Поставьте шкаф.", "cupboard.tool", 1, "Place a tool cupboard", "Поставьте шкаф", "cupboard.tool", "Stone", "stones", 3000),
                    CreateDeployQuest(3203, 3002, 3, 3202, "Base Builder: Workbench 1", "Строитель базы: верстак 1", "Set up the first workbench for crafting.", "Установите первый верстак для крафта.", "Place a level 1 workbench.", "Поставьте верстак 1 уровня.", "workbench1", 1, "Place a level 1 workbench", "Поставьте верстак 1 уровня", "workbench1", "Metal Fragments", "metal.fragments", 500),
                    CreateDeployQuest(3204, 3002, 4, 3203, "Base Builder: Workbench 2", "Строитель базы: верстак 2", "Upgrade your production line with a better bench.", "Улучшите производство новым верстаком.", "Place a level 2 workbench.", "Поставьте верстак 2 уровня.", "workbench2", 1, "Place a level 2 workbench", "Поставьте верстак 2 уровня", "workbench2", "Metal Fragments", "metal.fragments", 1000),
                    CreateDeployQuest(3205, 3002, 5, 3204, "Base Builder: Workbench 3", "Строитель базы: верстак 3", "Finish the crafting chain with the top bench.", "Завершите цепочку крафта топовым верстаком.", "Place a level 3 workbench.", "Поставьте верстак 3 уровня.", "workbench3", 1, "Place a level 3 workbench", "Поставьте верстак 3 уровня", "workbench3", "Scrap", "scrap", 300),
                    CreateDeployQuest(3206, 3002, 6, 3205, "Base Builder: Wooden Door", "Строитель базы: деревянная дверь", "Close off the entrance with a wooden door.", "Закройте вход деревянной дверью.", "Place a wooden door.", "Поставьте деревянную дверь.", "door.hinged.wood", 1, "Place a wooden door", "Поставьте деревянную дверь", "door.hinged.wood", "Wood", "wood", 1000),
                    CreateDeployQuest(3207, 3002, 7, 3206, "Base Builder: Sheet Door", "Строитель базы: железная дверь", "Upgrade the entrance with a sheet metal door.", "Укрепите вход железной дверью.", "Place a sheet metal door.", "Поставьте железную дверь.", "door.hinged.metal", 1, "Place a sheet metal door", "Поставьте железную дверь", "door.hinged.metal", "Metal Fragments", "metal.fragments", 750),
                    CreateDeployQuest(3208, 3002, 8, 3207, "Base Builder: Armored Door", "Строитель базы: МВК дверь", "Finish your entrance with an armored door.", "Завершите укрепление входа МВК дверью.", "Place an armored door.", "Поставьте МВК дверь.", "door.hinged.toptier", 1, "Place an armored door", "Поставьте МВК дверь", "door.hinged.toptier", "HQM", "metal.refined", 25)
                }
            };
        }

        private DailyCatalog BuildDefaultDailyCatalog()
        {
            return new DailyCatalog
            {
                DailyQuests = new List<QuestDefinition>
                {
                    new QuestDefinition
                    {
                        QuestID = 2001,
                        QuestDisplayName = "Daily Lumberjack",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Ежедневный лесоруб", ["en"] = "Daily Lumberjack" },
                        QuestDescription = "Gather wood for the camp.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Добудьте дерево для лагеря.", ["en"] = "Gather wood for the camp." },
                        QuestMissions = "Gather 5000 wood.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Соберите 5000 дерева.", ["en"] = "Gather 5000 wood." },
                        QuestType = "Gather",
                        QuestCategory = "Daily",
                        QuestTabType = "Daily",
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "2001_wood",
                                Type = "Gather",
                                Target = "wood",
                                TargetCount = 5000,
                                Description = "Gather wood",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Соберите дерево", ["en"] = "Gather wood" }
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Scrap", PrizeType = "Item", ItemShortName = "scrap", ItemAmount = 125 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    },
                    new QuestDefinition
                    {
                        QuestID = 2002,
                        QuestDisplayName = "Daily Crafter",
                        QuestDisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Ежедневный крафтер", ["en"] = "Daily Crafter" },
                        QuestDescription = "Craft resources for the workshop.",
                        QuestDescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Скрафтите ресурсы для мастерской.", ["en"] = "Craft resources for the workshop." },
                        QuestMissions = "Craft 10 bandages.",
                        QuestMissionsMultiLanguage = new Dictionary<string, string> { ["ru"] = "Скрафтите 10 бинтов.", ["en"] = "Craft 10 bandages." },
                        QuestType = "Craft",
                        QuestCategory = "Daily",
                        QuestTabType = "Daily",
                        Objectives = new List<ObjectiveDefinition>
                        {
                            new ObjectiveDefinition
                            {
                                ObjectiveId = "2002_bandage",
                                Type = "Craft",
                                Target = "bandage",
                                TargetCount = 10,
                                Description = "Craft bandages",
                                DescriptionMultiLanguage = new Dictionary<string, string> { ["ru"] = "Создайте бинты", ["en"] = "Craft bandages" }
                            }
                        },
                        PrizeList = new List<PrizeDefinition>
                        {
                            new PrizeDefinition { PrizeName = "Low Grade Fuel", PrizeType = "Item", ItemShortName = "lowgradefuel", ItemAmount = 50 }
                        },
                        IsRepeatable = true,
                        Cooldown = 60
                    }
                }
            };
        }

        private QuestlineCatalog BuildDefaultQuestlineCatalog()
        {
            return new QuestlineCatalog
            {
                Questlines = new List<QuestlineDefinition>
                {
                    new QuestlineDefinition
                    {
                        QuestlineId = 3001,
                        DisplayName = "Frontier Path",
                        DisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Путь первопроходца", ["en"] = "Frontier Path" },
                        StageQuestIds = new List<long> { 3101, 3102, 3103 }
                    },
                    new QuestlineDefinition
                    {
                        QuestlineId = 3002,
                        DisplayName = "Base Builder",
                        DisplayNameMultiLanguage = new Dictionary<string, string> { ["ru"] = "Строительство базы", ["en"] = "Base Builder" },
                        StageQuestIds = new List<long> { 3201, 3202, 3203, 3204, 3205, 3206, 3207, 3208 }
                    }
                }
            };
        }

        private object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity as BasePlayer;
            if (player == null || item == null)
            {
                return null;
            }

            ProcessProgress(player, "Gather", item.info.shortname, item.amount, item.skin, item, null);
            return null;
        }

        private object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || item == null)
            {
                return null;
            }

            ProcessProgress(player, "Gather", item.info.shortname, item.amount, item.skin, item, null);
            return null;
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (player == null || item == null)
            {
                return;
            }

            ProcessProgress(player, "Gather", item.info.shortname, item.amount, item.skin, item, null);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null)
            {
                return;
            }

            ProcessProgress(player, "Loot", entity.ShortPrefabName, 1, 0, null, entity.ShortPrefabName);
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            if (crafter == null || crafter.owner == null || item == null)
            {
                return;
            }

            ProcessProgress(crafter.owner, "Craft", item.info.shortname, item.amount, item.skin, item, null);
        }

        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (player == null || block == null)
            {
                return;
            }

            ProcessProgress(player, "Grade", block.ShortPrefabName, 1, 0, null, block.ShortPrefabName);
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            if (planner == null || planner.GetOwnerPlayer() == null || gameObject == null)
            {
                return;
            }

            var entity = gameObject.ToBaseEntity();
            if (entity == null)
            {
                return;
            }

            ProcessProgress(planner.GetOwnerPlayer(), "Deploy", entity.ShortPrefabName, 1, 0, null, entity.ShortPrefabName);
        }

        private void OnCardSwipe(CardReader cardReader, BasePlayer player, string cardName)
        {
            if (player == null)
            {
                return;
            }

            ProcessProgress(player, "Swipe", cardName ?? string.Empty, 1, 0, null, cardName);
        }

        private void OnItemRecycle(Recycler recycler, Item item, BasePlayer player)
        {
            if (player == null || item == null)
            {
                return;
            }

            ProcessProgress(player, "RecycleItem", item.info.shortname, item.amount, item.skin, item, null);
        }

        private void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
        {
            if (player == null || item == null)
            {
                return;
            }

            ProcessProgress(player, "Growseedlings", item.info.shortname, item.amount, item.skin, item, null);
        }

        private void OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
        {
            if (player == null || item == null)
            {
                return;
            }

            ProcessProgress(player, "Fishing", item.info.shortname, 1, item.skin, item, null);
        }

        private void OnCrateHack(HackableLockedCrate crate)
        {
            if (crate == null || crate.OwnerID == 0)
            {
                return;
            }

            var player = BasePlayer.FindByID(crate.OwnerID);
            if (player == null)
            {
                return;
            }

            ProcessProgress(player, "HackCrate", crate.ShortPrefabName, 1, 0, null, crate.ShortPrefabName);
        }

        private void OnPlayerDeath(BasePlayer victim, HitInfo info)
        {
            if (victim == null || info == null)
            {
                return;
            }

            var killer = info.InitiatorPlayer;
            if (killer == null || killer == victim)
            {
                return;
            }

            if (IsExcludedPvpKill(killer, victim))
            {
                return;
            }

            ProcessProgress(killer, "EntityKill", "player", 1, 0, null, victim.displayName);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
            {
                return;
            }

            var player = info.InitiatorPlayer;
            if (player == null)
            {
                return;
            }

            if (entity is BasePlayer)
            {
                return;
            }

            var entityTarget = entity.ShortPrefabName ?? string.Empty;
            var entityDescriptor = string.Format("{0}|{1}|{2}", entityTarget, entity.name ?? string.Empty, entity.GetType().Name ?? string.Empty);
            ProcessProgress(player, "EntityKill", entityTarget, 1, 0, null, entityDescriptor);
        }

        private void OnTechTreeNodeUnlocked(BasePlayer player, object node, ItemDefinition itemDefinition)
        {
            if (player == null || itemDefinition == null)
            {
                return;
            }

            ProcessProgress(player, "Research", itemDefinition.shortname, 1, 0, null, itemDefinition.shortname);
        }

        private void OnItemResearched(BasePlayer player, Item item, int amount)
        {
            if (player == null || item == null)
            {
                return;
            }

            ProcessProgress(player, "Research", item.info.shortname, 1, item.skin, item, null);
        }

        private void OnCustomVendingSetupGiveSoldItem(BasePlayer player, Item item)
        {
            if (player == null || item == null)
            {
                return;
            }

            ProcessProgress(player, "PurchaseFromNpc", item.info.shortname, item.amount, item.skin, item, null);
        }

        private void OnNpcGiveSoldItem(BasePlayer player, Item item)
        {
            if (player == null || item == null)
            {
                return;
            }

            ProcessProgress(player, "PurchaseFromNpc", item.info.shortname, item.amount, item.skin, item, null);
        }

        private void OnRaidableBaseCompleted(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "RaidableBases", "RaidableBases", 1, 0, null, "RaidableBases");
            }
        }

        private void OnBossKilled(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "BossMonster", "BossMonster", 1, 0, null, "BossMonster");
            }
        }

        private void OnDroneKilled(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "IQDronePatrol", "IQDronePatrol", 1, 0, null, "IQDronePatrol");
            }
        }

        private void OnLootedDefenderSupply(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "IQDefenderSupply", "IQDefenderSupply", 1, 0, null, "IQDefenderSupply");
            }
        }

        private void OnHarborEventWinner(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "HarborEvent", "HarborEvent", 1, 0, null, "HarborEvent");
            }
        }

        private void OnSatDishEventWinner(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "SatelliteDishEvent", "SatelliteDishEvent", 1, 0, null, "SatelliteDishEvent");
            }
        }

        private void OnSputnikEventWin(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "Sputnik", "Sputnik", 1, 0, null, "Sputnik");
            }
        }

        private void OnGasStationEventWinner(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "GasStationEvent", "GasStationEvent", 1, 0, null, "GasStationEvent");
            }
        }

        private void OnTriangulationWinner(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "Triangulation", "Triangulation", 1, 0, null, "Triangulation");
            }
        }

        private void OnFerryTerminalEventWinner(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "FerryTerminalEvent", "FerryTerminalEvent", 1, 0, null, "FerryTerminalEvent");
            }
        }

        private void OnConvoyEventWin(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "Convoy", "Convoy", 1, 0, null, "Convoy");
            }
        }

        private void OnCaravanEventWin(BasePlayer player)
        {
            if (player != null)
            {
                ProcessProgress(player, "Caravan", "Caravan", 1, 0, null, "Caravan");
            }
        }

        private bool IsExcludedPvpKill(BasePlayer killer, BasePlayer victim)
        {
            if (killer == null || victim == null)
            {
                return false;
            }

            if (IsPluginTrue(Friends, "AreFriends", killer.userID, victim.userID))
            {
                return true;
            }

            if (IsPluginTrue(Clans, "IsClanMember", killer.userID, victim.userID))
            {
                return true;
            }

            if (IsPluginTrue(EventHelper, "IsEventPlayer", killer) || IsPluginTrue(EventHelper, "IsEventPlayer", victim))
            {
                return true;
            }

            if (IsPluginTrue(Battles, "IsPlayerOnBattle", killer.userID) || IsPluginTrue(Battles, "IsPlayerOnBattle", victim.userID))
            {
                return true;
            }

            if (IsPluginTrue(Duel, "IsPlayerOnActiveDuel", killer.userID) || IsPluginTrue(Duelist, "InEvent", killer) || IsPluginTrue(ArenaTournament, "IsOnTournament", killer.userID))
            {
                return true;
            }

            return false;
        }

        private bool IsPluginTrue(Plugin plugin, string hook, params object[] args)
        {
            if (plugin == null)
            {
                return false;
            }

            try
            {
                var result = plugin.Call(hook, args);
                if (result == null)
                {
                    return false;
                }

                return Convert.ToBoolean(result);
            }
            catch
            {
                return false;
            }
        }
    }
}
