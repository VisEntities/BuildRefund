/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Build Refund", "VisEntities", "1.0.0")]
    [Description("Demolishing structures returns some of the original cost.")]
    public class BuildRefund : RustPlugin
    {
        #region Fields

        private static BuildRefund _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Refund Percentage")]
            public int RefundPercentage { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                RefundPercentage = 100
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private object OnStructureDemolish(BuildingBlock block, BasePlayer player)
        {
            if (block == null || player == null)
                return null;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.USE))
                return null;

            Construction construction = block.blockDefinition;
            if (construction == null)
                return null;

            ConstructionGrade defaultGrade = construction.defaultGrade;
            ConstructionGrade currentGrade = block.currentGrade;
            if (defaultGrade == null || currentGrade == null)
                return null;

            List<ItemAmount> baseCosts = defaultGrade.CostToBuild();
            List<ItemAmount> currentCosts = currentGrade.CostToBuild();

            Dictionary<int, float> upgradeCosts = new Dictionary<int, float>();

            foreach (ItemAmount cost in currentCosts)
            {
                upgradeCosts[cost.itemDef.itemid] = cost.amount;
            }

            foreach (ItemAmount cost in baseCosts)
            {
                if (upgradeCosts.ContainsKey(cost.itemDef.itemid))
                    upgradeCosts[cost.itemDef.itemid] -= cost.amount;
            }

            List<int> keys = new List<int>(upgradeCosts.Keys);
            foreach (int key in keys)
            {
                if (upgradeCosts[key] <= 0f)
                    upgradeCosts.Remove(key);
            }

            if (upgradeCosts.Count == 0)
                return null;

            List<string> refundDetails = new List<string>();

            foreach (KeyValuePair<int, float> kvp in upgradeCosts)
            {
                int refundQuantity = Mathf.RoundToInt(kvp.Value * (_config.RefundPercentage / 100f));
                if (refundQuantity > 0)
                {
                    var refundItem = ItemManager.CreateByItemID(kvp.Key, refundQuantity);
                    if (refundItem != null)
                    {
                        player.GiveItem(refundItem, BaseEntity.GiveItemReason.PickedUp);
                        var itemDef = ItemManager.FindItemDefinition(kvp.Key);
                        refundDetails.Add(string.Format("- {0} x{1}", itemDef.displayName.translated, refundQuantity));
                    }
                }
            }

            if (refundDetails.Count > 0)
            {
                string refundDetail = string.Join("\n", refundDetails);
                string blockName = block.ShortPrefabName;
                string gradeName = currentGrade.gradeBase.type.ToString();
                MessagePlayer(player, Lang.RefundSummary, blockName, gradeName, refundDetail);
            }

            return null;
        }

        #endregion Oxide Hooks

        #region Permissions

        private static class PermissionUtil
        {
            public const string USE = "buildrefund.use";
            private static readonly List<string> _permissions = new List<string>
            {
                USE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions

        #region Localization

        private class Lang
        {
            public const string RefundSummary = "RefundSummary";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.RefundSummary] = "Refund issued for demolishing {0} (Grade: {1}):\n{2}"

            }, this, "en");
        }

        private static string GetMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = _plugin.lang.GetMessage(messageKey, _plugin, player.UserIDString);

            if (args.Length > 0)
                message = string.Format(message, args);

            return message;
        }

        public static void MessagePlayer(BasePlayer player, string messageKey, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);
            _plugin.SendReply(player, message);
        }

        #endregion Localization
    }
}