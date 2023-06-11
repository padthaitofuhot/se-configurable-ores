/*
    Configurable Ores - A Space Engineers mod for managing planetary ores.
    Copyright 2022 Travis Wichert

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using VRageMath;
using static ConfigurableOres.ModInfo;
using static ConfigurableOres.Strings;

// ReSharper disable InconsistentNaming

namespace ConfigurableOres
{
    public static class MenuStrings
    {
        private static Dictionary<string, Dictionary<Keys, string>> MenuStringsDict =
            new Dictionary<string, Dictionary<Keys, string>>();

        #region MenuStrings

        #region MenuString Keys

        private enum Keys
        {
            Item,
            Arg,
            Hint,
            Help,
            Warning,
            Error,
            Result
        }

        #endregion

        #region Getters

        private static string Get(string topic, Keys key)
        {
            Log($"Get: {topic}/{key.ToString()}");

            Dictionary<Keys, string> inner;
            string value;

            if (MenuStringsDict.TryGetValue(topic, out inner) && MenuStringsDict[topic].TryGetValue(key, out value))
            {
                return value;
            }

            Log($"No string: {topic}/{key.ToString()}");
            return "";
        }

        public static string Item(string topic, string arg = "")
        {
            var line = Get(topic, Keys.Item);
            if (arg.Length > 0) line = $"{line} {arg}";
            return line;
        }

        public static string Hint(string topic, string arg = "")
        {
            var line = Get(topic, Keys.Hint);
            if (arg.Length > 0) line = string.Format(line, arg);
            return line;
        }

        public static string Arg(string topic)
        {
            return Get(topic, Keys.Arg);
        }

        public static string MenuLine(string breadcrumbs, string topic, string hintarg = "")
        {
            var item = Item(topic);
            var itemarg = Arg(topic);

            var hint = Hint(topic, hintarg);

            if (itemarg.Length > 0) return $"{breadcrumbs} {item} {itemarg}: {hint}";

            return $"{breadcrumbs} {item}: {hint}";
        }

        public static string Help(string topic)
        {
            return Get(topic, Keys.Help);
        }

        public static string MenuHelp(string topic)
        {
            var item = Item(topic);
            var help = Help(topic);
            return $"{item}: {help}";
        }

        public static string Warning(string topic)
        {
            return Get(topic, Keys.Warning);
        }

        public static string Error(string topic)
        {
            return Get(topic, Keys.Error);
        }

        public static string Result(string topic)
        {
            return Get(topic, Keys.Result);
        }

        #endregion

        #region Setters

        // Thanks for the pointers Digi
        private static void Add(string topic, Keys key, string value)
        {
            Log($"Add: {topic}/{key.ToString()}");

            Dictionary<Keys, string> innerDictionary;
            string keyValue;

            // If topic key does not exist, create it and add inner dict
            if (!MenuStringsDict.TryGetValue(topic, out innerDictionary))
            {
                var dict = new Dictionary<Keys, string> { { key, value } };
                MenuStringsDict.Add(topic, dict);
                return;
            }

            // If topic key already exists, just add the inner dict
            if (!MenuStringsDict[topic].TryGetValue(key, out keyValue))
            {
                MenuStringsDict[topic].Add(key, value);
                return;
            }

            // Shouldn't get here unless there's a dupe.
            Log($"Error: Could not add: {topic}/{key.ToString()}");
        }

        private static void AddMenu(string topic, string command, string hint)
        {
            AddItem(topic, command);
            AddHint(topic, hint);
        }

        private static void AddItem(string topic, string command)
        {
            Add(topic, Keys.Item, command);
        }

        private static void AddHint(string topic, string hint)
        {
            Add(topic, Keys.Hint, hint);
        }

        private static void AddArg(string topic, string arg)
        {
            Add(topic, Keys.Arg, arg);
        }

        private static void AddHelp(string topic, string help)
        {
            Add(topic, Keys.Help, help);
        }

        private static void AddWarning(string topic, string warning)
        {
            Add(topic, Keys.Warning, warning);
        }

        private static void AddError(string topic, string error)
        {
            Add(topic, Keys.Error, error);
        }

        private static void AddResult(string topic, string result)
        {
            Add(topic, Keys.Result, result);
        }

        #endregion

        static MenuStrings()
        {
            #region Results

            AddResult("done", "Done!");
            AddResult("confirmed", "{0} set to {1}");
            AddResult("action", "{0} has been {1}");
            AddResult("actions", "{0} have been {1}");

            #endregion

            #region Warnings

            AddWarning("inheritance",
                "WARNING: Settings are propagated from World level to Planet level to Ore level. If you make changes made at this level, it will also change the same settings for everything connected, in the order: World -> Planet -> Ore. For example, setting a minimum depth at the world level will set the same depth for all planets and all ores assigned to those planets. It is best to make changes at the world level before making more detailed changes on planets and assigned ores.");
            AddWarning("size_and_depth_system",
                "NOTE: In Space Engineers, ore deposits have a horizontal shape, vertical start depth, and vertical stop depth. The horizontal shapes are determined entirely by planet model data which is inaccessible to mods. However, the ore deposit vertical start and stop depths may be changed by mods, and that's what this mod does.");

            #endregion

            #region Errors

            AddError("default", "Command unknown or incorrect: {0}");
            AddError("general", "Error: {0}");
            AddError("ambiguous_command", "Ambiguous command: {0}");
            AddError("not_implemented", "Not implemented: {0}");

            AddError("not_positive", "{0} must be a positive number");
            AddError("not_boolean", "{0} must be one of these words: {1}");
            AddError("not_alphanumeric", "{0} must be letters or numbers accepted by SE's chat system");
            AddError("not_cubesize", "{0} must be \"Large\" or \"Small\"");
            AddError("bounds_error_greater_than", "{0} must be greater than {1}");
            AddError("bounds_error_lesser_than", "{0} must be lesser than {1}");

            #endregion

            #region Generic Menuing Stuff

            AddMenu("save", "Save", "Save mod configuration");
            AddMenu("reset", "Reset", "Reset these settings to previous");
            AddMenu("info", "Info", "Show info about {0}");
            AddMenu("list", "List", "Show list of {0}");
            AddMenu("add", "Add", "Add an item to the list");
            AddMenu("del", "Del", "Remove an item from the list");
            AddMenu("clear", "Clear", "Clear all items from the list");
            AddMenu("dump", "Dump", "Get dump generated oremappings to chat");
            AddMenu("help", "Help", "Get help");
            AddMenu("show", "Show", "Show current information and settings");

            #endregion

            #region AutoDepth Stuff

            AddItem("depth_detector_range_small", $"Small detector longest range");
            AddItem("depth_detector_range_large", $"Large detector longest range");
            AddMenu("depth", "Depth", "Ore deposit depth settings menu");
            AddItem("depth_summary", Item("depth"));
            AddHelp("depth_summary", "Edit the depth at which deposits spawn");
            AddMenu("depth_min", "Min", "<Meters>: Set minimum depth");
            AddHelp("depth_min",
                $"Sets the minimum depth at which an ore deposit can spawn. Any value less than 2 can cause ores to be spawned at the planet surface.");
            AddMenu("depth_max", "Max", "<Meters>: Set maximum depth");
            AddHelp("depth_max", $"Sets the maximum depth. Ores cannot spawn deeper than this.");
            AddMenu("depth_use_progressive", "UseProgressive", "<On/Off>: Scale depth of an ore's successive ore slots?");
            AddHelp("depth_use_progressive",
                $"When enabled, the {Item("depth")} system puts ore deposits progressively deeper each time they show up in a planet's ore mapping table (see guide). For example, if Iron appears 8 times in a planet ore mapping table, some Iron deposits will be particularly deep. This emulates \"Vanilla\" deposit depths and is enabled by default.");
            AddMenu("depth_use_detector", "UseDetector",
                $"<On/Off>: Configure {Item("depth_max")} depth from ore detector ranges?");
            AddHelp("depth_use_detector",
                $"When enabled, the {Item("depth")} system sets the {Item("depth_max")} depth equal to the maximum range of ore detectors during depth calculations. Enabled by default.");
            AddMenu("depth_curve", "Curve", "<Float>: Set the depth curve factor");
            AddHelp("depth_curve",
                $"Sets the shape of the {Item("depth")} curve. This provides more nuance to deposit depths. Smaller values cause ores to spawn deeper; larger values cause ores to spawn shallower.");
            AddMenu("depth_fuzz", "Fuzz", "<Float>: Set depth fuzz factor");
            AddHelp("depth_fuzz",
                $"Sets the variance or \"fuzziness\" of calculated ore deposit depths. Ore deposit depths are calculated and then increased or decreased slightly based on this value.  Depths will not exceed {Item("depth_min")} or {Item("depth_max")}.");
            AddMenu("depth_detector_size", "DetectorSize",
                $"<Block Size>: \"Large\" or \"Small\"");
            AddHelp("depth_detector_size",
                $"Must be set to either Large or Small. This is the grid size of the ore detector whose max range will be used when {Item("depth_use_detector")} is enabled. In the Vanilla game, Large Ore Detector has the longest range. If you use modded ore detectors or want an easier game, you could set this to Small.");
            AddMenu("depth_detector_factor", "DetectorFactor",
                "<Float>: % of detector range for auto depth");
            AddHelp("depth_detector_factor",
                $"Adjusts the relative depth of ore deposits from the range of the configured ore detector block size. The default is to use 100% (1.0) of the detector range. Setting this lower (< 1.0) will cause ores to spawn more shallow; whereas setting it higher (> 1.0) will cause ores to spawn deeper.");
            AddHelp("depth",
                $"These settings alter the behavior of the {Item("depth")} algorithm. The defaults provide a \"Vanilla feel\" and are recommended for new users of the mod. Please refer to the guide before changing Advanced settings."
                + DOUBLE_NEWLINE
                + MenuHelp("depth_min")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_max")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_use_progressive")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_use_detector")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_curve")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_fuzz")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_detector_size")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_detector_factor")
                + NEWLINE
            );

            // Include these only at the Ore level
            AddMenu("depth_custom_depth", "CustomDepth", "Set a custom deposit depth");
            AddHelp("depth_custom_depth", $"Sets a specific depth for the ore deposit.");
            AddMenu("depth_use_custom_depth", "UseCustomDepth", "Use the custom deposit depth setting");
            AddHelp("depth_use_custom_depth",
                $"When enabled, the depth calculation system is disabled and the value set by {Item("depth_custom_depth")} is used instead. This allows specific placement of ores.");
            AddHelp("ore_depth_help",
                NEWLINE
                + MenuHelp("depth")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_use_custom_depth")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_custom_depth")
                + NEWLINE
            );

            #endregion

            #region AutoSize Stuff

            AddMenu("size", "Size", "Ore deposit size settings menu");
            AddItem("size_summary", Item("size"));
            AddHelp("size_summary", "Edit deposit sizes");
            AddMenu("size_min", "Min", "<Meters>: Set minimum deposit size");
            AddHelp("size_min", $"Sets the minimum vertical size of an ore deposit.");
            AddMenu("size_max", "Max", "<Meters>: Set maximum deposit size");
            AddHelp("size_max", $"Sets the maximum vertical size of an ore deposit.");
            AddMenu("size_fuzz", "Fuzz", "<Float>: Set deposit size fuzz factor");
            AddHelp("size_fuzz",
                $"Sets the variance or \"fuzziness\" of a calculated ore deposit size. Ore deposit sizes are calculated and then increased or decreased based on a random number within this value.  Sizes will not exceed the {Item("size_min")} or {Item("size_max")} values.");
            AddMenu("size_factor", "Factor", "Set multiple of deposit depth to calculate deposit size");
            AddHelp("size_factor", $"Sets the percentage of the deposit depth to determine the deposit size.");
            AddHelp("size",
                $"These settings alter the behavior of the {Item("size")} algorithm. The defaults provide a \"Vanilla feel\" and are recommended for new users of the mod. Please refer to the guide before changing Advanced settings."
                + DOUBLE_NEWLINE
                + MenuHelp("size_min")
                + DOUBLE_NEWLINE
                + MenuHelp("size_max")
                + DOUBLE_NEWLINE
                + MenuHelp("size_fuzz")
                + DOUBLE_NEWLINE
                + MenuHelp("size_factor")
                + NEWLINE
            );

            // Include these only at the Ore level
            AddMenu("size_custom_size", "CustomSize", "Set a custom deposit size");
            AddHelp("size_custom_size", "Sets a specific vertical size for the ore deposit.");
            AddMenu("size_use_custom_size", "UseCustomSize", "Use the custom deposit size setting");
            AddHelp("size_use_custom_size",
                $"When enabled, the size calculation system is disabled and the value set by {Item("size_custom_size")} is used instead. This allows specific placement of ores.");
            AddHelp("ore_size_help",
                NEWLINE
                + MenuHelp("depth")
                + DOUBLE_NEWLINE
                + MenuHelp("size_use_custom_size")
                + DOUBLE_NEWLINE
                + MenuHelp("size_custom_size")
                + NEWLINE
            );

            #endregion

            #region Ore Stuff

            AddMenu("ore", "<Ore>", $"Edit this planet's config for <Ore>");
            AddMenu("ore_menu_add", Item("ore"), Hint("planet_add_ore"));
            AddMenu("ore_menu_del", Item("ore"), Hint("planet_del_ore"));
            AddItem("ore_voxel_material_type", "VoxelMaterialType");
            AddItem("ore_target_color", "TargetColor");
            AddItem("ore_color_influence", "ColorInfluence");
            AddItem("ore_slot_count", "Ore slot count");
            AddItem("ore_summary", "<Ore>");
            AddHelp("ore_summary",
                $"Allows editing individual ore settings. You can see this planet's ore assignments with the {Item("show")} command.");
            AddMenu("ore_rarity", "Rarity", "Edit ore rarity");
            AddHelp("ore_rarity",
                $"{Item("ore_rarity")} is the measure of how rare an ore is compared to other ores assigned to the planet. "
                + $"The higher the {Item("ore_rarity")} value, the more rare an ore is."
                + "This determines how often an ore will appear on a planet as well as its default deposit spawn depth and size. "
                + $"Ores with a low {Item("ore_rarity")} value compared to other ores will be significantly more common and shallow. "
                + $"Conversely, ores with a higher {Item("ore_rarity")} will be uncommon with deeper deposits. "
                + $"Ores with the same {Item("ore_rarity")} value as other ores will be as rare or common as those ores. "
                + $"Example: Consider a planet with three assigned ores. The first ore has a {Item("ore_rarity")} of 1 and the second and third ores have a {Item("ore_rarity")} value of 2. "
                + "In that case, the first ore will consume roughly 50% of the planet's ore slots, whereas the second and third ores will consume roughly 25% each. "
                + NEWLINE
                + $"In any case, two things are always true:"
                + NEWLINE
                + "1) Every ore will have at least one ore slot."
                + $"2) 100% of a planet's ore slots will be assigned any ore's {Item("ore_rarity")}."
            );
            AddHelp("ore",
                "Each ore has its own settings for further customization. This permits such things as adding Uranium back to the EarthLike planet, but setting its depth to something well outside the Vanilla Large grid ore detector (for example, 2000m)."
                + DOUBLE_NEWLINE
                + MenuHelp("ore_rarity")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_summary")
                + DOUBLE_NEWLINE
                + MenuHelp("size_summary")
                + NEWLINE
            );

            #endregion

            #region Planet Stuff

            AddItem("planets", "Planets");
            AddMenu("planet", "<Planet>", $"Edit planet named <Planet> (\"/ore mars\")");
            AddHint("planet_known", "{0} {1}: Edit {1} ore assignments");
            AddMenu("planet_show", "Show", "Show planet info and ore assignments.");
            AddMenu("planet_add_ore", "Add", $"Add \"{Item("ore")}\" with optional [{Item("ore_rarity")}]");
            AddArg("planet_add_ore", $"{Item("ore")} [{Item("ore_rarity")}]");
            AddHelp("planet_add_ore",
                $"Adds the given {Item("ore")} to the planet. <Ore> is always the name of any ore (for example, \"uranium\"). <Ore> will be added with the planet's mean {Item("ore_rarity")} value unless {Item("ore_rarity")} is provided with the {Item("add")} command."
            );
            AddError("planet_add_ore_duplicate",
                $"An ore may only be added once to each planet. If you want an ore to be more common, decrease its {Item("ore_rarity")} value.");
            AddMenu("planet_del_ore", "Del", $"Remove ore named \"{Item("ore")}\"");
            AddArg("planet_del_ore", Item("ore"));
            AddHelp("planet_del_ore",
                $"Removes the given {Item("ore")} from the planet."
            );
            AddMenu("planet_clear", "Clear", "Clears all ore assignments");
            AddHelp("planet_clear", "Clears all ore assignments from the planet and replaces with Stone.");
            AddHelp("planet",
                "Each planet has its own settings and ore assignments. This menu is where you edit those settings."
                + DOUBLE_NEWLINE
                + MenuHelp("planet_add_ore")
                + DOUBLE_NEWLINE
                + MenuHelp("planet_del_ore")
                + DOUBLE_NEWLINE
                + MenuHelp("ore_summary")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_summary")
                + DOUBLE_NEWLINE
                + MenuHelp("size_summary")
                + NEWLINE
            );

            #endregion

            #region Mod Menu

            AddMenu("mod", "Mod", "Edit mod settings");
            AddItem("mod_summary", "Mod");
            AddHelp("mod_summary", "These settings modify core mod behaviors such as logging.");
            AddMenu("mod_logging", "Logging", "<On/Off>: Enable or disable logging");
            AddHelp("mod_logging",
                $"{Item("mod_logging")}: This setting enables or disables writing debug messages to the Space Engineers log file. This mod produces a lot of logging messages, so leave logging disabled unless you are troubleshooting.");
            AddMenu("mod_command_prefix", "CommandPrefix", "Set the command prefix (default \"/ore\")");
            AddHelp("mod_command_prefix",
                $"{Item("mod_command_prefix")}: This setting can be any combination of letters and numbers which are accepted by Space Engineers chat system. It is recommended to leave this the default.");
            AddMenu("mod_disable_chat_commands", "DisableChatCommands",
                "Disable chat commands to prevent changes. You must edit the config file to enable them again.");
            AddMenu("mod_disable_chat_commands_confirm", "Confirm DisableChatCommands",
                "Confirm disabling chat commands");
            AddHelp("mod_disable_chat_commands",
                $"{Item("mod_disable_chat_commands")}: This command entirely disables the chat UI and requires a change to the mod's config file to undo. This is useful when you are finished making changes to your world and want to keep yourself from cheating or prevent shenanigans in multiplayer or dedicated server");
            AddWarning("mod_disable_chat_commands",
                DOUBLE_NEWLINE +
                $"WARNING: Disabling chat commands cannot be undone except by directly editing the config file while the game or server is shut down. To really disable chat commands you must use \"{Item("mod_disable_chat_commands_confirm")}\"" +
                DOUBLE_NEWLINE);
            AddWarning("mod_chat_commands_are_disabled",
                DOUBLE_NEWLINE +
                "***** Chat command system is now disabled. To re-enable it, you must edit the mod config file in the storage directory of this world save. For more information, please read the guide. *****" +
                DOUBLE_NEWLINE);
            AddHelp("mod",
                Help("mod_summary")
                + DOUBLE_NEWLINE
                + MenuHelp("mod_logging")
                + DOUBLE_NEWLINE
                + MenuHelp("mod_disable_chat_commands")
                + NEWLINE
            );

            #endregion

            #region World Stuff

            AddMenu("world", "World", "Edit world settings");
            AddMenu("world_rebuild", "Rebuild", "Force regenerate all planet ore maps");
            AddMenu("world_voxel_material", "<Voxel Material>", "The name of a voxel material");
            AddMenu("world_static_voxel_mats", "StaticVoxelMats", "Edit static VoxelMaterials list");
            AddMenu("world_ignored_planets", "IgnoredPlanets", "Edit ignored planets list");

            AddHelp("world",
                "These settings alter the entire world."
                + DOUBLE_NEWLINE
                + Warning("inheritance")
                + DOUBLE_NEWLINE
                + MenuHelp("mod_summary")
                + DOUBLE_NEWLINE
                + MenuHelp("depth_summary")
                + DOUBLE_NEWLINE
                + MenuHelp("size_summary")
                + NEWLINE
            );

            #endregion

            #region Root Stuff

            AddMenu("root_planets", "Planets", "List editable planets");
            AddMenu("root_ores", "Ores", "List known ores");
            AddHelp("root",
                "This mod's chat UI is organized like a tree. It has a trunk, branches, and leaves."
                + DOUBLE_NEWLINE
                + "\"{0}\" is the trunk of the tree. You're already there whenever you type {0}."
                + DOUBLE_NEWLINE
                + $"When you typed \"{{0}} {Item("help")}\", you went from the trunk \"{{0}}\" to a branch named \"{Item("help")}\". "
                + "Whenever you go to a branch, you will see hints about any other branches you can get to from there. "
                + $"Most branches have a \"{Item("help")}\" branch so you can get more help."
                + DOUBLE_NEWLINE
                + "Some branches have leaves. A leaf can be edited. A leaf always needs a value."
                + "A hint for the value is shown in angle brackets, <Like This>. For example, to add an ore to planet Mars, "
                + $"you'd type \"{{0}} mars add {Item("ore")}\" and replace {Item("ore")} with the name of a real ore: uranium, iron, whatever ore you want!"
                + DOUBLE_NEWLINE
                + "All commands are case-insensitive, meaning you can MiX CaPitAL anD LOwerCasE and it will work fine. "
                + "The only exception to case-insensitivity is when specifying Large or Small ore detector sizes."
                + DOUBLE_NEWLINE
                + $"Try typing \"{{0}} {Item("planets")}\" to get a list of planets you can use when you see {Item("planet")}."
                + NEWLINE
            );

            #endregion
        }

        #endregion

        #region Local Logging

        private static string LOGGING_PREFIX = "MENU_STRINGS";

        private static void LogBegin<T>(T message, bool isServer = false) =>
            Helpers.LogBegin(LOGGING_PREFIX, message, isServer);

        private static void LogEnd<T>(T message, bool isServer = false) =>
            Helpers.LogEnd(LOGGING_PREFIX, message, isServer);

        private static void LogTry<T>(T message, bool isServer = false) =>
            Helpers.LogTry(LOGGING_PREFIX, message, isServer);

        private static void LogFail<T>(T message, bool isServer = false) =>
            Helpers.LogFail(LOGGING_PREFIX, message, isServer);

        private static void LogWarn<T>(T message, bool isServer = false) =>
            Helpers.LogWarn(LOGGING_PREFIX, message, isServer);

        private static void LogVar<T>(string name, T varT, bool isServer = false) =>
            Helpers.LogVar(LOGGING_PREFIX, name, varT, isServer);

        private static void Log<T>(T message, bool showHeader = false, bool isServer = false) =>
            Helpers.Log(LOGGING_PREFIX, message, showHeader, isServer);

        #endregion
    }

    public static class Strings
    {
        #region Legacy to be converted

        #region Symbols

        public const string SEPARATOR = " : ";
        public const string BULLET = ">";
        public const char NEWLINE = '\n';
        public static readonly string DOUBLE_NEWLINE = $"{NEWLINE}{NEWLINE}";
        public const char SPACE = ' ';
        public const char ASTERISK = '*';
        public const string COMMA_SEP = ", ";

        #endregion

        #region Formatting

        public const string FORMAT_PAD_TWO = "D2";
        public const string FORMAT_LARGE_NUMBER = "N0";
        public const string FORMAT_TIME_STAMP = "HH:mm:ss";
        public const string FORMAT_DATE_TIME_STAMP = "MM/d/yyyy HH:mm:ss";
        public const string FORMAT_TIME_SPAN = "h'h 'm'm 's's'";
        public const string FORMAT_HOURS = "0' hours'";
        public const string FORMAT_MINUTES = "0' minutes'";
        public const string FORMAT_SECONDS = "0' seconds'";
        public const string TEMPLATE_TIME_STAMP = "Time stamp: {0}";

        public static readonly string TEMPLATE_LOG =
            MOD_CHAT_AUTHOR + "[{0}]" + BULLET + SPACE + "{1}" + SPACE + BULLET + SPACE + "{2}";

        #endregion

        #region ChatIU

        #region ChatUI Formatting

        public static readonly Color CHAT_COLOR = Color.Magenta;
        public const string CHAT_TEMPLATE_FLAG = "{0} {1}";
        public const string CHAT_TEMPLATE_MESSAGE = "{0}{1}";
        public const string CHAT_TEMPLATE_VAR = "{0} = {1}";

        #endregion

        #region ChatUI Menuing

        public const string CHAT_MENU_PREFIX_HEADER_TOP = ">>>";
        public const string CHAT_MENU_PREFIX_HEADER_COMMANDS = "==";
        public const string CHAT_LINE_THICK = "===========================================";

        public const string CHAT_LINE_THIN =
            "--------------------------------------------------------------------------";

        public static readonly string CHAT_HELLO = NEWLINE + MODINFO_VERSION_TEXT;
        public const string CHAT_HELLO_EDITING_HINT = "Type '{0}' to start editing!";
        public const string CHAT_HELLO_RESTART_HINT = "You must restart your game after changes.";

        public static readonly string CHAT_HELLO_COMMANDS_ENABLED =
            CHAT_HELLO_EDITING_HINT + NEWLINE + CHAT_HELLO_RESTART_HINT;

        public static readonly string CHAT_MENU_HEADER_TOP = NEWLINE + $"{CHAT_MENU_PREFIX_HEADER_TOP} {{0}}" + NEWLINE;
        public static readonly string CHAT_MENU_HEADER_COMMANDS = $"{CHAT_MENU_PREFIX_HEADER_COMMANDS} Commands:";

        public static readonly string CHAT_MENU_HEADER_ADVANCED =
            NEWLINE + $"{CHAT_MENU_PREFIX_HEADER_COMMANDS} Advanced (read guide):";

        public static readonly string CHAT_MENU_HEADER_GENERIC = $"{CHAT_MENU_PREFIX_HEADER_COMMANDS} {{0}}: ";

        #endregion

        #region ChatUI Menus

        #region Root Menu

        public static readonly string CHAT_MENU_ROOT_HEADER = $"{MOD_NAME} - Planet OreMappings Editor";

        public const string CHAT_MENU_ROOT_HINT_EXAMPLE = "Example: '{0} {1}'";

        public static readonly List<string> CommandHints = new List<string>
        {
            "alien del uranium (Deletes uranium from Alien planet)",
            "world autodepth progressive enable (Enables progressive automatic depth)",
            "PertAM add ice 1 (Adds ice deposits to Pertam and makes them VERY common)",
            "moon add uranium 10 (adds uranium to Moon and makes it rare)",
            "Triton del silicon (Deletes silicon from Triton)",
            "Europa del ice (Deletes ice from Europa)",
            "mars (Shows the menu for Mars)",
            "alien clear (Clears ALL ores from Alien planet)",
            "moon show (Shows the ores configured on Moon)",
            "world deeper MinDepth 40 (Sets the minimum depth for ALL ore deposits to 40 meters)"
        };

        #endregion

        #region Chat Menu Planets

        public const string CHAT_MENU_PLANET_ASSIGNED_ORES = "Assigned Ores (Rarity)";
        public const string CHAT_MENU_PLANET_UNASSIGNED_ORES = "Unassigned Ores";
        public const string CHAT_MENU_PLANET_TOTAL_SLOTS = "Total ore slots";
        public const string CHAT_MENU_PLANET_MEAN_RARITY = "Average ore Rarity";
        public const string CHAT_MENU_PLANET_MEAN_FREQUENCY = "Average slots per ore";
        public const string CHAT_MENU_PLANET_ASSIGNED_ORE_COUNT = "Assigned ores count";
        public const string CHAT_MENU_PLANET_UNASSIGNED_ORE_COUNT = "Unassigned ores count";
        public const string CHAT_MENU_PLANET_STATIC_VOXEL_MATS_COUNT = "Static voxel materials count";


        public const string CHAT_HELP_PLANET_HEADER = " Planet Config - {0}";
        public const string TEMPLATE_ORE_ASSIGNMENTS = "{0} (Rarity {1})";
        public const string CHAT_HELP_VOXEL_LIST = "Voxels: List oremapping VoxelMaterials";
        public const string CHAT_HELP_ORES = "<ore>: Configure the assigned ore";
        public const string CHAT_HELP_ORE_ADD = "add: <Ore> [rarity] - Add ore, optional rarity";
        public const string CHAT_HELP_ORE_DEL = "del: <Ore> - Delete ore";
        public const string CHAT_HELP_ORE_CLEAR = "clear: Clear ores and fill with Stone";
        public const string CHAT_HELP_ORE_SHOW = "show: Write the current ore map to chat";
        public const string CHAT_TEMPLATE_TOTAL_SLOTS = "Total Ore Slots: {0}";
        public const string CHAT_TEMPLATE_AVG_RARITY = "Ores on {0} with Rarity >{1} will be deep";
        public const string CHAT_REBUILD_ORE_MAPPINGS = "Planet ore maps rebuilt and saved";
        public const string CHAT_TEMPLATE_SHOW_COMMAND = "Ore Maps for {0}";
        public const string CHAT_TEMPLATE_SHOW_KEY = "Ore Name (Rarity): Deposit Depth / Deposit Size";
        public const string CHAT_TEMPLATE_SHOW_OREMAP = "{0} ({1}): {2}m / {3}m";

        #endregion

        #region Chat Menu Ore

        public static readonly string CHAT_MENU_ORE_CONTENT_HELP =
            $"";

        public const string CHAT_HELP_ORE_HEADER = " {0} - {1} Settings (read the guide)";
        public const string CHAT_HELP_ORE_ADD_LIST = " Unassigned Ores";
        public const string CHAT_HELP_ORE_ADD_FAILED = "Add failed: duplicate ore or no free slots";
        public const string CHAT_HELP_ORE_ADD_NOTFOUND = "Add failed: requested ore not found in list. Use \"/ore ores\" to get list of available ores.";
        public static string CHAT_HELP_ORE_ADD_SUCCESS = "{0} added to {1} with rarity {2}";

        public static string CHAT_HELP_ORE_ADD_FAILED_WITH_RARITY =
            "Failed to add {0} to {1}. Could not parse rarity argument from: {2}";

        public const string CHAT_HELP_ORE_DEL_LIST = " Assigned Ores";
        public static string CHAT_HELP_ORE_DEL_SUCCESS = "{0} removed from {1}";
        public const string CHAT_HELP_ORE_DEL_FAILED = "Del failed";
        public static string CHAT_HELP_ORE_CLEAR_SUCCESS = "Cleared ores from {0}";
        public const string CHAT_HELP_ORE_CLEAR_FAILED = "Clear failed";
        public const string CHAT_HELP_MENU_ORE_SETTING_NAME = "<Setting> <Value>: Change a setting";
        public const string HELP_CONFIG_ORE_RARITY = "Rarity: {0}";
        public const string HELP_CONFIG_ORE_RARE = " (Rare";
        public const string HELP_CONFIG_ORE_COMMON = " (Common";
        public const string CHAT_CONFIG_ORE_AVG_RARITY = ", >{0} will be deep)";
        public const string HELP_CONFIG_ORE_DEPTH = "Depth: {0}";
        public const string HELP_CONFIG_ORE_AUTODEPTH_ACTIVE = " (OreAutoDepth On)";
        public const string HELP_CONFIG_ORE_DEPTH_DEEPER_ORES = "Depth: {0} + Deeper Ores: {1}";
        public const string HELP_CONFIG_ORE_AUTOSIZE_ACTIVE = " (OreAutoSize On)";
        public const string HELP_CONFIG_ORE_SIZE = "Size: {0}";
        public const string HELP_CONFIG_ORE_AUTO_DEPTH = "OreAutoDepth: {0}";
        public const string HELP_CONFIG_ORE_AUTO_SIZE = "OreAutoSize: {0}";
        public const string HELP_CONFIG_ORE_AUTO_DEPTH_ORE_DETECTOR = "UseDetector: {0}";
        public const string HELP_CONFIG_ORE_AUTO_DEPTH_DETECTOR_SIZE = " (Req. Detector: {0})";
        public const string HELP_CONFIG_ORE_RARITY_SUCCESS = "{0} rarity set to {1}";
        public const string HELP_CONFIG_ORE_DEPTH_SUCCESS = "{0} deposit depth set to {1}";
        public const string HELP_CONFIG_ORE_SIZE_SUCCESS = "{0} deposit size set to {1}";
        public const string HELP_CONFIG_ORE_AUTO_DEPTH_SUCCESS = "{0} OreAutoDepth set to {1}";
        public const string HELP_CONFIG_ORE_AUTO_SIZE_SUCCESS = "{0} OreAutoSize set to {1}";
        public const string HELP_CONFIG_ORE_AUTO_DEPTH_ORE_DETECTOR_SUCCESS = "{0} UseDetector set to {1}";

        public const string HELP_CONFIG_ORE_AUTO_DEPTH_DETECTOR_FACTOR = "AutoDepthDetectorFactor: {0}";
        public const string HELP_CONFIG_ORE_AUTO_DEPTH_FUZZ = "AutoDepthFuzz: {0}";
        public const string HELP_CONFIG_ORE_AUTO_SIZE_FACTOR = "AutoSizeFactor: {0}";
        public const string HELP_CONFIG_ORE_AUTO_SIZE_FUZZ = "AutoSizeFuzz: {0}";

        public const string HELP_RARITY =
            "How rare is this ore? Larger rarirty, fewer deposits. (1=common, >1=more rare)";

        public const string HELP_CONFIG_ORE_GENERATOR_TUNABLES = "Generator tweakables (READ THE DOCS!)";

        #endregion

        #endregion

        #endregion

        #region Roles

        public const string ROLE_DEDICATED_SERVER = "DS";
        public const string ROLE_CLIENT = "CL";
        public const string ROLE_CLIENT_HOST = "MH";
        public const string ROLE_SINGLE_PLAYER = "SP";

        #endregion

        #region Logging

        #region Logging Preficies

        public const string TEMPLATE_FLAG = "[{0}] ";
        public const string VAR = "var";
        public const string TEMPLATE_VAR = "{0} = {1}";
        public const string BEGIN = "BEGIN";
        public const string END = "END";
        public const string TRY = "TRY";
        public const string FAIL = "FAIL";
        public const string WARN = "WARN";
        public const string SUCCESS = "SUCCESS";
        public const string EXISTS = "EXISTS";
        public const string NO_DATA = "NO DATA";
        public const string NO_RECIPIENT = "NO RECIPIENT";
        public const string FOUND = "FOUND";
        public const string NOT_FOUND = "NOT FOUND";
        public const string MOD_NOT_FOUND = "MOD NOT FOUND";
        public const string EMPTY_QUEUE = "EMPTY QUEUE";
        public const string DONE = "Done!";

        #endregion

        #region Session Logging

        public const string LOG_PREFIX_SESSION = "SESSION";
        public const string LOG_INIT = "Init()";
        public const string LOG_INITIALIZE = "Initialize(¯\\_(ツ)_/¯)";
        public const string LOG_BEFORE_START = "BeforeStart()";
        public const string LOG_UNLOAD_DATA = "UnloadData()";
        public const string LOG_UPDATE_AFTER_SIMULATION = "UpdateAfterSimulation()";
        public const string LOG_LOAD_DATA = "LoadData() OVERRIDE";
        public const string LOG_LOAD_OREDETECTOR_RANGES = "Load maximum ore detector ranges";
        public const string LOG_LOAD_VOXELMATERIALS = "Load mineable voxel materials";
        public const string LOG_LOAD_PLANET_DEFINITIONS = "Load planet generator definitions";
        public const string LOG_FOUND_PLANET_CONFIGURATION = "Found planet configuration";

        #endregion

        #region Configuration Logging

        public const string LOG_PREFIX_CONFIGURATION = "CONFIG";
        public const string LOG_LOAD_CONFIGURATION = "Load configuration";
        public const string LOG_READ_CONFIGURATION = "READ configuration";
        public const string LOG_SAVE_CONFIGURATION = "Save configuration";
        public const string LOG_WRITE_CONFIGURATION = "Write configuration";
        public const string LOG_SERIALIZE_CONFIGURATION = "Serialize configuration";
        public const string LOG_DESERIALIZE_CONFIGURATION = "Deserialize configuration";
        public const string LOG_FROM_XML_CONFIGURATION = "Get config from XML";
        public const string LOG_WORLD_VAR_NOT_FOUND = "Value not found in world store sbc!";
        public const string LOG_CREATE_DEFAULT_CONFIGURATION = "Create default configuration";
        public const string LOG_CONFIGURATION_INVALID = "Invalid config file";
        public const string LOG_SANITY_CHECK_SETTINGS = "Sanity check config data";

        #endregion

        #region ChatUI Logging

        public const string LOG_CHAT_COMMAND_HANDLER = "ChatHandler({0}, {1}, {2})";

        #endregion

        #region MyPlanetConfiguration Logging

        public const string LOG_PREFIX_PLANET = "ORE_PLANET";
        public const string LOG_PURGE_MISSING_PLANET_CONFIGS = "Purge configurations of missing planets";
        public const string LOG_PURGE_MISSING_PLANET_CONFIG = "Queue purge missing planet: {0}";
        public const string LOG_LOAD_AND_CHECK_PLANET_SETTINGS = "Load and check planet configs";
        public const string LOG_CHECK_AND_CONFIGURE_PLANET_SETTING = "Init planet: {0}";
        public const string LOG_NEW_PLANET_FOUND = "New planet";
        public const string LOG_IGNORE_FILTERED_PLANET = "Filtered planet: {0}";
        public const string LOG_REMOVE_FILTERED_PLANET = "Remove filtered planet from settings";
        public const string LOG_SKIP_INITIALIZE_KNOWN_PLANET = "Known planet: {0}";
        public const string LOG_CREATE_PLANET_CONFIG = "Creating config";
        public const string LOG_APPLY_SETTINGS_TO_CONFIGURED_PLANETS = "Apply settings to all configured planets";
        public const string LOG_APPLY_SETTINGS_TO_PLANET = "Applied oremappings to planet: {0}";
        public const string LOG_PLANET_DEFAULT_ASSIGNMENTS = "Generating default ore assignments";

        public const string LOG_PLANET_GET_ORESLOTS_FROM_BLUE =
            "Get ore slot count from blue channel values in planet OreMapping";

        public const string LOG_PLANET_MASS_ASSIGNMENT = "Mass assign ores from existing OreMapping";
        public const string LOG_PLANET_MASS_INITIALIZE_RARITY = "Mass initialize ore assignments' rarity";
        public const string LOG_PLANET_GENERATE_MAPPINGS = "Generating planet ore mappings";
        public const string LOG_PLANET_FREE_SLOTS = "Give each ore one ore slot";
        public const string LOG_PLANET_RAFFLE_RARITY_TICKETS = "Add tickets to the raffle based on rarity";

        #endregion

        #region MyPlanetOreAssignment Logging

        public const string LOG_PREFIX_ORE_ASSIGNMENT = "ORE_ASSIGNMENT";

        public const string LOG_ORE_ASSIGNMENT_INITIALIZE =
            "New OreAssignment: VoxelMaterial={0} MinedOre={1} Count={2} DepthStart={3} DepthSize={4}";

        public const string LOG_ORE_ASSIGNMENT_UPDATE_RARITY =
            "UpdateRarity: SlotCount={0} VoxelMaterial={1} MyCount={2} Rarity={3}";

        public const string LOG_ORE_ASSIGNMENT_UPDATE_DEPTHS =
            "UpdateDepthBasis: Ore={0} Rarity/MeanRarity={1}/{2} Common={3} OreUseDetectorSize={4} CustomOreDepthModifier={5}, DepthStart={6} DepthSize={7}";

        public const string LOG_ORE_ASSIGNMENT_TO_OREMAPPING =
            "OreMap: Value={0} Type={1} Start={2} Depth={3} TargetColor={4} ColorInfluence={5}";

        #endregion

        #region Voxel Ore Collection

        public const string LOG_PREFIX_VOXEL_ORE_COLLECTION = "VOXEL_ORE_COLLECTION";

        #endregion

        #endregion

        #endregion
    }
}