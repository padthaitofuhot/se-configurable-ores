using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.World.Generator;
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
using static System.String;
using static ConfigurableOres.Configuration;
using static ConfigurableOres.Helpers;
using static ConfigurableOres.Strings;
using static ConfigurableOres.MenuStrings;
using static VRage.Game.MyCubeSize;

namespace ConfigurableOres
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class ConfigurableOres : MySessionComponentBase
    {
        # region Data & Cache

        /// <summary>
        /// See also Initialize();
        /// 
        /// There are multiple potential entry points into a MySessionComponentBase
        /// We check this flag to avoid collisions
        /// </summary>
        private bool _isInitialized;

        public IEnumerable<MyPlanetGeneratorDefinition> AllPlanetGeneratorDefinitions;
        public IEnumerable<MyVoxelMaterialDefinition> AllVoxelMaterialDefinitions;

        public Dictionary<MyCubeSize, float> OreDetectorRanges = new Dictionary<MyCubeSize, float>();

        //public Dictionary<string, string> VoxelMaterialToOre = new Dictionary<string, string>();
        public MyVoxelOreCollection VoxelOres = new MyVoxelOreCollection();

        public Configuration Config;

        /// <summary>
        /// Our local class reference
        /// </summary>
        public static ConfigurableOres Instance;

        # endregion

        #region Overrides

        #region Init

        /// <summary>
        /// From Patrick:
        /// 
        /// Initialization code. Safe to call multiple times.
        /// There is no certainty whether Init() or LoadData() will be
        /// called first (or possibly one of the event listeners), so 
        /// here we are. Use this anywhere the code might "start."
        /// </summary>
        private void Initialize()
        {
            if (_isInitialized) return;

            var logging = LOGGING_ENABLED;
            LOGGING_ENABLED = true;
            Log(LOG_INITIALIZE, showHeader: true);
            LOGGING_ENABLED = logging;

            Instance = this;

            // static class constructor
            // var loadMenuStrings = Item("done");

            _isInitialized = true;
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            LogBegin(LOG_INIT);

            Initialize();
            base.Init(sessionComponent);

            LogEnd(LOG_INIT);
        }

        public override void BeforeStart()
        {
            LogBegin(LOG_BEFORE_START);

            WriteToChat(CHAT_HELLO);

            // Don't start chat handler on DS
            if (IsMultiplayerHost || IsSinglePlayer)
            {
                // chat handler fuse
                if (!Config.DisableChatCommands)
                {
                    MyAPIGateway.Utilities.MessageEnteredSender += ChatHandler;
                    WriteToChat(Format(CHAT_HELLO_COMMANDS_ENABLED, COMMAND_PREFIX));
                }
            }

            LogEnd(LOG_BEFORE_START);
        }

        #endregion

        #region Load data

        public override void LoadData()
        {
            LogBegin(LOG_LOAD_DATA);

            Initialize();

            Config = Load(IsDedicatedServer || IsMultiplayerHost || IsSinglePlayer);
            LOGGING_ENABLED = Config.Logging;

            LoadOreDetectorMaxRanges();
            LoadMineableVoxelMaterials();
            AllPlanetGeneratorDefinitions = MyDefinitionManager.Static.GetPlanetsGeneratorsDefinitions();
            LoadAndCheckPlanetConfigurations();
            ApplyPlanetSettings();

            if (IsDedicatedServer || IsMultiplayerHost || IsSinglePlayer) Save(Config);

            LogEnd(LOG_LOAD_DATA);
        }

        protected override void UnloadData()
        {
            LogBegin(LOG_UNLOAD_DATA);

            if (IsDedicatedServer || IsMultiplayerHost || IsSinglePlayer)
            {
                Save(Config);
                MyAPIGateway.Utilities.MessageEnteredSender -= ChatHandler;
            }

            Instance = null;
            Config = null;

            LogEnd(LOG_UNLOAD_DATA);
        }

        #endregion

        #endregion

        #region Data Loading

        private void LoadOreDetectorMaxRanges()
        {
            /*
             * Load all ore detector definitions.
             * Find the maximum range of all large and small ore detectors.
             */

            LogBegin(LOG_LOAD_OREDETECTOR_RANGES);

            OreDetectorRanges.Add(Large, 0f);
            OreDetectorRanges.Add(Small, 0f);

            foreach (var oreDetector in MyDefinitionManager.Static.GetDefinitionsOfType<MyOreDetectorDefinition>())
            {
                if (oreDetector.CubeSize == Large
                    && oreDetector.MaximumRange > OreDetectorRanges[Large])
                    OreDetectorRanges[Large] = oreDetector.MaximumRange;

                if (oreDetector.CubeSize == Small
                    && oreDetector.MaximumRange > OreDetectorRanges[Small])
                    OreDetectorRanges[Small] = oreDetector.MaximumRange;
            }

            LogVar("Max: OreDetectorRanges[MyCubeSize.Large]", OreDetectorRanges[Large]);
            LogVar("Max: OreDetectorRanges[MyCubeSize.Small]", OreDetectorRanges[Small]);

            LogEnd(LOG_LOAD_OREDETECTOR_RANGES);
        }

        private void LoadMineableVoxelMaterials()
        {
            /*
             * Load up all the voxel definitions from the game, add anything relevant to lookup tables
             */

            LogBegin(LOG_LOAD_VOXELMATERIALS);

            AllVoxelMaterialDefinitions = MyDefinitionManager.Static.GetVoxelMaterialDefinitions().ToList();

            foreach (var voxelMaterial in AllVoxelMaterialDefinitions)
            {
                if (!voxelMaterial.CanBeHarvested) continue;

                var oreName = voxelMaterial.MinedOre;
                var voxelName = voxelMaterial.Id.SubtypeId.String;
                var isStatic = Config.StaticVoxelMaterials.Any(v => StringContains(v, voxelName));

                switch (isStatic)
                {
                    case true:
                        VoxelOres.Add(voxelName, oreName, true);
                        break;

                    case false:
                    {
                        if (Config.NeverStaticVoxelMaterials.Any(v => StringContains(v, voxelName)))
                        {
                            VoxelOres.Add(voxelName, oreName, false);
                            Log($"{voxelName} -> {oreName} is in NeverStaticVoxelMaterials list");
                            continue;
                        }

                        if (Config.IgnoredOres.Any(v => StringContains(v, oreName)))
                        {
                            Log($"{voxelName} -> {oreName} is in IgnoredOres list");
                            continue;
                        }

                        // Assume a VoxelMaterial with a duplicate ore is static
                        if (VoxelOres.ContainsOre(oreName))
                        {
                            // Add to config file so we don't have to process it again and squash duplicates
                            if (!Config.StaticVoxelMaterials.Any(v => StringContains(v, voxelName)))
                            {
                                Config.StaticVoxelMaterials.Add(voxelName);
                            }

                            Log($"{voxelName} -> {oreName} added to StaticVoxelMaterials list");
                            VoxelOres.Add(voxelName, oreName, true);
                            continue;
                        }

                        Log($"{voxelName} -> {oreName} added as minable ore");
                        VoxelOres.Add(voxelName, oreName, false);
                        break;
                    }
                }
            }

            Log(Helpers.NiceList(VoxelOres.DebugMap()));

            LogEnd(LOG_LOAD_VOXELMATERIALS);
        }

        private void LoadAndCheckPlanetConfigurations()
        {
            /*
             * todo: wash me 
             */

            LogBegin(LOG_LOAD_AND_CHECK_PLANET_SETTINGS);

            var planetsToRemove = new List<string>();

            // Spew configured planets we know about going into this
            if (Config.Logging)
                Config.MyPlanetConfigurations.MemberPlanets
                    .ForEach(p => LogVar(LOG_FOUND_PLANET_CONFIGURATION, p.Name));

            /*
            /*
             * Clean settings to remove planets which are no longer present
             * although tbh usually deleting a planet definition from the game after it's had an instance
             * created will corrupt the save.  Completely unrelated to this mod.  But hey, we can try to be
             * graceful about things.
             */
            if (Config.MyPlanetConfigurations != null && Config.MyPlanetConfigurations.Count() > 0)
            {
                LogBegin(LOG_PURGE_MISSING_PLANET_CONFIGS);
                foreach (var planet in
                         Config.MyPlanetConfigurations.MemberPlanets.Where(planet =>
                             !AllPlanetGeneratorDefinitions
                                 .Select(p => p.Id.SubtypeId.String)
                                 .Contains(planet.Name)))
                {
                    Log(Format(LOG_PURGE_MISSING_PLANET_CONFIG, planet.Name));
                    planetsToRemove.Add(planet.Name);
                }

                LogEnd(LOG_PURGE_MISSING_PLANET_CONFIGS);
            }

            foreach (var planetGeneratorDefinition in AllPlanetGeneratorDefinitions)
            {
                var planetSubtypeId = planetGeneratorDefinition.Id.SubtypeId.ToString();

                Log(Format(LOG_CHECK_AND_CONFIGURE_PLANET_SETTING, planetSubtypeId));

                // filter out planets we won't use and shouldn't touch
                var isFiltered = false;
                foreach (var ignored in Config.IgnoredPlanets)
                {
                    isFiltered |= planetSubtypeId.IndexOf(ignored, StringComparison.CurrentCultureIgnoreCase) > -1;
                }

                if (isFiltered)
                {
                    Log(Format(LOG_IGNORE_FILTERED_PLANET, planetSubtypeId));
                    // Remove instances of filtered planets that are somehow in the configuration.
                    if (Config.MyPlanetConfigurations.Contains(planetSubtypeId))
                    {
                        LogWarn(LOG_REMOVE_FILTERED_PLANET);
                        planetsToRemove.Add(planetSubtypeId);
                    }

                    continue;
                }

                switch (Config.MyPlanetConfigurations.Contains(planetSubtypeId))
                {
                    // Skip new planet initialization, planet is already in settings
                    case true:
                        Log(Format(LOG_SKIP_INITIALIZE_KNOWN_PLANET, planetSubtypeId));

                        Config.MyPlanetConfigurations.Get(planetSubtypeId).ReInit();
                        break;

                    // New MyPlanetConfiguration found, create new procedural ores MyPlanetConfiguration instance and add to settings.
                    case false:
                        Log(Format(LOG_NEW_PLANET_FOUND, planetSubtypeId));
                        LogBegin(LOG_CREATE_PLANET_CONFIG);

                        Config.MyPlanetConfigurations.Add(
                            new MyPlanetConfigurationCollectionMember(
                                planetGeneratorDefinition.Id.SubtypeId.String,
                                new MyPlanetConfiguration(planetGeneratorDefinition.OreMappings)
                            )
                        );

                        LogEnd(LOG_CREATE_PLANET_CONFIG);
                        break;
                }
            }

            LogBegin("Purging planets in purge queue");
            LogVar("Purge queue size", planetsToRemove.Count);
            foreach (var planetToRemove in planetsToRemove)
            {
                LogVar("Purging planet config", planetToRemove);
                Config.MyPlanetConfigurations.Remove(planetToRemove);
            }

            LogEnd("Purging planets in purge queue");

            LogEnd(LOG_LOAD_AND_CHECK_PLANET_SETTINGS);
        }

        #endregion

        #region Apply Planet Settings

        private void ApplyPlanetSettings()
        {
            /*
             * ==============================================================================
             * This is where we replace the planet's OreMappings with our own.
             * Thanks for the inspiration, enenra.  If it weren't for AQD - Deeper Ores
             * this mod would not exist.
             * ==============================================================================
             */

            LogBegin(LOG_APPLY_SETTINGS_TO_CONFIGURED_PLANETS);

            foreach (var planetGeneratorDefinition in AllPlanetGeneratorDefinitions)
            {
                var planetSubtypeId = planetGeneratorDefinition.Id.SubtypeId.ToString();

                // Skip changing oremappings of planets for which there are no configs or zero oremapping slots (such as Overvent)
                if (!Config.MyPlanetConfigurations.Contains(planetSubtypeId) ||
                    Config.MyPlanetConfigurations.Get(planetSubtypeId).OreSlotCount < 1)
                {
                    Log(Format(LOG_APPLY_SETTINGS_TO_PLANET,
                        $"{planetSubtypeId} is in IgnoredPlanets list or has no ore slots, skipping."));
                    continue;
                }

                var intendedMappings = Config.MyPlanetConfigurations
                    .Get(planetSubtypeId)
                    .GeneratedPlanetOreMappings.ToArray();

                planetGeneratorDefinition.OreMappings = intendedMappings;

                switch (CheckOreMappingsWereApplied(planetSubtypeId, planetGeneratorDefinition.OreMappings,
                            intendedMappings))
                {
                    case false:
                        LogFail("CheckOreMappingsWereApplied() returned False");
                        Log(Format(LOG_APPLY_SETTINGS_TO_PLANET, planetSubtypeId));
                        break;
                    case true:
                        Log(Format(LOG_APPLY_SETTINGS_TO_PLANET, planetSubtypeId));
                        break;
                }
            }

            LogEnd(LOG_APPLY_SETTINGS_TO_CONFIGURED_PLANETS);
        }

        private bool CheckOreMappingsWereApplied(string planetName, MyPlanetOreMapping[] originalMappings,
            MyPlanetOreMapping[] intendedMappings)
        {
            //var originList = new List<MyPlanetOreMapping>();
            //var intendedList = new List<MyPlanetOreMapping>();

            //var asdf = originalMappings.GetEnumerator();

            for (var i = 0; i < originalMappings.Length; i++)
            {
                var asdf = originalMappings[i].ToString();
                var qwer = intendedMappings[i].ToString();

                if (asdf == qwer) continue;

                LogFail($"A {planetName} OreMapping did not match the intended assignment:");
                LogVar("Planet's OreMapping", asdf);
                LogVar("Intended OreMapping:", qwer);
                return false;
            }

            return true;
        }

        #endregion

        #region Chat Menu

        #region Handler

        private void ChatHandler(ulong sender, string messageText, ref bool sendToOthers)
        {
            Log(Format(LOG_CHAT_COMMAND_HANDLER, sender.ToString(), messageText, sendToOthers.ToString()));

            // Chat fuse burned
            if (Config.DisableChatCommands) return;
            
            if (!IsMyMatch(COMMAND_PREFIX, messageText)) return;

            if (!MenuRoot(COMMAND_PREFIX, TrimMyMatch(COMMAND_PREFIX, messageText)))
                WriteErrorToChat(Error("default"), messageText);
        }

        #endregion

        #region Chat Menu Root

        private bool MenuRoot(string breadcrumbs, string message)
        {
            if (message.Length < 1) return MenuRootDisplay(breadcrumbs);

            // Help
            //if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("root"));

            // World
            if (IsMyMatch(Item("world"), message))
            {
                return MenuWorld(breadcrumbs, RegexTrim(Item("world"), message));
            }

            // List Planets
            if (IsMyMatch(Item("root_planets"), message))
            {
                var menuText = new StringBuilder();

                menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("root_planets"), breadcrumbs)));

                //menuText.AppendLine(Format(CHAT_MENU_HINT_PLANETS, BreadCrumb(breadcrumbs, CHAT_MENU_ITEM_PLANETS)));

                menuText.AppendLine(NiceList(Config.MyPlanetConfigurations.GetNames(), true));
                /*foreach (var planetName in Config.MyPlanetConfigurations.GetNames())
                {
                    menuText.AppendLine(Format(CHAT_MENU_HINT_PLANET_KNOWN, breadcrumbs, planetName));
                }*/

                WriteToChat(menuText.ToString());
                return true;
            }

            // List Ores
            if (IsMyMatch(Item("root_ores"), message))
            {
                var menuText = new StringBuilder();

                menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("root_ores"), breadcrumbs)));

                menuText.AppendLine(NiceList(VoxelOres.GetUsableOres()));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Named Planet
            foreach (var planetName in Config.MyPlanetConfigurations.GetNames())
            {
                if (!IsMyMatch(planetName, message)) continue;
                if (Config.MyPlanetConfigurations.Get(planetName).OreSlotCount < 1)
                {
                    // todo: needs strings
                    WriteToChat($"{planetName} has no ore slots and is not configurable.");
                    return false;
                }

                return MenuPlanet(breadcrumbs, planetName, RegexTrim(planetName, message));
            }

            return false;
        }

        private bool MenuRootDisplay(string breadcrumbs)
        {
            /*
             * >>> Mod root menu header

                == Commands from here:
                {Command Prefix} help: Get help.
                {Command Prefix} deeper: Edit "deeper ores" settings (Optional)
                {Command Prefix} world: Edit world settings (Advanced)
                {Command Prefix} {Planet Name}: Edit planet {Planet Name} ores

             */

            var menuText = new StringBuilder();

            menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(CHAT_MENU_ROOT_HEADER, breadcrumbs)));

            menuText.AppendLine(Format(CHAT_MENU_HEADER_COMMANDS));
            // menuText.AppendLine(MenuLine(breadcrumbs, "help"));
            menuText.AppendLine(MenuLine(breadcrumbs, "root_planets"));
            menuText.AppendLine(MenuLine(breadcrumbs, "root_ores"));
            menuText.AppendLine(MenuLine(breadcrumbs, "world"));
            menuText.AppendLine(MenuLine(breadcrumbs, "planet"));

            /*foreach (var planetName in Config.MyPlanetConfigurations.GetNames())
            {
                menuText.AppendLine(Format(CHAT_MENU_HINT_PLANET, breadcrumbs, planetName));
            }*/

            //
            // == Planets you can customize:
            //var planetNames = Config.MyPlanetConfigurations.GetNames();
            //menuText.Append(NiceList(planetNames, true));

            //
            // Example command: /ore alien del uranium
            //menuText.Append(NEWLINE);
            //CommandHints.ShuffleList();
            //menuText.AppendLine(Format(CHAT_MENU_ROOT_HINT_EXAMPLE, COMMAND_PREFIX, CommandHints.FirstOrDefault()));

            WriteToChat(menuText.ToString());
            return true;
        }

        private bool MenuWorldRebuild()
        {
            Config.MyPlanetConfigurations.GenerateMappings();
            Save(Config);

            WriteToChat(CHAT_REBUILD_ORE_MAPPINGS);
            return true;
        }

        #endregion

        #region Chat Menu World

        #region Chat Menu World Root

        private bool MenuWorld(string breadcrumbs, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("world"));

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("world"), breadcrumbs)));

                menuText.AppendLine("");
                menuText.AppendLine(Format(CHAT_MENU_HEADER_COMMANDS));
                //menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size"));
                menuText.AppendLine(MenuLine(breadcrumbs, "mod"));

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_ADVANCED));
                // todo: implement configuring Ignored Planets
                //menuText.AppendLine(Format(CHAT_MENU_HINT_IGNORED_PLANETS, breadcrumbs));
                // todo: implement configuring Static Voxel Materials
                //menuText.AppendLine(Format(CHAT_MENU_HINT_STATIC_VOXEL_MATERIALS, breadcrumbs));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            //if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("world"));

            // Mod settings
            if (IsMyMatch(Item("mod"), message))
            {
                return MenuWorldModSettings(breadcrumbs, RegexTrim(Item("mod"), message));
            }

            // Depth
            if (IsMyMatch(Item("depth"), message))
            {
                return MenuAutoDepth(breadcrumbs, RegexTrim(Item("depth"), message));
            }

            // Size
            if (IsMyMatch(Item("size"), message))
            {
                return MenuAutoSize(breadcrumbs, RegexTrim(Item("size"), message));
            }

            // Ignored Planets
            if (IsMyMatch(Item("world_ignored_planets"), message))
            {
                return MenuIgnoredPlanets(breadcrumbs, RegexTrim(Item("world_ignored_planets"), message));
            }

            // Static Voxel Materials
            if (IsMyMatch(Item("world_static_voxel_mats"), message))
            {
                return MenuStaticVoxelMats(breadcrumbs, RegexTrim(Item("world_static_voxel_mats"), message));
            }

            // Hidden
            LogVar("in world menu, got to Hidden commands. message", message);
            if (IsMyMatch(Item("world_rebuild"), message))
            {
                return MenuWorldRebuild();
            }

            if (IsMyMatch(Item("dump"), message))
            {
                WriteToChat(Config.MyPlanetConfigurations.DumpOreMappings());
                return true;
            }

            if (IsMyMatch(Item("save"), message))
            {
                Save(Config);
                return true;
            }

            if (IsMyMatch("apply", message))
            {
                // Try this and see what happens
                WriteToChat("Trying to apply new mappings...");
                ApplyPlanetSettings();
                return true;
            }

            return false;
        }

        #endregion

        #region Chat Menu World / Mod Settings

        private bool MenuWorldModSettings(string breadcrumbs, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("mod"));

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("mod"), breadcrumbs)));

                menuText.AppendLine("");
                menuText.AppendLine(Format(CHAT_MENU_HEADER_COMMANDS));
                //menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "show"));
                //menuText.AppendLine(Format(CHAT_MENU_HINT_COMMAND_PREFIX, breadcrumbs));
                menuText.AppendLine(MenuLine(breadcrumbs, "mod_logging"));
                menuText.AppendLine(MenuLine(breadcrumbs, "mod_disable_chat_commands"));
                menuText.AppendLine(MenuLine(breadcrumbs, "reset"));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            //if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("mod"));

            // Show
            if (IsMyMatch(Item("show"), message))
            {
                var settings = new Dictionary<string, string>
                {
                    //{CHAT_MENU_ITEM_COMMAND_PREFIX, Config.CommandPrefix},
                    { Item("mod_logging"), Config.Logging.ToString() }
                };
                WriteToChat(DisplaySettings(breadcrumbs, settings));
            }

            // Reset
            if (IsMyMatch(Item("reset"), message))
            {
                var item = Item("reset");
                Config.CommandPrefix = DEFAULT_COMMAND_PREFIX;
                Config.Logging = DEFAULT_LOGGING_ENABLED;
                Save(Config);
                WriteConfirmationToChat("settings", item, "actions");
                return true;
            }

            // Command Prefix
            // todo: implement

            // Logging
            if (IsMyMatch(Item("mod_logging"), message))
            {
                var item = Item("mod_logging");
                var parsed = ChatParseBool(item, message);
                if (!parsed.Item1) return false;

                Config.Logging = parsed.Item2;
                WriteConfirmationToChat(item, parsed.Item2);

                Save(Config);
                return true;
            }

            // Disable Chat Commands
            if (IsMyMatch(Item("mod_disable_chat_commands"), message))
            {
                WriteToChat(Warning("mod_disable_chat_commands"));
                return true;
            }

            // Confirm Disable Chat Commands
            if (IsMyMatch(Item("mod_disable_chat_commands_confirm"), message))
            {
                var item = Item("mod_disable_chat_commands_confirm");
                Config.DisableChatCommands = true;
                WriteConfirmationToChat(item, true);
                WriteToChat(Warning("mod_chat_commands_are_disabled"));

                Save(Config);
                return true;
            }

            return true;
        }

        #endregion

        #region Chat Menu World / Depth

        private bool MenuAutoDepth(string breadcrumbs, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("depth"));

            Log("In MenuAutoDepth(string breadcrumbs, string message)");
            LogVar("message", message);

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("depth"), breadcrumbs)));

                menuText.AppendLine("");
                menuText.AppendLine(CHAT_MENU_HEADER_COMMANDS);
                //menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "show"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_min"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_max"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_use_detector"));
                menuText.AppendLine(MenuLine(breadcrumbs, "reset"));

                menuText.AppendLine(CHAT_MENU_HEADER_ADVANCED);
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_use_progressive"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_curve"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_fuzz"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_detector_size"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_detector_factor"));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            //if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("depth"));

            // Show
            if (IsMyMatch(Item("show"), message))
            {
                var settings = new Dictionary<string, string>
                {
                    {
                        Item("depth_detector_range_small"),
                        OreDetectorRanges[Small].ToString(CultureInfo.CurrentCulture)
                    },
                    {
                        Item("depth_detector_range_large"),
                        OreDetectorRanges[Large].ToString(CultureInfo.CurrentCulture)
                    },
                    { Item("depth_min"), Config.Depth.Min.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_max"), Config.Depth.Max.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_use_progressive"), Config.Depth.UseProgressive.ToString() },
                    { Item("depth_use_detector"), Config.Depth.UseDetector.ToString() },
                    { Item("depth_curve"), Config.Depth.Curve.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_fuzz"), Config.Depth.Fuzz.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_detector_size"), Config.Depth.DetectorSize.ToString() },
                    { Item("depth_detector_factor"), Config.Depth.DetectorFactor.ToString(CultureInfo.CurrentCulture) }
                };
                WriteToChat(DisplaySettings(breadcrumbs, settings));
                return true;
            }

            // Reset
            if (IsMyMatch(Item("reset"), message))
            {
                var item = Item("reset");

                Config.Depth.Reset();
                WriteConfirmationToChat("settings", item, "actions");

                MenuWorldRebuild();
                Save(Config);
                return true;
            }

            // Depth Min
            if (IsMyMatch(Item("depth_min"), message))
            {
                var item = Item("depth_min");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (Config.Depth.Max < parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_lesser_than"), item, Item("depth_max"));
                    return false;
                }

                Config.Depth.SetMin(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);
                return true;
            }

            // Depth Max
            if (IsMyMatch(Item("depth_max"), message))
            {
                var item = Item("depth_max");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (Config.Depth.Min > parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_greater_than"), item, Item("depth_max"));
                    return false;
                }

                Config.Depth.SetMax(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);
                return true;
            }

            // Progressive Depth Enable/Disable
            if (IsMyMatch(Item("depth_use_progressive"), message))
            {
                var item = Item("depth_use_progressive");
                var parsed = ChatParseBool(item, message);
                if (!parsed.Item1) return false;

                Config.Depth.SetUseProgressive(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);

                return true;
            }

            // Use Detector
            if (IsMyMatch(Item("depth_use_detector"), message))
            {
                var item = Item("depth_use_detector");
                var parsed = ChatParseBool(item, message);
                if (!parsed.Item1) return false;

                Config.Depth.SetUseDetector(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);

                return true;
            }

            Log("Before booltest");
            // Bool Test
            if (IsMyMatch("booltest", message))
            {
                Log("In IsMyMatch(\"booltest\", message)");

                var item = "booltest";

                LogVar("item", item);
                LogVar("message", message);

                var parsed = ChatParseBool(item, message);

                LogVar("parsed.Item1 (success)", parsed.Item1);
                LogVar("parsed.Item2 (boolean)", parsed.Item2);

                return true;
            }

            Log("After booltest");

            // Auto Depth Curve
            if (IsMyMatch(Item("depth_curve"), message))
            {
                var item = Item("depth_curve");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                Config.Depth.SetCurve(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);

                return true;
            }

            // Auto Depth Fuzz
            if (IsMyMatch(Item("depth_fuzz"), message))
            {
                var item = Item("depth_fuzz");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                Config.Depth.SetFuzz(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);

                return true;
            }

            // Detector Size
            if (IsMyMatch(Item("depth_detector_size"), message))
            {
                var item = Item("depth_detector_size");
                var parsed = ChatParseCubeSize(item, message);
                if (!parsed.Item1) return false;

                Config.Depth.SetDetectorSize(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);

                return true;
            }

            // Detector Factor
            if (IsMyMatch(Item("depth_detector_factor"), message))
            {
                var item = Item("depth_detector_factor");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                Config.Depth.SetDetectorFactor(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);

                return true;
            }

            Log("Leaving MenuAutoDepth(string breadcrumbs, string message)");

            return false;
        }

        #endregion

        #region Chat Menu World / Size

        private bool MenuAutoSize(string breadcrumbs, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("size"));

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("size"), breadcrumbs)));

                menuText.AppendLine("");
                menuText.AppendLine(Format(CHAT_MENU_HEADER_COMMANDS));
                //menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "show"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size_min"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size_max"));
                menuText.AppendLine(MenuLine(breadcrumbs, "reset"));

                menuText.AppendLine(CHAT_MENU_HEADER_ADVANCED);
                menuText.AppendLine(MenuLine(breadcrumbs, "size_factor"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size_fuzz"));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            if (IsMyMatch(Item("help"), message))
                return DisplayHelp(BreadCrumb(breadcrumbs, Item("help")), Help("size"));

            // Show
            if (IsMyMatch(Item("show"), message))
            {
                var settings = new Dictionary<string, string>
                {
                    { Item("size_min"), Config.Size.Min.ToString(CultureInfo.CurrentCulture) },
                    { Item("size_max"), Config.Size.Max.ToString(CultureInfo.CurrentCulture) },
                    { Item("size_factor"), Config.Size.Factor.ToString(CultureInfo.CurrentCulture) },
                    { Item("size_fuzz"), Config.Size.Fuzz.ToString(CultureInfo.CurrentCulture) },
                };
                //WriteToChat(DisplaySettings(BreadCrumb(breadcrumbs, Item("show")), settings));
                WriteToChat(DisplaySettings(breadcrumbs, settings));
                return true;
            }

            // Reset
            if (IsMyMatch(Item("reset"), message))
            {
                var item = Item("reset");

                Config.Size.Reset();
                WriteConfirmationToChat("settings", item, "actions");

                MenuWorldRebuild();
                Save(Config);
                return true;
            }

            // Size Min
            if (IsMyMatch(Item("size_min"), message))
            {
                var item = Item("size_min");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (Config.Size.Max < parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_lesser_than"), item, Item("size_max"));
                    return false;
                }

                Config.Size.SetMin(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);
                return true;
            }

            // Size Max
            if (IsMyMatch(Item("size_max"), message))
            {
                var item = Item("size_max");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (Config.Size.Min > parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_greater_than"), item, Item("size_min"));
                    return false;
                }

                Config.Size.SetMax(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);
                return true;
            }

            // Auto Size Factor
            if (IsMyMatch(Item("size_factor"), message))
            {
                var item = Item("size_factor");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                Config.Size.SetFactor(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                MenuWorldRebuild();
                Save(Config);

                return true;
            }

            // Auto Size Fuzz
            if (IsMyMatch(Item("size_fuzz"), message))
            {
                var item = Item("size_fuzz");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                Config.Size.SetFuzz(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                // todo: implement independent autodepth fuzz per planet
                MenuWorldRebuild();
                Save(Config);

                return true;
            }

            return false;
        }

        #endregion

        #region Chat Menu World / IgnoredPlanets

        private bool MenuIgnoredPlanets(string breadcrumbs, string message)
        {
            WriteErrorToChat(Error("not_implemented"), message);
            return false;
        }

        #endregion

        #region Chat Menu World / StaticVoxelMats

        private bool MenuStaticVoxelMats(string breadcrumbs, string message)
        {
            WriteErrorToChat(Error("not_implemented"), message);
            return false;
        }

        #endregion

        #endregion

        #region Chat Menu Planet

        #region Chat Menu Planet Root

        private bool MenuPlanet(string breadcrumbs, string planetName, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, planetName);

            Log("Got to MenuPlanet");
            LogVar("breadcrumbs", breadcrumbs);
            LogVar("planetName", planetName);
            LogVar("message", message);

            var planet = Config.MyPlanetConfigurations.Get(planetName);

            var assignedOreList = planet.GetAssignedOres();
            var assignedOresPretty = planet.GetPrettyAssignedOresWithRarity();
            var unassignedOreList = planet.GetUnassignedOres();
            var staticOresList = planet.StaticOres.ToList();

            var totalSlots = planet.AssignableOresBlueChannelValues.Count;
            var meanRarity = planet.MeanRarity;
            var meanFrequency = planet.MeanFrequency;

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("planet_known"), breadcrumbs, planetName)));

                menuText.AppendLine("");
                menuText.AppendLine(CHAT_MENU_HEADER_COMMANDS);
                //menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "planet_show"));
                menuText.AppendLine(MenuLine(breadcrumbs, "planet_add_ore", planetName));
                menuText.AppendLine(MenuLine(breadcrumbs, "planet_del_ore", planetName));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size"));
                menuText.AppendLine(MenuLine(breadcrumbs, "ore"));
                menuText.AppendLine(MenuLine(breadcrumbs, "clear"));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            //if (IsMyMatch(Item("help"), message)) return DisplayHelp(BreadCrumb(breadcrumbs, Item("help")), Help("planet"));

            // Show
            if (IsMyMatch(Item("planet_show"), message))
            {
                var menuText = new StringBuilder();

                var settings = new Dictionary<string, string>
                {
                    { CHAT_MENU_PLANET_TOTAL_SLOTS, totalSlots.ToString() },
                    { CHAT_MENU_PLANET_ASSIGNED_ORE_COUNT, assignedOreList.Count.ToString() },
                    { CHAT_MENU_PLANET_UNASSIGNED_ORE_COUNT, unassignedOreList.Count.ToString() },
                    { CHAT_MENU_PLANET_MEAN_RARITY, meanRarity.ToString(CultureInfo.CurrentCulture) },
                    { CHAT_MENU_PLANET_MEAN_FREQUENCY, meanFrequency.ToString(CultureInfo.CurrentCulture) },
                    { CHAT_MENU_PLANET_STATIC_VOXEL_MATS_COUNT, staticOresList.Count.ToString() },
                    { CHAT_MENU_PLANET_ASSIGNED_ORES, NiceList(assignedOresPretty, true) },
                    { CHAT_MENU_PLANET_UNASSIGNED_ORES, NiceList(unassignedOreList, true) }
                };
                menuText.AppendLine(DisplaySettings(breadcrumbs, settings));

                /*menuText.Append(Format(CHAT_MENU_HEADER_GENERIC, CHAT_MENU_PLANET_ASSIGNED_ORES));
                menuText.AppendLine(NiceList(assignedOresPretty, true));
                menuText.AppendLine();
                menuText.Append(Format(CHAT_MENU_HEADER_GENERIC, CHAT_MENU_PLANET_UNASSIGNED_ORES));
                menuText.AppendLine(NiceList(unassignedOreList, true));*/

                WriteToChat(menuText.ToString());

                return true;
            }

            // Add an ore
            if (IsMyMatch(Item("add"), message))
            {
                message = RegexTrim(Item("add"), message);
                return MenuPlanetAddOre(breadcrumbs, planetName, message);
            }

            // Remove an ore
            if (IsMyMatch(Item("del"), message))
            {
                message = RegexTrim(Item("del"), message);
                return MenuPlanetDelOre(breadcrumbs, planetName, message);
            }

            // Clear all ore mappings and replace everything with stone
            if (IsMyMatch(Item("clear"), message))
            {
                if (!Config.MyPlanetConfigurations.Get(planetName).ClearOreAssignments()) return false;

                Config.MyPlanetConfigurations.Get(planetName).GenerateMappings();

                Save(Config);

                WriteToChat(Format(CHAT_HELP_ORE_CLEAR_SUCCESS, planetName));
                return true;
            }

            // AutoDepth
            if (IsMyMatch(Item("depth"), message))
            {
                var item = Item("depth");
                message = RegexTrim(item, message);
                return MenuPlanetAutoDepth(breadcrumbs, planetName, message);
            }

            // AutoSize
            if (IsMyMatch(Item("size"), message))
            {
                var item = Item("size");
                message = RegexTrim(item, message);
                return MenuPlanetAutoSize(breadcrumbs, planetName, message);
            }

            // Edit ore assignment by ore name
            foreach (var oreName in assignedOreList)
            {
                if (!IsMyMatch(oreName, message)) continue;

                message = RegexTrim(oreName, message);
                return MenuPlanetOre(breadcrumbs, oreName, planetName, message);
            }

            if (IsMyMatch(Item("dump"), message))
            {
                WriteToChat(planet.DumpOreMappings());
                return true;
            }

            return false;
        }

        #endregion

        #region Chat Menu Planet / AutoDepth

        private bool MenuPlanetAutoDepth(string breadcrumbs, string planetName, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("depth"));

            var planet = Config.MyPlanetConfigurations.Get(planetName);

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("depth"), breadcrumbs)));
                
                menuText.AppendLine("");
                menuText.AppendLine(CHAT_MENU_HEADER_COMMANDS);
                // menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "show"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_min"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_max"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_use_detector"));
                menuText.AppendLine(MenuLine(breadcrumbs, "reset"));
                
                menuText.AppendLine(CHAT_MENU_HEADER_ADVANCED);
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_use_progressive"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_curve"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_fuzz"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_detector_size"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_detector_factor"));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            // if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("depth"));

            // Show
            if (IsMyMatch(Item("show"), message))
            {
                var settings = new Dictionary<string, string>
                {
                    { Item("depth_min"), planet.Depth.Min.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_max"), planet.Depth.Max.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_use_progressive"), planet.Depth.UseProgressive.ToString() },
                    { Item("depth_use_detector"), planet.Depth.UseDetector.ToString() },
                    { Item("depth_curve"), planet.Depth.Curve.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_fuzz"), planet.Depth.Fuzz.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_detector_size"), planet.Depth.DetectorSize.ToString() },
                    { Item("depth_detector_factor"), planet.Depth.DetectorFactor.ToString(CultureInfo.CurrentCulture) }
                };
                WriteToChat(DisplaySettings(breadcrumbs, settings));
                return true;
            }

            // Reset
            if (IsMyMatch(Item("reset"), message))
            {
                var item = Item("reset");
                planet.Depth.Reset();
                WriteConfirmationToChat("settings", item, "actions");

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Depth Min
            if (IsMyMatch(Item("depth_min"), message))
            {
                var item = Item("depth_min");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (planet.Depth.Max < parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_lesser_than"), item, Item("depth_max"));
                    return false;
                }

                planet.Depth.SetMin(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Depth Max
            if (IsMyMatch(Item("depth_max"), message))
            {
                var item = Item("depth_max");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (planet.Depth.Min > parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_greater_than"), item, Item("depth_min"));
                    return false;
                }

                planet.Depth.SetMax(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Progressive Depth Enable/Disable
            if (IsMyMatch(Item("depth_use_progressive"), message))
            {
                var item = Item("depth_use_progressive");
                var parsed = ChatParseBool(item, message);
                if (!parsed.Item1) return false;

                planet.Depth.SetUseProgressive(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Use Detector
            if (IsMyMatch(Item("depth_use_detector"), message))
            {
                var item = Item("depth_use_detector");
                var parsed = ChatParseBool(item, message);
                if (!parsed.Item1) return false;

                planet.Depth.SetUseDetector(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Auto Depth Curve
            if (IsMyMatch(Item("depth_curve"), message))
            {
                var item = Item("depth_curve");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                planet.Depth.SetCurve(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Auto Depth Fuzz
            if (IsMyMatch(Item("depth_fuzz"), message))
            {
                var item = Item("depth_fuzz");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                planet.Depth.SetFuzz(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Detector Size
            if (IsMyMatch(Item("depth_detector_size"), message))
            {
                var item = Item("depth_detector_size");
                var parsed = ChatParseCubeSize(item, message);
                if (!parsed.Item1) return false;

                planet.Depth.SetDetectorSize(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Detector Factor
            if (IsMyMatch(Item("depth_detector_factor"), message))
            {
                var item = Item("depth_detector_factor");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                planet.Depth.SetDetectorFactor(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            return false;
        }

        #endregion

        #region Chat Menu Planet / AutoSize

        private bool MenuPlanetAutoSize(string breadcrumbs, string planetName, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("size"));
            var planet = Config.MyPlanetConfigurations.Get(planetName);

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("size"), breadcrumbs)));

                menuText.AppendLine("");
                menuText.AppendLine(CHAT_MENU_HEADER_COMMANDS);
                //menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "show"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size_min"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size_max"));
                menuText.AppendLine(MenuLine(breadcrumbs, "reset"));

                menuText.AppendLine(CHAT_MENU_HEADER_ADVANCED);
                menuText.AppendLine(MenuLine(breadcrumbs, "size_factor"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size_fuzz"));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
           // if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("size"));

            // Show
            if (IsMyMatch(Item("show"), message))
            {
                var settings = new Dictionary<string, string>
                {
                    { Item("size_min"), planet.Size.Min.ToString(CultureInfo.CurrentCulture) },
                    { Item("size_max"), planet.Size.Max.ToString(CultureInfo.CurrentCulture) },
                    { Item("size_factor"), planet.Size.Factor.ToString(CultureInfo.CurrentCulture) },
                    { Item("size_fuzz"), planet.Size.Fuzz.ToString(CultureInfo.CurrentCulture) },
                };
                WriteToChat(DisplaySettings(breadcrumbs, settings));
                return true;
            }

            if (IsMyMatch(Item("reset"), message))
            {
                var item = Item("reset");
                planet.Size.Reset();
                WriteConfirmationToChat("settings", item, "actions");

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Size Min
            if (IsMyMatch(Item("size_min"), message))
            {
                var item = Item("size_min");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (planet.Size.Max < parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_lesser_than"), item, Item("size_max"));
                    return false;
                }

                planet.Size.SetMin(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Size Max
            if (IsMyMatch(Item("size_max"), message))
            {
                var item = Item("size_max");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (planet.Size.Min > parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_greater_than"), item, Item("size_min"));
                    return false;
                }

                planet.Size.SetMax(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Auto Size Factor
            if (IsMyMatch(Item("size_factor"), message))
            {
                var item = Item("size_factor");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                planet.Size.SetFactor(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Auto Size Fuzz
            if (IsMyMatch(Item("size_fuzz"), message))
            {
                var item = Item("size_fuzz");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                planet.Size.SetFuzz(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            return false;
        }

        #endregion

        #region Chat Menu Planet Add Ore

        private bool MenuPlanetAddOre(string breadcrumbs, string planetName, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("planet_add_ore"));

            var planet = Config.MyPlanetConfigurations.Get(planetName);

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("planet_add_ore"), breadcrumbs, planetName)));

                menuText.AppendLine("");
                menuText.AppendLine(Format(CHAT_MENU_HEADER_COMMANDS));
                //menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(Format(MenuLine(breadcrumbs, "ore_menu_add"), planetName));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            // if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("planet_add_ore"));

            // <Ore> [Rarity]
            foreach (var ore in VoxelOres.GetUsableOres())
            {
                if (!IsMyMatch(ore, message)) continue;

                message = RegexTrim(ore, message);

                var rarity = planet.MeanRarity;

                // Skip parsing rarity if nothing left to parse
                if (message.Length > 0)
                {
                    if (!float.TryParse(message, out rarity))
                    {
                        WriteToChat(Format(CHAT_HELP_ORE_ADD_FAILED_WITH_RARITY, ore, planetName, message));
                    }
                }

                if (planet.AddOreAssignment(ore, rarity))
                {
                    planet.GenerateMappings();

                    Save(Config);
                    WriteToChat(Format(CHAT_HELP_ORE_ADD_SUCCESS, ore, planetName, rarity));
                    return true;
                }

                break;
            }

            WriteToChat(CHAT_HELP_ORE_ADD_FAILED);
            return true;
        }

        #endregion

        #region Chat Menu Planet Del Ore

        private bool MenuPlanetDelOre(string breadcrumbs, string planetName, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("planet_del_ore"));
            var planet = Config.MyPlanetConfigurations.Get(planetName);

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                // menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("planet_del_ore"), breadcrumbs, planetName)));

                menuText.AppendLine("");
                menuText.AppendLine(CHAT_MENU_HEADER_COMMANDS);
                // menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(Format(MenuLine(breadcrumbs, "ore_menu_del"), planetName));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            // if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("planet_del_ore"));

            // <Ore>
            LogVar("Checking for matching ores in message", message);
            foreach (var ore in VoxelOres.GetUsableOres())
            {
                LogVar("Ore", ore);
                if (IsMyMatch(ore, message))
                {
                    Log($"Found {ore}!");
                    if (planet.DelOreAssignment(ore))
                    {
                        Log(
                            "PlanetConfiguration reports successful deletion of ore assignment. Running GenerateMappings()");
                        planet.GenerateMappings();
                        Log("Saving config...");
                        Save(Config);

                        WriteToChat(Format(CHAT_HELP_ORE_DEL_SUCCESS, ore, planetName));
                        return true;
                    }

                    LogFail("planet.DelOreAssignment(ore) appears to have failed, why?");
                    // todo: what if break isn't correct here?
                    break;
                }

                Log("Not this ore");
            }

            Log("Couldn't find that ore in the ");
            WriteToChat(CHAT_HELP_ORE_DEL_FAILED);
            return false;
        }

        //todo: remove after review
        private bool MenuPlanetDelOreDisplay(string planetName)
        {
            var menuText = new StringBuilder();
            menuText.AppendLine(Format(CHAT_MENU_PREFIX_HEADER_TOP, CHAT_HELP_ORE_DEL_LIST));
            menuText.Append(NiceList(GetFormattedOresAssignmentList(Config.MyPlanetConfigurations.Get(planetName)),
                true));

            WriteToChat(menuText.ToString());
            return true;
        }

        #endregion

        #region Chat Menu Planet Hidden

        private bool MenuPlanetCmdDebug(string planetName)
        {
            var planet = Config.MyPlanetConfigurations.Get(planetName);
            var showText = new StringBuilder();

            /*showText.AppendLine(Format(CHAT_MENU_PREFIX_HEADER_TOP,
                Format(CHAT_TEMPLATE_SHOW_COMMAND, planetName) + " " +
                Format(CHAT_TEMPLATE_TOTAL_SLOTS, planet.OreSlotCount)));
            */
            showText.AppendLine("");
            showText.AppendLine(CHAT_TEMPLATE_SHOW_KEY);
            showText.AppendLine(CHAT_LINE_THIN);

            foreach (var mapping in planet.GeneratedPlanetOreMappings)
            {
                var minedOre = GetMinedOreByVoxelMaterialName(mapping.Type);
                var depth = mapping.Start;
                var size = mapping.Depth;
                var rarity = Config.MyPlanetConfigurations.Get(planetName).Get(minedOre).Rarity;
                showText.AppendLine(Format(CHAT_TEMPLATE_SHOW_OREMAP, minedOre, rarity, depth, size));
            }

            WriteToChat(showText.ToString());
            return true;
        }

        #endregion

        #endregion

        #region Chat Menu Ore

        #region Chat Menu Ore Root

        private bool MenuPlanetOre(string breadcrumbs, string oreName, string planetName, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, oreName);

            var planet = Config.MyPlanetConfigurations.Get(planetName);
            var ore = planet.Get(oreName);

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("ore"), breadcrumbs, planetName)));

                menuText.AppendLine("");
                menuText.AppendLine(CHAT_MENU_HEADER_COMMANDS);
                //menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "show"));
                menuText.AppendLine(MenuLine(breadcrumbs, "ore_rarity"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size"));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Show
            if (IsMyMatch(Item("show"), message))
            {
                var settings = new Dictionary<string, string>
                {
                    { Item("ore_slot_count"), ore.Count.ToString() },
                    { Item("ore_rarity"), ore.Rarity.ToString(CultureInfo.CurrentCulture) },
                    { Item("ore_voxel_material_type"), ore.VoxelMaterialType },
                    { Item("ore_target_color"), ore.TargetColor },
                    { Item("ore_color_influence"), ore.ColorInfluence.ToString(CultureInfo.CurrentCulture) }
                };
                WriteToChat(DisplaySettings(breadcrumbs, settings));
                return true;
            }

            // Help
            // if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, CHAT_MENU_ORE_CONTENT_HELP);

            // Rarity
            if (IsMyMatch(Item("ore_rarity"), message))
            {
                var item = Item("ore_rarity");
                var usablePlanetOreSlots = planet.OreSlotCount - planet.StaticOres.Count;
                var prettySlots = $"planet's ore slots ({usablePlanetOreSlots})";
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (parsed.Item2 > usablePlanetOreSlots)
                {
                    WriteErrorToChat(Error("bounds_error_lesser_than"), item, prettySlots);
                    return false;
                }

                ore.Rarity = parsed.Item2;
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);
                return true;
            }


            // AutoDepth
            if (IsMyMatch(Item("depth"), message))
            {
                message = RegexTrim(Item("depth"), message);
                return MenuPlanetOreAutoDepth(breadcrumbs, oreName, planetName, message);
            }

            // AutoSize
            if (IsMyMatch(Item("size"), message))
            {
                message = RegexTrim(Item("size"), message);
                return MenuPlanetOreAutoSize(breadcrumbs, oreName, planetName, message);
            }

            if (IsMyMatch(Item("dump"), message))
            {
                WriteToChat(planet.DumpOreMappings(ore));
                return true;
            }

            return false;
        }

        #endregion

        #region Chat Menu Ore / AutoDepth

        private bool MenuPlanetOreAutoDepth(string breadcrumbs, string oreName, string planetName, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("depth"));

            var planet = Config.MyPlanetConfigurations.Get(planetName);
            var ore = planet.Get(oreName);

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("auto_depth"), breadcrumbs)));
                
                menuText.AppendLine("");
                menuText.AppendLine(CHAT_MENU_HEADER_COMMANDS);
                //menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "show"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_min"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_max"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_use_detector"));
                menuText.AppendLine(MenuLine(breadcrumbs, "reset"));

                menuText.AppendLine(CHAT_MENU_HEADER_ADVANCED);
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_use_progressive"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_curve"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_fuzz"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_detector_size"));
                menuText.AppendLine(MenuLine(breadcrumbs, "depth_detector_factor"));
                //menuText.AppendLine(MenuLine(breadcrumbs, "depth_use_custom_depth"));
                //menuText.AppendLine(MenuLine(breadcrumbs, "depth_custom_depth"));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            //if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("depth"));

            // Show
            if (IsMyMatch(Item("show"), message))
            {
                var settings = new Dictionary<string, string>
                {
                    { Item("depth_min"), ore.Depth.Min.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_max"), ore.Depth.Max.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_use_progressive"), ore.Depth.UseProgressive.ToString() },
                    { Item("depth_use_detector"), ore.Depth.UseDetector.ToString() },
                    { Item("depth_curve"), ore.Depth.Curve.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_fuzz"), ore.Depth.Fuzz.ToString(CultureInfo.CurrentCulture) },
                    { Item("depth_detector_size"), ore.Depth.DetectorSize.ToString() },
                    { Item("depth_detector_factor"), ore.Depth.DetectorFactor.ToString(CultureInfo.CurrentCulture) }
                };
                WriteToChat(DisplaySettings(breadcrumbs, settings));
                return true;
            }

            // Reset
            if (IsMyMatch(Item("reset"), message))
            {
                var item = Item("reset");
                ore.Depth.Reset();
                WriteConfirmationToChat("settings", item, "actions");

                ore.ToOreMappingMembers();
                Save(Config);
                return true;
            }

            // Depth Min
            if (IsMyMatch(Item("depth_min"), message))
            {
                var item = Item("depth_min");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (ore.Depth.Max < parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_lesser_than"), item, Item("depth_max"));
                    return false;
                }

                ore.Depth.SetMin(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Depth Max
            if (IsMyMatch(Item("depth_max"), message))
            {
                var item = Item("depth_max");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (ore.Depth.Min > parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_greater_than"), item, Item("depth_min"));
                    return false;
                }

                ore.Depth.SetMax(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Progressive Depth Enable/Disable
            if (IsMyMatch(Item("depth_use_progressive"), message))
            {
                var item = Item("depth_use_progressive");
                var parsed = ChatParseBool(item, message);
                if (!parsed.Item1) return false;

                ore.Depth.SetUseProgressive(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Use Detector
            if (IsMyMatch(Item("depth_use_detector"), message))
            {
                var item = Item("depth_use_detector");
                LogVar("item", item);
                LogVar("message", message);
                var parsed = ChatParseBool(item, message);
                if (!parsed.Item1) return false;

                ore.Depth.SetUseDetector(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Auto Depth Curve
            if (IsMyMatch(Item("depth_curve"), message))
            {
                var item = Item("depth_curve");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                ore.Depth.SetCurve(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Auto Depth Fuzz
            if (IsMyMatch(Item("depth_fuzz"), message))
            {
                var item = Item("depth_fuzz");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                ore.Depth.SetFuzz(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Detector Size
            if (IsMyMatch(Item("depth_detector_size"), message))
            {
                var item = Item("depth_detector_size");
                var parsed = ChatParseCubeSize(item, message);
                if (!parsed.Item1) return false;

                ore.Depth.SetDetectorSize(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Detector Factor
            if (IsMyMatch(Item("depth_detector_factor"), message))
            {
                var item = Item("depth_detector_factor");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                ore.Depth.SetDetectorFactor(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            return false;
        }

        #endregion

        #region Chat Menu Ore / AutoSize

        private bool MenuPlanetOreAutoSize(string breadcrumbs, string oreName, string planetName, string message)
        {
            breadcrumbs = BreadCrumb(breadcrumbs, Item("size"));

            var planet = Config.MyPlanetConfigurations.Get(planetName);
            var ore = planet.Get(oreName);

            // Default
            if (message.Length < 1)
            {
                var menuText = new StringBuilder();

                //menuText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("auto_depth"), breadcrumbs)));

                menuText.AppendLine("");
                menuText.AppendLine(CHAT_MENU_HEADER_COMMANDS);
                // menuText.AppendLine(MenuLine(breadcrumbs, "help"));
                menuText.AppendLine(MenuLine(breadcrumbs, "show"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size_min"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size_max"));
                menuText.AppendLine(MenuLine(breadcrumbs, "reset"));

                menuText.AppendLine(CHAT_MENU_HEADER_ADVANCED);
                menuText.AppendLine(MenuLine(breadcrumbs, "size_fuzz"));
                menuText.AppendLine(MenuLine(breadcrumbs, "size_factor"));
                //menuText.AppendLine(MenuLine(breadcrumbs, "size_use_custom_size"));
                //menuText.AppendLine(MenuLine(breadcrumbs, "size_custom_size"));

                WriteToChat(menuText.ToString());
                return true;
            }

            // Help
            // if (IsMyMatch(Item("help"), message)) return DisplayHelp(breadcrumbs, Help("size"));

            // Show
            if (IsMyMatch(Item("show"), message))
            {
                var settings = new Dictionary<string, string>
                {
                    { Item("size_min"), ore.Size.Min.ToString(CultureInfo.CurrentCulture) },
                    { Item("size_max"), ore.Size.Max.ToString(CultureInfo.CurrentCulture) },
                    { Item("size_factor"), ore.Size.Factor.ToString(CultureInfo.CurrentCulture) },
                    { Item("size_fuzz"), ore.Size.Fuzz.ToString(CultureInfo.CurrentCulture) }
                };
                WriteToChat(DisplaySettings(breadcrumbs, settings));
                return true;
            }

            // Reset
            if (IsMyMatch(Item("reset"), message))
            {
                var item = Item("reset");
                ore.Size.Reset();
                WriteConfirmationToChat("settings", item, "actions");

                ore.ToOreMappingMembers();
                Save(Config);
                return true;
            }

            // Size Min
            if (IsMyMatch(Item("size_min"), message))
            {
                var item = Item("size_min");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (ore.Size.Max < parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_lesser_than"), item, Item("size_max"));
                    return false;
                }

                ore.Size.SetMin(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Size Max
            if (IsMyMatch(Item("size_max"), message))
            {
                var item = Item("size_max");
                var parsed = ChatParsePositiveInt(item, message);
                if (!parsed.Item1) return false;

                if (ore.Size.Min > parsed.Item2)
                {
                    WriteErrorToChat(Error("bounds_error_greater_than"), item, Item("size_min"));
                    return false;
                }

                ore.Size.SetMax(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);
                return true;
            }

            // Auto Size Factor
            if (IsMyMatch(Item("size_factor"), message))
            {
                var item = Item("size_factor");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                ore.Size.SetFactor(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            // Auto Size Fuzz
            if (IsMyMatch(Item("size_fuzz"), message))
            {
                var item = Item("size_fuzz");
                var parsed = ChatParsePositiveFloat(item, message);
                if (!parsed.Item1) return false;

                ore.Size.SetFuzz(parsed.Item2);
                WriteConfirmationToChat(item, parsed.Item2);

                planet.GenerateMappings();
                Save(Config);

                return true;
            }

            return false;
        }

        #endregion

        #endregion

        #region Chat Menuing Helpers

        private static string BreadCrumb(string crumbs = "", string newCrumb = "")
        {
            Log($"In BreadCrumb({crumbs})");
            return crumbs.Length > 0 ? $"{crumbs} {newCrumb}" : newCrumb;
        }

        private static string DisplaySettings(string breadcrumbs, Dictionary<string, string> settings)
        {
            var showText = new StringBuilder();
            
            showText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("show"), breadcrumbs)));

            foreach (var setting in settings)
            {
                showText.AppendLine(Format(CHAT_TEMPLATE_VAR, setting.Key, setting.Value));
            }

            //WriteToChat(showText.ToString());
            return showText.ToString();
        }

        private static bool DisplayHelp(string breadcrumbs, string help)
        {
            var helpText = new StringBuilder();

            helpText.AppendLine(Format(CHAT_MENU_HEADER_TOP, Format(Hint("help"), breadcrumbs)));
            helpText.AppendLine(Format(help, breadcrumbs));

            WriteToChat(helpText.ToString());
            return true;
        }

        private static List<string> GetFormattedOresAssignmentList(MyPlanetConfiguration myPlanetConfig,
            bool templated = false)
        {
            /*
             * Filter out static voxel materials so we don't mess with the weird
             * stuff some Planets do with the oremappings, for example Mars's "Ice" VoxelMaterial
             */

            var tmpList = new List<string>();

            switch (templated)
            {
                case true:
                    tmpList = myPlanetConfig.GetNonStaticVoxelOreAssignments()
                        .Select(a => Format(TEMPLATE_ORE_ASSIGNMENTS, a.VoxelMinedOre, a.Rarity))
                        .ToList();
                    break;
                case false:
                    tmpList = myPlanetConfig.GetNonStaticVoxelOreAssignments()
                        .Select(a => a.VoxelMinedOre)
                        .ToList();
                    break;
            }

            return tmpList;
        }

        /*private List<string> GetFilteredVoxelsToOreNames()
        {
            return (from voxelMaterial in VoxelMaterialToOre
                where voxelMaterial.Value != "Stone"
                      || voxelMaterial.Key == "Stone_01"
                where voxelMaterial.Value != "Iron"
                      || voxelMaterial.Key == "Iron_02"
                where !voxelMaterial.Value.Contains("Ice")
                      || voxelMaterial.Key == "Ice_01"
                //|| (voxelMaterial.Key == "Ice_01" || voxelMaterial.Key == "Ice")
                select voxelMaterial.Value).ToList();
        }*/

        private static MyTuple<bool, bool> ChatParseBool(string command, string message)
        {
            message = RegexTrim(command, message);
            if (message.Length < 1) return new MyTuple<bool, bool>(false, false);

            bool tmp;

            if (bool.TryParse(ClarifyBoolString(message), out tmp)) return new MyTuple<bool, bool>(true, tmp);

            Log($"In ChatParseBool(string \"{command}\", string \"{message})\"");
            LogVar("Error(\"not_boolean\")", Error("not_boolean"));
            LogVar("DEFAULT_BOOLEAN_SYNONYMS", NiceList(DEFAULT_BOOLEAN_SYNONYMS.Keys.ToList(), true));

            WriteErrorToChat(Error("not_boolean"), command, NiceList(DEFAULT_BOOLEAN_SYNONYMS.Keys.ToList(), true));
            return new MyTuple<bool, bool>(false, false);
        }

        private static MyTuple<bool, float> ChatParsePositiveFloat(string command, string message)
        {
            message = RegexTrim(command, message);
            if (message.Length < 1) return new MyTuple<bool, float>(false, 0);

            float tmp;

            if (!float.TryParse(message, out tmp))
            {
                WriteErrorToChat(Error("not_positive"), command);
                return new MyTuple<bool, float>(false, 0);
            }

            if (!(tmp < 0)) return new MyTuple<bool, float>(true, tmp);

            WriteErrorToChat(Error("not_positive"), command);
            return new MyTuple<bool, float>(false, 0);
        }

        private static MyTuple<bool, int> ChatParsePositiveInt(string command, string message)
        {
            message = RegexTrim(command, message);
            if (message.Length < 1) return new MyTuple<bool, int>(false, 0);

            int tmp;

            if (!int.TryParse(message, out tmp))
            {
                WriteErrorToChat(Error("not_positive"), command);
                return new MyTuple<bool, int>(false, 0);
            }

            if (tmp >= 0) return new MyTuple<bool, int>(true, tmp);

            WriteErrorToChat(Error("not_positive"), command);
            return new MyTuple<bool, int>(false, 0);
        }

        private static MyTuple<bool, MyCubeSize> ChatParseCubeSize(string command, string message)
        {
            message = RegexTrim(command, message);
            if (message.Length < 1) return new MyTuple<bool, MyCubeSize>(false, Large);

            MyCubeSize tmp;

            if (MyCubeSize.TryParse(message, out tmp)) return new MyTuple<bool, MyCubeSize>(true, tmp);

            WriteErrorToChat(Error("not_cubesize"), command);
            return new MyTuple<bool, MyCubeSize>(false, tmp);
        }

        private static string NiceList(List<string> list, bool delimited = false,
            string delimiter = COMMA_SEP)
            => Helpers.NiceList(list, delimited, delimiter);

        private static void WriteListToChat(List<string> list, bool delimited = false,
            string delimiter = COMMA_SEP)
            => Helpers.WriteListToChat(list, delimited, delimiter);

        private static void WriteToChat(string message, string flag = null)
        {
            Helpers.WriteToChat(message, flag);
            Helpers.Log(LOG_PREFIX_SESSION, message);
        }

        private static void WriteErrorToChat(string template, string command, string more = "") =>
            Helpers.WriteErrorToChat(template, command, more);

        #endregion

        #endregion

        #region Single Player / Multiplayer / Dedicated Server identifiers

        // Thanks PatrickQ !

        /// <summary>
        /// True when this machine is a dedicated server.
        /// </summary>
        public bool IsDedicatedServer =>
            // if False, game is Single-player
            MyAPIGateway.Multiplayer.MultiplayerActive
            // if False, is client
            && MyAPIGateway.Multiplayer.IsServer
            // if True, is DS
            && MyAPIGateway.Utilities.IsDedicated;

        /// <summary>
        /// True when this machine is hosting a game as well as playing.
        /// </summary>
        public bool IsMultiplayerHost =>
            MyAPIGateway.Multiplayer.MultiplayerActive
            && MyAPIGateway.Multiplayer.IsServer
            && !MyAPIGateway.Utilities.IsDedicated;

        /// <summary>
        /// True when this machine is a multiplayer client.
        /// </summary>
        public bool IsMultiplayerClient =>
            // if False, game is Single-player
            MyAPIGateway.Multiplayer.MultiplayerActive
            // if False, is client
            && !MyAPIGateway.Multiplayer.IsServer;

        /// <summary>
        /// True when this machine has player clients connected.
        /// </summary>
        public bool HasConnectedPlayers =>
            MyAPIGateway.Multiplayer.MultiplayerActive
            && MyAPIGateway.Multiplayer.Players?.Count > 0;

        /// <summary>
        /// True when this machine is running in single-player.
        /// </summary>
        public bool IsSinglePlayer =>
            // if False, game is Single-player
            !MyAPIGateway.Multiplayer.MultiplayerActive;

        #endregion

        #region Menu Strings Methods

        private static string Error(string topic) => MenuStrings.Error(topic);
        private static string Warning(string topic) => MenuStrings.Warning(topic);
        private static string Item(string topic) => MenuStrings.Item(topic);
        private static string Hint(string topic) => MenuStrings.Hint(topic);
        private static string Help(string topic) => MenuStrings.Help(topic);
        private static string Result(string topic) => MenuStrings.Result(topic);

        #endregion

        #region Local Logging

        private static void LogBegin<T>(T message, bool isServer = false) =>
            Helpers.LogBegin(LOG_PREFIX_SESSION, message, isServer);

        private static void LogEnd<T>(T message, bool isServer = false) =>
            Helpers.LogEnd(LOG_PREFIX_SESSION, message, isServer);

        private static void LogTry<T>(T message, bool isServer = false) =>
            Helpers.LogTry(LOG_PREFIX_SESSION, message, isServer);

        private static void LogFail<T>(T message, bool isServer = false) =>
            Helpers.LogFail(LOG_PREFIX_SESSION, message, isServer);

        private static void LogWarn<T>(T message, bool isServer = false) =>
            Helpers.LogWarn(LOG_PREFIX_SESSION, message, isServer);

        private static void LogVar<T>(string name, T varT, bool isServer = false) =>
            Helpers.LogVar(LOG_PREFIX_SESSION, name, varT, isServer);

        private static void Log<T>(T message, bool showHeader = false, bool isServer = false) =>
            Helpers.Log(LOG_PREFIX_SESSION, message, showHeader, isServer);

        #endregion
    }
}