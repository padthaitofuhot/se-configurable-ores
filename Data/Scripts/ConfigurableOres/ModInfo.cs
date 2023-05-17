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

// ReSharper disable InconsistentNaming
namespace ConfigurableOres
{
    public class ModInfo
    {
        /// <summary>
        /// Release: write / rewrite
        /// Major version: updates / new features -- compatability may break
        /// Minor version: minor changes / bugfixes -- backwards compatibility maintained
        /// Build: build - no CI/CD, not used.
        /// </summary>
        private const int RELEASE_NUMBER = 1;

        private const int MAJOR_VERSION = 0;
        private const int MINOR_VERSION = 12;
        private const int BUILD_NUMBER = 0;

        public static readonly Version Version = new Version(
            major: RELEASE_NUMBER,
            minor: MAJOR_VERSION,
            build: MINOR_VERSION,
            revision: BUILD_NUMBER
        );

        public const string MOD_NAME = "Configurable Ores";
        public const uint MOD_ID = 2973891097;
        public const ushort CHANNEL_ID = 51723;
        public const string MOD_CHAT_AUTHOR = "ORE";

        public static readonly string MODINFO_VERSION_TEXT = MOD_NAME + " v" + Version;

        public static readonly string MODINFO_PRETTY_HEADER =
            "-=] " + MODINFO_VERSION_TEXT + " (Mod ID " + MOD_ID + ") [=-";

        public static readonly string MODINFO_MOD_URL =
            "https://steamcommunity.com/sharedfiles/filedetails/?id=" + MOD_ID;
    }
}