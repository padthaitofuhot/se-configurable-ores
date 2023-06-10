using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using ConfigurableOres;
using static ConfigurableOres.ModInfo;
using static ConfigurableOres.Strings;

// ReSharper disable InconsistentNaming

namespace ConfigurableOres
{
    /*
     *  Based on Helpers class by Patrick
     */
    public static class Helpers
    {
        #region Configuration Defaults

        //  TODO [DEBUG] -- Set to false before publishing
        public const string CONFIG_FILE_NAME = "ConfigurableOres.xml";

        // ------------------------------
        // TODO: Set these to false on release.
        // ------------------------------
        // Whether or not to throw() up on errors
        public const bool ABEND_ON_FAULT = true;
        // Whether or not logging is enabled immediately
        public static bool LOGGING_ENABLED = true;
        // Whether or not default new config enables logging
        public static bool DEFAULT_LOGGING_ENABLED = true;
        // ------------------------------
        
        public const string COMMAND_PREFIX = "/ore";
        public const string MENU_REGEX_PREFIX = "(?i)^";
        public const string MENU_REGEX_SUFFIX = "\\s*";
        public const float DEFAULT_OREMAP_DEPTH_START = 15f;
        public const float DEFAULT_OREMAP_DEPTH_SIZE = 1f;
        public const string DEFAULT_OREMAP_TARGETCOLOR = "#616c83";
        public const float DEFAULT_OREMAP_COLORINFLUENCE = 15f;

        public const float DEFAULT_ORE_DEPTH_MIN = 2;
        public const float DEFAULT_ORE_DEPTH_MAX = 150;
        public const float DEFAULT_ORE_AUTO_DEPTH_FUZZ = 0.50f;

        public const bool DEFAULT_ORE_AUTO_DEPTH_PROGRESSIVE = true;

        // Cruise Control - Exponent for curving depths deeper as slot count decreases
        // Use a value larger than 1 to curve distribution towards minDepth.
        // Use a value smaller than 1 to curve distribution towards maxDepth.
        public const float DEFAULT_ORE_AUTO_DEPTH_CURVE_EXPONENT = 0.8f;
        public const bool DEFAULT_ORE_USE_DETECTOR_STATE = true;
        public const MyCubeSize DEFAULT_ORE_USE_DETECTOR_CUBESIZE = MyCubeSize.Large;
        public const float DEFAULT_ORE_USE_DETECTOR_FACTOR = 1f;
        public const bool DEFAULT_ORE_CUSTOM_DEPTH_STATE = false;
        public const float DEFAULT_ORE_CUSTOM_DEPTH = 0;

        public const float DEFAULT_ORE_SIZE_MIN = 1;
        public const float DEFAULT_ORE_SIZE_MAX = 24;
        public const float DEFAULT_ORE_AUTO_SIZE_FACTOR = 0.1f;
        public const float DEFAULT_ORE_AUTO_SIZE_FUZZ = 0.5f;
        public const bool DEFAULT_ORE_CUSTOM_SIZE_STATE = false;
        public const float DEFAULT_ORE_CUSTOM_SIZE = 0;

        public static readonly List<string> DEFAULT_OREMAP_NEVER_STATIC_VOXEL_MATERIALS = new List<string>()
        {
            "Stone_01",
            "Iron_02",
            "Nickel_01",
            "Cobalt_01",
            "Magnesium_01",
            "Silicon_01",
            "Silver_01",
            "Gold_01",
            "Platinum_01",
            "Uraninite_01",
            "Ice_01"
        };

        // todo: make editable
        public static readonly List<string> DEFAULT_OREMAP_STATIC_VOXEL_MATERIALS = new List<string>
        {
            "Ice",
            "CobaltCrystal",
            "DebugMaterial",
            "Stone_02",
            "Stone_03",
            "Stone_04",
            "Stone_05",
            "Iron_01",
            "Ice_02",
            "Ice_03",
            "SmallMoonRocks",
            "TritonStone",
            "TritonBlend",
            "TritonIce",
            "CrackedSoil",
            "DustyRocks",
            "DustyRocks2",
            "DustyRocks3",
            "PertamSand",
            "DebugMaterial",
            "Sand_02",
            "DesertRocks",
            "MarsRocks",
            "MarsSoil",
            "MoonSoil",
            "MoonRocks",
            "Snow",
            "Ice",
            "IceEuropa2",
            "Grass",
            "Grass bare",
            "Rocks_grass",
            "Grass_old",
            "Grass_old bare",
            "Grass_02",
            "Woods_grass",
            "Woods_grass bare",
            "Soil",
            "Stone",
            "AlienGreenGrass",
            "AlienGreenGrass bare",
            "AlienIce_03",
            "AlienRockyTerrain",
            "AlienRockyMountain",
            "AlienSand",
            "AlienRockGrass",
            "AlienRockGrass bare",
            "AlienOrangeGrass",
            "AlienOrangeGrass bare",
            "AlienIce",
            "AlienSnow",
            "AlienYellowGrass",
            "AlienYellowGrass bare",
            "AlienSoil"
        };

        // todo: make editable
        public static readonly List<string> DEFAULT_IGNORED_PLANETS = new List<string>
        {
            "tutorial",
            "test",
            "example"
        };

        // todo: make editable
        public static readonly List<string> DEFAULT_IGNORED_ORES = new List<string>
        {
            "Organic",
        };

        // todo: make editable
        public static readonly Dictionary<string, string> DEFAULT_ORE_ELEMENT_SYMBOLS = new Dictionary<string, string>
        {
            { "ur", "uranium" },
            { "fe", "iron" },
            { "co", "cobalt" },
            { "si", "silicon" },
            { "ni", "nickel" },
            { "au", "gold" },
            { "ag", "silver" },
            { "pt", "platinum" },
            { "ti", "titanium" },
            { "th", "thorium" }
        };

        // todo: make editable
        public static readonly Dictionary<string, string> DEFAULT_BOOLEAN_SYNONYMS = new Dictionary<string, string>
        {
            { "true", "true" },
            { "false", "false" },
            { "on", "true" },
            { "off", "false" },
            { "enable", "true" },
            { "disable", "false" },
            { "yes", "true" },
            { "no", "false" }
        };

        #endregion

        #region Universal Constants

        public static readonly Regex regexAlphaNum = new Regex("^[a-zA-Z0-9]*$");

        public const int SECONDS_TO_MILLISECONDS = 1000;
        public const int DAY_HOURS_TO_SECONDS = 24 * 60 * 60;
        public const int NOTIFICATION_DURATION = 15;

        // public readonly static int Seed = DateTime.Now.DayOfYear * DAY_HOURS_TO_SECONDS + (int)DateTime.Now.TimeOfDay.TotalSeconds;
        private static readonly int Seed = Now.DayOfYear * DAY_HOURS_TO_SECONDS + (int)Now.TimeOfDay.TotalSeconds;
        private static readonly Random R = new Random(Seed);

        /// <summary>
        /// Returns a random number between -1 and 1.
        /// </summary>
        public static float RandomFuzz => (float)R.NextDouble() * 2 - 1;

        public static float NegaFuzz => 1 - (float)R.NextDouble();

        private static DateTime Now => MyAPIGateway.Session.GameDateTime;

        #endregion

        #region Static Helpers

        public static string GetMinedOreByVoxelMaterialName(string voxelMaterialName)
        {
            return ConfigurableOres.Instance.AllVoxelMaterialDefinitions
                .Where(vm =>
                    string.Equals(vm.Id.SubtypeId.String, voxelMaterialName, StringComparison.OrdinalIgnoreCase))
                .Select(vm => vm).Single().MinedOre;
        }

        public static List<string> GetFilteredVoxelsToOreNames(Dictionary<string, string> voxelMaterialToOre,
            Configuration config)
        {
            return (from voxelMaterial in voxelMaterialToOre
                where !config.StaticVoxelMaterials.Contains(voxelMaterial.Key)
                select voxelMaterial.Value).ToList();
        }

        public static bool StringContains(string source, string target)
        {
            return source.IndexOf(target, StringComparison.CurrentCultureIgnoreCase) > -1;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        #region Menu Helpers

        public static string MenuRegexBuilder(string match)
        {
            var regexString = $"{MENU_REGEX_PREFIX}{Regex.Escape(match)}{MENU_REGEX_SUFFIX}";
            LogVar("MenuRegexBuilder", "regexString", regexString);
            return regexString;
        }
        
        public static bool IsMyMatch(string match, string text)
        {
            var _regex = new Regex("(?i)^" + match);
            return IsMyMatch(_regex, text);
        }

        public static bool IsMyMatch(Regex match, string text)
        {
            return match.IsMatch(text);
        }

        public static string RegexTrim(string match, string text)
        {
            return Regex.Replace(text, CHAT_REGEX_PREFIX + match, "", RegexOptions.IgnoreCase).Trim();
        }

        public static string ClarifyBoolString(string text)
        {
            LogVar("", "ClarifyBoolString(string text)", text);

            var clarified = DEFAULT_BOOLEAN_SYNONYMS
                .Aggregate(text.Trim(), (current, nym)
                    => Regex.Replace(current, "^" + nym.Key + "$", nym.Value,
                        RegexOptions.IgnoreCase));

            LogVar("", "clarified", clarified);

            return clarified;
        }

        // todo: implement short names and atomic symbols of ores
        public static string ClarifyOreString(string text)
        {
            return DEFAULT_ORE_ELEMENT_SYMBOLS
                .Aggregate(text.Trim(), (current, nym)
                    => Regex.Replace(current, "^" + nym.Key + "$", nym.Value,
                        RegexOptions.IgnoreCase));
        }

        public static string NiceList(List<string> list, bool delimited = false, string delimiter = COMMA_SEP)
        {
            var sb = new StringBuilder();
            foreach (var line in list)
            {
                switch (delimited)
                {
                    case true:
                        sb.Append(line + delimiter);
                        break;
                    case false:
                        sb.AppendLine(line);
                        break;
                }
            }

            var output = sb.ToString();
            if (delimited && output.Length > 2) output = output.Substring(0, output.Length - 2);

            return output;
        }

        #endregion

        #region Chat Methods

        /// <summary>
        ///  Receiving clientside is MyAPIGateway.Utilities.MessageReceived
        ///  Note that only triggers for "real" chat messages, not messages that you send with a mod.
        ///  MyAPIGateway.Utilities.MessageEntered
        ///  MyAPIGateway.Utilities.RegisterMessageHandler
        /// </summary>
        public static void WriteToChatGeneralError(string message)
        {
            WriteToChatFlag(message, $"{Error("general")} {message}");
        }

        public static void WriteErrorToChat(string template, string command, string more = "")
        {
            LogVar("", "template", template);
            LogVar("", "command", command);
            LogVar("", "more", more);

            var error = string.Format(template, command, more);

            WriteToChat(error);
            Log(LOG_PREFIX_SESSION, error);
        }

        public static void WriteToChatFlag(string message, string flag)
        {
            WriteToChat(string.Format(CHAT_TEMPLATE_FLAG, flag, message));
        }

        public static void WriteListToChat(List<string> list, bool delimited = false,
            string delimiter = COMMA_SEP)
        {
            WriteToChat(NiceList(list, delimited, delimiter));
        }

        public static void WriteConfirmationToChat<T>(string key, T value, string result = "confirmed")
        {
            WriteToChat(String.Format(Result(result), key, value.ToString()));
        }

        public static void WriteToChat(string message, string flag = null)
        {
            MyVisualScriptLogicProvider.SendChatMessageColored(
                string.Format(CHAT_TEMPLATE_MESSAGE, flag, message),
                author: MOD_CHAT_AUTHOR,
                color: CHAT_COLOR);
            //font: MyFontEnum.DarkBlue);
        }

        #endregion

        #region Notification Handlers

        public static void WriteToNotification(string message, string colorName)
        {
            MyVisualScriptLogicProvider.ShowNotification(message,
                disappearTimeMs: NOTIFICATION_DURATION * SECONDS_TO_MILLISECONDS, font: colorName);
        }

        #endregion

        #region Menu Strings Methods

        private static string Error(string topic) => MenuStrings.Error(topic);
        private static string Warning(string topic) => MenuStrings.Warning(topic);
        private static string Item(string topic) => MenuStrings.Item(topic);
        private static string Hint(string topic) => MenuStrings.Hint(topic);
        private static string Help(string topic) => MenuStrings.Help(topic);
        private static string Result(string topic) => MenuStrings.Result(topic);

        #endregion

        #region Logging Methods

        public static void LogBegin<T>(string prefix, T message, bool isServer = false)
        {
            LogFlag(prefix: prefix, message: message.ToString(), flag: BEGIN, isServer: isServer);
        }

        public static void LogEnd<T>(string prefix, T message, bool isServer = false)
        {
            LogFlag(prefix: prefix, message: message.ToString(), flag: END, isServer: isServer);
        }

        public static void LogTry<T>(string prefix, T message, bool isServer = false)
        {
            LogFlag(prefix: prefix, message: message.ToString(), flag: TRY, isServer: isServer);
        }

        public static void LogFail<T>(string prefix, T message, bool isServer = false)
        {
            LogFlag(prefix: prefix, message: message.ToString(), flag: FAIL, isServer: isServer);
        }

        public static void LogWarn<T>(string prefix, T message, bool isServer = false)
        {
            LogFlag(prefix: prefix, message: message.ToString(), flag: WARN, isServer: isServer);
        }

        public static void LogVar<T>(string prefix, string name, T varT, bool isServer = false)
        {
            LogFlag(prefix: prefix, message: string.Format(TEMPLATE_VAR, name, varT.ToString()), flag: VAR,
                isServer: isServer);
        }

        public static void LogFlag<T>(string prefix, T message, string flag, bool showHeader = false,
            bool isServer = false)
        {
            Log(prefix: prefix, message: string.Format(TEMPLATE_FLAG, flag) + message, showHeader: showHeader,
                isServer: isServer);
        }

        public static void Log<T>(T message, bool showHeader = false, bool isServer = false)
        {
            Log("", message, showHeader, isServer);
        }

        public static void Log<T>(string prefix, T message, bool showHeader = false, bool isServer = false)
        {
            if (isServer)
            {
                //  Ignore logging when client on multiplayer server.
                if (ConfigurableOres.Instance == null)
                {
                    return;
                }

                if (ConfigurableOres.Instance.IsMultiplayerClient)
                {
                    return;
                }
            }

            if (!LOGGING_ENABLED)
            {
                return;
            }

            if (showHeader)
            {
                MyLog.Default.WriteLineAndConsole(string.Format(TEMPLATE_LOG, GetRole, prefix, MODINFO_PRETTY_HEADER));
                MyLog.Default.WriteLineAndConsole(string.Format(TEMPLATE_LOG, GetRole, prefix, MODINFO_MOD_URL));
            }

            MyLog.Default.WriteLineAndConsole(string.Format(TEMPLATE_LOG, GetRole, prefix, message.ToString()));
        }

        #endregion

        #region Roles

        private static string GetRole
        {
            get
            {
                if (ConfigurableOres.Instance == null)
                {
                    return string.Empty;
                }

                string role;
                if (ConfigurableOres.Instance.IsDedicatedServer)
                {
                    role = ROLE_DEDICATED_SERVER;
                }
                else if (ConfigurableOres.Instance.IsMultiplayerHost)
                {
                    role = ROLE_CLIENT_HOST;
                }
                else if (ConfigurableOres.Instance.IsMultiplayerClient)
                {
                    role = ROLE_CLIENT;
                }
                else
                {
                    role = ROLE_SINGLE_PLAYER;
                }

                return role;
            }
        }

        #endregion

        #endregion
    }
}