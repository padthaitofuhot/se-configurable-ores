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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EmptyKeys.UserInterface.Generated.EditFactionIconView_Gamepad_Bindings;
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
using VRage.Game.VisualScripting.Utils;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;
using static ConfigurableOres.Helpers;
using static ConfigurableOres.Strings;

// ReSharper disable InconsistentNaming

namespace ConfigurableOres
{
    /*  
     *  Thanks Patrick!
     */
    [Serializable]
    public class Configuration
    {
        #region World Configuration

        // System
        public bool Logging;
        //public string CommandPrefix = "";
        public bool DisableChatCommands;

        // OreAutoDepth
        /*public int WorldDepthMin;
        public int WorldDepthMax;
        public float WorldAutoDepthCurve;
        public float WorldAutoDepthFuzz;
        public bool WorldAutoDepthProgressive;
        public bool WorldUseDetector;
        public MyCubeSize WorldUseDetectorSize;
        public float WorldUseDetectorFactor;*/

        public MyDepthSettings Depth = new MyDepthSettings();

        // OreAutoSize
        /*public int WorldSizeMin;
        public int WorldSizeMax;
        public float WorldAutoSizeFactor;
        public float WorldAutoSizeFuzz;*/

        public MySizeSettings Size = new MySizeSettings();

        // Data
        public List<string> StaticVoxelMaterials;
        public List<string> NeverStaticVoxelMaterials;
        public List<string> IgnoredPlanets;

        public List<string> IgnoredOres;

        //public List<string> OreElementSymbols;
        public MyPlanetConfigurationCollection MyPlanetConfigurations = new MyPlanetConfigurationCollection();

        #endregion

        #region Persistence Methods

        private Configuration()
        {
            // Parameterless, empty constructor for deserialization
        }

        // Older calls to this do their own conditional logic, but should be refactored because the conditions they use can be condensed.
        public static void Save(Configuration configuration, bool isHost = true)
        {
            if (isHost)
            {
                SaveOnHost(configuration);
            }
        }
        
        public static void SaveOnHost(Configuration configuration)
        {
            LogBegin(LOG_SAVE_CONFIGURATION);

            var configAsXML = SerializeConfig(configuration);

            /*  
            * Save to world.sbc for multiplayer/dedicated server clients.
            * Save to external config file for manual editing.
            */
            MyAPIGateway.Utilities.SetVariable<string>(CONFIG_FILE_NAME, Base64Encode(configAsXML));

            try
            {
                LogTry(LOG_WRITE_CONFIGURATION);
                using (var writer =
                       MyAPIGateway.Utilities.WriteFileInWorldStorage(CONFIG_FILE_NAME, typeof(Configuration)))
                {
                    writer.Write(configAsXML);
                    LogEnd(LOG_WRITE_CONFIGURATION);
                }
            }

            catch (Exception e)
            {
                LogFail(LOG_WRITE_CONFIGURATION);
                Log(e.StackTrace);

                // Leave a big error in log and crash if config cannot be loaded.
                // todo: spam notification about bad config and disable mod functionality.
                if (ABEND_ON_FAULT)
                {
                    Log(configAsXML);
                    throw;
                }
            }
        }

        public static Configuration Load(bool isHost = true)
        {
            return isHost ? LoadOnHost() : LoadOnClient();
        }

        // Hosts should only load config from config file, never world var.
        private static Configuration LoadOnHost()
        {
            LOGGING_ENABLED = true;
            LogBegin(LOG_LOAD_CONFIGURATION);

            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE_NAME, typeof(Configuration)))
            {
                var configAsXML = "";
                try
                {
                    LogTry(LOG_READ_CONFIGURATION);
                    using (var reader =
                           MyAPIGateway.Utilities.ReadFileInWorldStorage(CONFIG_FILE_NAME, typeof(Configuration)))
                    {
                        configAsXML = reader.ReadToEnd();
                    }

                    LogEnd(LOG_READ_CONFIGURATION);
                }
                catch (Exception e)
                {
                    // Dump a turd in the log and crash if config cannot be read.
                    // todo: spam notification about bad config and disable mod functionality.

                    LogFail(LOG_READ_CONFIGURATION);
                    Log(e.Message);
                    Log(e.StackTrace);
                    if (ABEND_ON_FAULT) throw;
                }

                LogEnd(LOG_LOAD_CONFIGURATION);
                return ConfigFromXML(configAsXML);
            }

            // Make default config.
            Log("No config present, creating default config.");
            var newConfiguration = new Configuration();
            newConfiguration.SetDefaults();
            return newConfiguration;
        }

        private static Configuration LoadOnClient()
        {
            LOGGING_ENABLED = true;
            Log(LOG_LOAD_CONFIGURATION);

            var configAsB64 = "";

            LogBegin("Load config from world.sbc");
            if (!MyAPIGateway.Utilities.GetVariable<string>(CONFIG_FILE_NAME, out configAsB64))
            {
                LogFail(LOG_WORLD_VAR_NOT_FOUND);
                LogFail(LOG_READ_CONFIGURATION);
                LogFail(LOG_LOAD_CONFIGURATION);
                throw new Exception(LOG_WORLD_VAR_NOT_FOUND);
            }

            LogEnd("Load config from world.sbc");

            var loadedConfig = ConfigFromXML(Base64Decode(configAsB64));

            LogEnd(LOG_LOAD_CONFIGURATION);
            return loadedConfig;
        }

        private static Configuration ConfigFromXML(string configAsXML)
        {
            try
            {
                LogTry(LOG_FROM_XML_CONFIGURATION);

                var deserializedConfig = DeserializeConfig(configAsXML);

                // sanity checks
                var sanity = deserializedConfig.CheckSanity();
                if (deserializedConfig == null || !sanity.Item1)
                {
                    Log($"Load fail reason: {sanity.Item2}");
                    throw new Exception(LOG_CONFIGURATION_INVALID);
                }

                Log(deserializedConfig.ToString(), isServer: true);

                LogEnd(LOG_FROM_XML_CONFIGURATION);
                return deserializedConfig;
            }
            catch (Exception e)
            {
                LogFail(LOG_FROM_XML_CONFIGURATION);
                Log(e.Message);
                Log(e.StackTrace);

                // Leave a big dookie in the log and crash if config cannot be loaded.
                // todo: spam notification about bad config and disable mod functionality.
                if (ABEND_ON_FAULT) throw;
            }
        }

        /// <summary>
        /// Indicates if the config object has usable values for
        /// all data. Will perform some correction for some invalid data.
        /// </summary>
        private MyTuple<bool, string> CheckSanity()
        {
            LogBegin(LOG_SANITY_CHECK_SETTINGS);

            var isValid = true;
            var reason = "";

            if (!isValid) return new MyTuple<bool, string>(isValid, "CommandPrefix IsNullOrWhiteSpace");

            isValid &= (Depth.Fuzz > 0);
            if (!isValid) return new MyTuple<bool, string>(isValid, "Depth.Fuzz > 0");

            isValid &= (Depth.Curve > 0);
            if (!isValid) return new MyTuple<bool, string>(isValid, "Depth.Curve > 0");

            isValid &= (Depth.DetectorFactor > 0);
            if (!isValid) return new MyTuple<bool, string>(isValid, "Depth.DetectorFactor > 0");

            isValid &= (Size.Factor > 0);
            if (!isValid) return new MyTuple<bool, string>(isValid, "Size.Factor > 0");

            isValid &= (Size.Fuzz > 0);
            if (!isValid) return new MyTuple<bool, string>(isValid, "Size.Fuzz > 0");

            isValid &= (Depth.DetectorSize == MyCubeSize.Large)
                       || (Depth.DetectorSize == MyCubeSize.Small);
            if (!isValid) return new MyTuple<bool, string>(isValid, "Depth.DetectorSize is not valid CubeSize");


            // can't sanity check this because SE prohibits .Count on List<byte> for some reason???
            // todo: work around?
            //isValid &= MyPlanetConfigurations.Count() > 0;
            //isValid &= StaticVoxelMaterials.Any();

            foreach (var memberPlanet in MyPlanetConfigurations.MemberPlanets)
            {
                Log($"Check config of planet: {memberPlanet.Name}");
                
                // Planet Name must not be Null or Whitespace
                isValid &= !string.IsNullOrWhiteSpace(memberPlanet.Name);
                if (!isValid)
                    return new MyTuple<bool, string>(isValid,
                        $"{memberPlanet.Name}: !string.IsNullOrWhiteSpace(memberPlanet.Name)");
                
                var planetConfig = memberPlanet.PlanetConfig;
                
                // OreSlotCount must be Zero or == Total ore slots - Static voxel ore slots
                LogVar("OreSlotCount", planetConfig.OreSlotCount);
                if (planetConfig.OreSlotCount == 0)
                {
                    Log("OreSlotCount == 0, skipping further checks");
                    continue;
                }

                var totalAO = planetConfig.AssignedOres.Sum(a => a.Count);
                LogVar("Total ore slots", totalAO);
                
                var staticAO = planetConfig.AssignedOres.Where(a => a.HasStaticVoxelMaterial)
                    .Sum(b => b.BlueValues.Count);
                LogVar("Static voxel ore slots", staticAO);

                isValid &= planetConfig.OreSlotCount == totalAO - staticAO;
                
                if (!isValid)
                {
                    return new MyTuple<bool, string>(isValid, $"{memberPlanet.Name}: OreSlotCount must be Zero ({planetConfig.OreSlotCount}) or == Total ore slots ({totalAO}) - Static voxel ore slots ({staticAO}).");
                }

                // AssignableOresBlueChannelValues list must == All blue values except Static voxel blue values
                isValid &= planetConfig.AssignableOresBlueChannelValues.Count ==
                           planetConfig.AssignedOres
                               .Where(a => a.HasStaticVoxelMaterial == false)
                               .Sum(b => b.BlueValues.Count);

                if (!isValid)
                    return new MyTuple<bool, string>(isValid,
                        $"{memberPlanet.Name}: AssignableOresBlueChannelValues list must be 0 or == All blue values except Static voxel blue values. Was: {planetConfig.AssignableOresBlueChannelValues.Count}");
            }

            LogEnd(LOG_SANITY_CHECK_SETTINGS);
            return new MyTuple<bool, string>(isValid, "Config is sane");
        }

        private void SetDefaults()
        {
            LogBegin(LOG_CREATE_DEFAULT_CONFIGURATION);

            DisableChatCommands = false;

            // TODO: set this to false before release!!!
            Logging = DEFAULT_LOGGING_ENABLED;

            Depth.Reset();
            Size.Reset();
            StaticVoxelMaterials = DEFAULT_OREMAP_STATIC_VOXEL_MATERIALS;
            NeverStaticVoxelMaterials = DEFAULT_OREMAP_NEVER_STATIC_VOXEL_MATERIALS;
            IgnoredPlanets = DEFAULT_IGNORED_PLANETS;
            IgnoredOres = DEFAULT_IGNORED_ORES;
            //OreElementSymbols = DEFAULT_ORE_ELEMENT_SYMBOLS;

            LogEnd(LOG_CREATE_DEFAULT_CONFIGURATION);
        }

        public static string SerializeConfig(Configuration configuration)
        {
            Log(LOG_SERIALIZE_CONFIGURATION);
            return MyAPIGateway.Utilities.SerializeToXML(configuration);
        }

        public static Configuration DeserializeConfig(string configuration)
        {
            Log(LOG_DESERIALIZE_CONFIGURATION);
            return MyAPIGateway.Utilities.SerializeFromXML<Configuration>(configuration);
        }
        
        #endregion
        
        #region Menu Strings Methods

        private static void Error(string topic) => MenuStrings.Error(topic);
        private static void Warning(string topic) => MenuStrings.Warning(topic);
        private static void Item(string topic) => MenuStrings.Item(topic);
        private static void Hint(string topic) => MenuStrings.Hint(topic);
        private static void Help(string topic) => MenuStrings.Help(topic);
        private static void Result(string topic) => MenuStrings.Result(topic);

        #endregion

        #region Logging

        private static void LogBegin<T>(T message, bool isServer = false) =>
            Helpers.LogBegin(LOG_PREFIX_CONFIGURATION, message, isServer);

        private static void LogEnd<T>(T message, bool isServer = false) =>
            Helpers.LogEnd(LOG_PREFIX_CONFIGURATION, message, isServer);

        private static void LogTry<T>(T message, bool isServer = false) =>
            Helpers.LogTry(LOG_PREFIX_CONFIGURATION, message, isServer);

        private static void LogFail<T>(T message, bool isServer = false) =>
            Helpers.LogFail(LOG_PREFIX_CONFIGURATION, message, isServer);

        private void LogWarn<T>(T message, bool isServer = false) =>
            Helpers.LogWarn(LOG_PREFIX_CONFIGURATION, message, isServer);

        private void LogVar<T>(string name, T varT, bool isServer = false) =>
            Helpers.LogVar(LOG_PREFIX_CONFIGURATION, name, varT, isServer);

        private static void LogFlag<T>(T message, string flag, bool showHeader = false, bool isServer = false) =>
            Helpers.LogFlag(LOG_PREFIX_CONFIGURATION, message, flag, showHeader, isServer);

        private static void Log<T>(T message, bool showHeader = false, bool isServer = false) =>
            Helpers.Log(LOG_PREFIX_CONFIGURATION, message, showHeader, isServer);

        #endregion
    }

    #region Settings Classes

    [Serializable]
    public class MyDepthSettings
    {
        public float Min;
        public float Max;
        public bool UseProgressive;
        public bool UseDetector;
        public float Curve;
        public float Fuzz;
        public MyCubeSize DetectorSize;

        public float DetectorFactor;

        //public bool UseCustomDepth;
        //public float CustomDepth;
        private MyDepthSettings Parent;
        private List<MyDepthSettings> Children = new List<MyDepthSettings>();

        public MyDepthSettings(
            float min = DEFAULT_ORE_DEPTH_MIN,
            float max = DEFAULT_ORE_DEPTH_MAX,
            bool useProgressive = DEFAULT_ORE_AUTO_DEPTH_PROGRESSIVE,
            bool useDetector = DEFAULT_ORE_USE_DETECTOR_STATE,
            float curve = DEFAULT_ORE_AUTO_DEPTH_CURVE_EXPONENT,
            float fuzz = DEFAULT_ORE_AUTO_DEPTH_FUZZ,
            MyCubeSize detectorSize = DEFAULT_ORE_USE_DETECTOR_CUBESIZE,
            float detectorFactor = DEFAULT_ORE_USE_DETECTOR_FACTOR
            //bool useCustomDepth = DEFAULT_ORE_CUSTOM_DEPTH_STATE,
            //float customDepth = DEFAULT_ORE_CUSTOM_DEPTH
        )
        {
            Min = min;
            Max = max;
            UseProgressive = useProgressive;
            UseDetector = useDetector;
            Curve = curve;
            Fuzz = fuzz;
            DetectorSize = detectorSize;
            DetectorFactor = detectorFactor;
            //UseCustomDepth = useCustomDepth;
            //CustomDepth = customDepth;
        }

        public MyDepthSettings()
        {
        }

        public MyDepthSettings Clone()
        {
            var clone = (MyDepthSettings)MemberwiseClone();
            clone.Children = new List<MyDepthSettings>();
            clone.SetRelationship(this);
            return clone;
        }

        /// <summary>
        /// To establish the world -> planet -> ore hierarchy of configuration influence,
        /// we link the depth settings in a parent->child relationship.
        /// Since the serializer does not know how to do that, we have to do it ourselves.
        /// 
        /// This method takes the parent MyDepthSettings object as the parameter.
        /// It sets itself as a child on the parent using .AddChild(this).
        ///
        /// If we have no parent here, then we must be at the top level of the hierarchy.
        /// </summary>
        /// <param name="parent">parent MyDepthSettings object</param>
        public void SetRelationship(MyDepthSettings parent = null)
        {
            LogBegin($"MyDepthSettings SetRelationship()");
            if (parent == null)
            {
                Log("Top level, no parent.");
                Log("Parent == null");
                LogEnd($"MyDepthSettings SetRelationship()");
                return;
            }

            Parent = parent;
            Log("child calling .AddChild");
            Parent.AddChild(this);
            LogEnd($"MyDepthSettings SetRelationship()");
        }

        public void AddChild(MyDepthSettings child)
        {
            LogBegin($"Adding child...");
            Children.Add(child);
            LogEnd($"Child added");
        }

        public void Reset(MyDepthSettings parent = null)
        {
            if (parent != null) Parent = parent;

            if (Parent == null)
            {
                ResetToDefaults();
                return;
            }

            Min = Parent.Min;
            Max = Parent.Max;
            UseProgressive = Parent.UseProgressive;
            UseDetector = Parent.UseDetector;
            Curve = Parent.Curve;
            Fuzz = Parent.Fuzz;
            DetectorSize = Parent.DetectorSize;
            DetectorFactor = Parent.DetectorFactor;
            //UseCustomDepth = Parent.UseCustomDepth;
            //CustomDepth = Parent.CustomDepth;
        }

        public void ResetToDefaults()
        {
            Min = DEFAULT_ORE_DEPTH_MIN;
            Max = DEFAULT_ORE_DEPTH_MAX;
            UseProgressive = DEFAULT_ORE_AUTO_DEPTH_PROGRESSIVE;
            UseDetector = DEFAULT_ORE_USE_DETECTOR_STATE;
            Curve = DEFAULT_ORE_AUTO_DEPTH_CURVE_EXPONENT;
            Fuzz = DEFAULT_ORE_AUTO_DEPTH_FUZZ;
            DetectorSize = DEFAULT_ORE_USE_DETECTOR_CUBESIZE;
            DetectorFactor = DEFAULT_ORE_USE_DETECTOR_FACTOR;
            //UseCustomDepth = DEFAULT_ORE_CUSTOM_DEPTH_STATE;
            //CustomDepth = DEFAULT_ORE_CUSTOM_DEPTH;
        }

        public void SetMin(float min)
        {
            Min = min;
            Children.ForEach(c => c.SetMin(min));
        }

        public void SetMax(float max)
        {
            Max = max;
            Children.ForEach(c => c.SetMax(max));
        }

        public void SetUseProgressive(bool useProgressive)
        {
            Log($"in SetUseProgressive({useProgressive})");
            UseProgressive = useProgressive;
            Log($"Children = {Children.Count}");
            Children.ForEach(c => c.SetUseProgressive(useProgressive));
        }

        public void SetUseDetector(bool useDetector)
        {
            UseDetector = useDetector;
            Children.ForEach(c => c.SetUseDetector(useDetector));
            Children.ForEach(c => c.SetMax(Max));
        }

        public void SetCurve(float curve)
        {
            Curve = curve;
            Children.ForEach(c => c.SetCurve(curve));
        }

        public void SetFuzz(float fuzz)
        {
            Fuzz = fuzz;
            Children.ForEach(c => c.SetFuzz(fuzz));
        }

        public void SetDetectorFactor(float detectorFactor)
        {
            DetectorFactor = detectorFactor;
            Children.ForEach(c => c.SetDetectorFactor(detectorFactor));
        }

        public void SetDetectorSize(MyCubeSize detectorSize)
        {
            DetectorSize = detectorSize;
            Children.ForEach(c => c.SetDetectorSize(detectorSize));
        }

        /*public void SetUseCustomDepth(bool useCustomDepth)
        {
            UseCustomDepth = useCustomDepth;
            Children.ForEach(c => c.SetUseCustomDepth(useCustomDepth));
        }

        public void SetCustomDepth(float customDepth)
        {
            CustomDepth = customDepth;
            Children.ForEach(c => c.SetCustomDepth(customDepth));
        }*/

        #region Logging

        private static string LOG_PREFIX = "DEPTH_SETTINGS";

        private static void LogBegin<T>(T message, bool isServer = false) =>
            Helpers.LogBegin(LOG_PREFIX, message, isServer);

        private static void LogEnd<T>(T message, bool isServer = false) =>
            Helpers.LogEnd(LOG_PREFIX, message, isServer);

        private static void LogTry<T>(T message, bool isServer = false) =>
            Helpers.LogTry(LOG_PREFIX, message, isServer);

        private static void LogFail<T>(T message, bool isServer = false) =>
            Helpers.LogFail(LOG_PREFIX, message, isServer);

        private void LogWarn<T>(T message, bool isServer = false) =>
            Helpers.LogWarn(LOG_PREFIX, message, isServer);

        private void LogVar<T>(string name, T varT, bool isServer = false) =>
            Helpers.LogVar(LOG_PREFIX, name, varT, isServer);

        private static void LogFlag<T>(T message, string flag, bool showHeader = false, bool isServer = false) =>
            Helpers.LogFlag(LOG_PREFIX, message, flag, showHeader, isServer);

        private static void Log<T>(T message, bool showHeader = false, bool isServer = false) =>
            Helpers.Log(LOG_PREFIX, message, showHeader, isServer);

        #endregion
    }

    [Serializable]
    public class MySizeSettings
    {
        public float Min;
        public float Max;
        public float Factor;

        public float Fuzz;

        //public bool UseCustomSize;
        //public float CustomSize;
        private MySizeSettings Parent;
        private List<MySizeSettings> Children = new List<MySizeSettings>();

        public MySizeSettings(
            float min = DEFAULT_ORE_SIZE_MIN,
            float max = DEFAULT_ORE_SIZE_MAX,
            float factor = DEFAULT_ORE_AUTO_SIZE_FACTOR,
            float fuzz = DEFAULT_ORE_AUTO_SIZE_FUZZ
            //bool useCustomSize = DEFAULT_ORE_CUSTOM_SIZE_STATE,
            //float customSize = DEFAULT_ORE_CUSTOM_SIZE
        )
        {
            Min = min;
            Max = max;
            Factor = factor;
            Fuzz = fuzz;
            //UseCustomSize = useCustomSize;
            //CustomSize = customSize;
        }

        public MySizeSettings()
        {
        }

        public MySizeSettings Clone()
        {
            var clone = (MySizeSettings)MemberwiseClone();
            clone.Children = new List<MySizeSettings>();
            clone.SetRelationship(this);
            return clone;
        }

        public void SetRelationship(MySizeSettings parent = null)
        {
            if (parent == null) return;

            Parent = parent;
            Parent.AddChild(this);
        }

        public void AddChild(MySizeSettings child)
        {
            Children.Add(child);
        }

        public void Reset(MySizeSettings parent = null)
        {
            if (parent != null) Parent = parent;

            if (Parent == null)
            {
                ResetToDefaults();
                return;
            }

            Min = Parent.Min;
            Max = Parent.Max;
            Factor = Parent.Factor;
            Fuzz = Parent.Fuzz;
            //UseCustomSize = Parent.UseCustomSize;
            //CustomSize = Parent.CustomSize;
        }

        public void ResetToDefaults()
        {
            Min = DEFAULT_ORE_SIZE_MIN;
            Max = DEFAULT_ORE_SIZE_MAX;
            Factor = DEFAULT_ORE_AUTO_SIZE_FACTOR;
            Fuzz = DEFAULT_ORE_AUTO_SIZE_FUZZ;
            //UseCustomSize = DEFAULT_ORE_CUSTOM_SIZE_STATE;
            //CustomSize = DEFAULT_ORE_CUSTOM_SIZE;
        }

        public void SetMin(float min)
        {
            Min = min;
            Children.ForEach(c => c.SetMin(min));
        }

        public void SetMax(float max)
        {
            Max = max;
            Children.ForEach(c => c.SetMax(max));
        }

        public void SetFactor(float factor)
        {
            Factor = factor;
            Children.ForEach(c => c.SetFactor(factor));
        }

        public void SetFuzz(float fuzz)
        {
            Fuzz = fuzz;
            Children.ForEach(c => c.SetFuzz(fuzz));
        }

        /*public void SetUseCustomSize(bool useCustomSize)
        {
            UseCustomSize = useCustomSize;
            Children.ForEach(c => c.SetUseCustomSize(useCustomSize));
        }

        public void SetCustomSize(float customSize)
        {
            CustomSize = customSize;
            Children.ForEach(c => c.SetCustomSize(customSize));
        }*/

        #region Logging

        private static string LOG_PREFIX = "SIZE_SETTINGS";

        private static void LogBegin<T>(T message, bool isServer = false) =>
            Helpers.LogBegin(LOG_PREFIX, message, isServer);

        private static void LogEnd<T>(T message, bool isServer = false) =>
            Helpers.LogEnd(LOG_PREFIX, message, isServer);

        private static void LogTry<T>(T message, bool isServer = false) =>
            Helpers.LogTry(LOG_PREFIX, message, isServer);

        private static void LogFail<T>(T message, bool isServer = false) =>
            Helpers.LogFail(LOG_PREFIX, message, isServer);

        private void LogWarn<T>(T message, bool isServer = false) =>
            Helpers.LogWarn(LOG_PREFIX, message, isServer);

        private void LogVar<T>(string name, T varT, bool isServer = false) =>
            Helpers.LogVar(LOG_PREFIX, name, varT, isServer);

        private static void LogFlag<T>(T message, string flag, bool showHeader = false, bool isServer = false) =>
            Helpers.LogFlag(LOG_PREFIX, message, flag, showHeader, isServer);

        private static void Log<T>(T message, bool showHeader = false, bool isServer = false) =>
            Helpers.Log(LOG_PREFIX, message, showHeader, isServer);

        #endregion
    }

    #endregion
}