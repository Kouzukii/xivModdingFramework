﻿// xivModdingFramework
// Copyright © 2018 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using Newtonsoft.Json;
using SharpDX.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Resources;
using xivModdingFramework.SqPack.FileTypes;

namespace xivModdingFramework.Mods
{
    /// <summary>
    /// This class contains the methods that deal with the .modlist file
    /// </summary>
    public class Modding
    {
        private readonly string _modSource;
        private readonly Version _modlistVersion = new Version(1, 0);
        private readonly JsonSerializer _serializer = new JsonSerializer();
        private readonly DirectoryInfo _modListDirectory;
        private ModList _modList;

        public DirectoryInfo GameDirectory { get; }

        public DirectoryInfo ModPackDirectory { get; }

        public Index Index { get; }

        public ProblemChecker ProblemChecker { get; }

        public Dat Dat { get; }

        /// <summary>
        /// Sets the modlist with a provided name
        /// </summary>
        /// <param name="modlistDirectory">The directory in which to place the Modlist</param>
        /// <param name="modListName">The name to give the modlist file</param>
        public Modding(DirectoryInfo gameDirectory, DirectoryInfo modPackDirectory, string modSource)
        {
            _modSource = modSource;
            GameDirectory = gameDirectory;
            ModPackDirectory = modPackDirectory;
            Index = new Index(GameDirectory);
            ProblemChecker = new ProblemChecker(this);
            Dat = new Dat(this);
            _modListDirectory = new DirectoryInfo(Path.Combine(GameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));

        }

        public async Task DeleteAllFilesAddedByTexTools() 
        {
            var modList = GetModList();
            var modsToRemove = modList.Mods.Where(it => it.source == "FilesAddedByTexTools");
            foreach (var mod in modsToRemove) 
            {
                await DeleteMod(mod.fullPath);
            }
        }

        /// <summary>
        /// Creates the Mod List that is used to keep track of mods.
        /// </summary>
        public void CreateModlist()
        {
            if (File.Exists(_modListDirectory.FullName))
            {
                return;
            }

            var modList = new ModList
            {
                version = _modlistVersion.ToString(),
                modCount = 0,
                modPackCount = 0,
                emptyCount = 0,
                ModPacks = new List<ModPack>(),
                Mods = new List<Mod>()
            };

            WriteModList(modList);
        }

        public void DeleteModlist() 
        {
            _modList = null;
            File.Delete(_modListDirectory.FullName);
        }

        /// <summary>
        /// Tries to get the mod entry for the given internal file path, return null otherwise
        /// </summary>
        /// <param name="internalFilePath">The internal file path to find</param>
        /// <returns>The mod entry if found, null otherwise</returns>
        public Task<Mod> TryGetModEntry(string internalFilePath)
        {
            return Task.Run(() =>
            {
                internalFilePath = internalFilePath.Replace("\\", "/");

                var modList = GetModList();

                if (modList == null) return null;

                foreach (var modEntry in modList.Mods)
                {
                    if (modEntry.fullPath.Equals(internalFilePath))
                    {
                        return modEntry;
                    }
                }

                return null;
            });
        }

        /// <summary>
        /// Checks to see whether the mod is currently enabled
        /// </summary>
        /// <param name="internalPath">The internal path of the file</param>
        /// <param name="dataFile">The data file to check in</param>
        /// <param name="indexCheck">Flag to determine whether to check the index file or just the modlist</param>
        /// <returns></returns>
        public async Task<(XivModStatus, string)> IsModEnabled(string internalPath, bool indexCheck)
        {
            if (!File.Exists(_modListDirectory.FullName))
            {
                return (XivModStatus.Original, null);
            }

            if (indexCheck)
            {
                var modEntry = await TryGetModEntry(internalPath);

                if (modEntry == null)
                {
                    return (XivModStatus.Original, null);
                }

                var originalOffset = modEntry.data.originalOffset;
                var moddedOffset = modEntry.data.modOffset;

                var offset = await Index.GetDataOffset(
                    HashGenerator.GetHash(Path.GetDirectoryName(internalPath).Replace("\\", "/")),
                    HashGenerator.GetHash(Path.GetFileName(internalPath)),
                    XivDataFiles.GetXivDataFile(modEntry.datFile));

                if (offset.Equals(originalOffset))
                {
                    return (XivModStatus.Disabled, modEntry.modPack?.name);
                }

                if (offset.Equals(moddedOffset))
                {
                    return (XivModStatus.Enabled, modEntry.modPack?.name);
                }

                throw new Exception("Offset in Index does not match either original or modded offset in modlist.");
            }
            else
            {
                var modEntry = await TryGetModEntry(internalPath);

                if (modEntry == null)
                {
                    return (XivModStatus.Original, null);
                }

                return (modEntry.enabled ? XivModStatus.Enabled : XivModStatus.Disabled, modEntry.modPack?.name);
            }
        }

        /// <summary>
        /// Toggles the mod on or off
        /// </summary>
        /// <param name="internalFilePath">The internal file path of the mod</param>
        /// <param name="enable">The status of the mod</param>
        public async Task ToggleModStatus(string internalFilePath, bool enable)
        {
            if (string.IsNullOrEmpty(internalFilePath))
            {
                throw new Exception("File Path missing, unable to toggle mod.");
            }

            var modEntry = await TryGetModEntry(internalFilePath);

            if (modEntry == null)
            {
                throw new Exception("Unable to find mod entry in modlist.");
            }

            if (enable)
            {
                await Index.UpdateIndex(modEntry.data.modOffset, internalFilePath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                await Index.UpdateIndex2(modEntry.data.modOffset, internalFilePath, XivDataFiles.GetXivDataFile(modEntry.datFile));
            }
            else
            {
                await Index.UpdateIndex(modEntry.data.originalOffset, internalFilePath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                await Index.UpdateIndex2(modEntry.data.originalOffset, internalFilePath, XivDataFiles.GetXivDataFile(modEntry.datFile));
            }

            var modList = GetModList();

            var entryEnableUpdate = modList.Mods.Find(m => m.fullPath == modEntry.fullPath);
            entryEnableUpdate.enabled = enable;

            WriteModList(modList);
        }

        /// <summary>
        /// Toggles the mod on or off
        /// </summary>
        /// <param name="internalFilePath">The internal file path of the mod</param>
        /// <param name="enable">The status of the mod</param>
        public async Task ToggleModPackStatus(string modPackName, bool enable) 
        {
            var modList = GetModList();
            List<Mod> mods = null;

            if (modPackName.Equals("Standalone (Non-ModPack)"))
            {
                mods = (from mod in modList.Mods
                    where mod.modPack == null
                    select mod).ToList();
            }
            else
            {
                mods = (from mod in modList.Mods
                    where mod.modPack != null && mod.modPack.name.Equals(modPackName)
                    select mod).ToList();
            }


            if (mods == null)
            {
                throw new Exception("Unable to find mods with given Mod Pack Name in modlist.");
            }

            foreach (var modEntry in mods)
            {
                if(modEntry.name.Equals(string.Empty)) continue;

                if (enable)
                {
                    await Index.UpdateIndex(modEntry.data.modOffset, modEntry.fullPath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                    await Index.UpdateIndex2(modEntry.data.modOffset, modEntry.fullPath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                    modEntry.enabled = true;
                }
                else
                {
                    await Index.UpdateIndex(modEntry.data.originalOffset, modEntry.fullPath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                    await Index.UpdateIndex2(modEntry.data.originalOffset, modEntry.fullPath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                    modEntry.enabled = false;
                }
            }

            WriteModList(modList);
        }

        /// <summary>
        /// Toggles all mods on or off
        /// </summary>
        /// <param name="enable">The status to switch the mods to True if enable False if disable</param>
        public async Task ToggleAllMods(bool enable, IProgress<(int current, int total, string message)> progress = null) 
        {
            var modList = GetModList();

            if(modList == null || modList.modCount == 0) return;

            var modNum = 0;
            foreach (var modEntry in modList.Mods)
            {
                if(string.IsNullOrEmpty(modEntry.name)) continue;
                if(string.IsNullOrEmpty(modEntry.fullPath)) continue;
                
                if (enable && !modEntry.enabled)
                {
                    await Index.UpdateIndex(modEntry.data.modOffset, modEntry.fullPath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                    await Index.UpdateIndex2(modEntry.data.modOffset, modEntry.fullPath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                    modEntry.enabled = true;
                }
                else if (!enable && modEntry.enabled)
                {
                    await Index.UpdateIndex(modEntry.data.originalOffset, modEntry.fullPath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                    await Index.UpdateIndex2(modEntry.data.originalOffset, modEntry.fullPath, XivDataFiles.GetXivDataFile(modEntry.datFile));
                    modEntry.enabled = false;
                }

                progress?.Report((++modNum, modList.Mods.Count, string.Empty));
            }

            WriteModList(modList);
        }

        /// <summary>
        /// Disables all mods from older modlist
        /// </summary>
        public async Task DisableOldModList(DirectoryInfo oldModListDirectory)
        {
            using (var sr = new StreamReader(oldModListDirectory.FullName))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var modEntry = JsonConvert.DeserializeObject<OriginalModList>(line);

                    if (!string.IsNullOrEmpty(modEntry.fullPath))
                    {
                        try
                        {
                            await Index.UpdateIndex(modEntry.originalOffset, modEntry.fullPath,
                                XivDataFiles.GetXivDataFile(modEntry.datFile));
                            await Index.UpdateIndex2(modEntry.originalOffset, modEntry.fullPath,
                                XivDataFiles.GetXivDataFile(modEntry.datFile));
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Unable to disable {modEntry.name} | {modEntry.fullPath}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Deletes a mod from the modlist
        /// </summary>
        /// <param name="modItemPath">The mod item path of the mod to delete</param>
        public async Task DeleteMod(string modItemPath) {
            var modList = GetModList(); 

            var modToRemove = (from mod in modList.Mods
                where mod.fullPath.Equals(modItemPath)
                select mod).FirstOrDefault();
            if (modToRemove.source == "FilesAddedByTexTools")
            {
                Index.DeleteFileDescriptor(modItemPath, XivDataFiles.GetXivDataFile(modToRemove.datFile));
                Index.DeleteFileDescriptor($"{modItemPath}.flag", XivDataFiles.GetXivDataFile(modToRemove.datFile));
            }
            if (modToRemove.enabled)
            {
                await ToggleModStatus(modItemPath, false);
            }

            modToRemove.name = string.Empty;
            modToRemove.category = string.Empty;
            modToRemove.fullPath = string.Empty;
            modToRemove.source = string.Empty;
            modToRemove.modPack = null;
            modToRemove.enabled = false;
            modToRemove.data.originalOffset = 0;
            modToRemove.data.dataType = 0;

            modList.emptyCount += 1;
            modList.modCount -= 1;


            WriteModList(modList);
        }

        /// <summary>
        /// Deletes a Mod Pack and all its mods from the modlist
        /// </summary>
        /// <param name="modPackName">The name of the Mod Pack to be deleted</param>
        public async Task DeleteModPack(string modPackName)
        {
            var modList = GetModList();

            var modPackItem = (from modPack in modList.ModPacks
                where modPack.name.Equals(modPackName)
                select modPack).FirstOrDefault();

            modList.ModPacks.Remove(modPackItem);

            var modsToRemove = (from mod in modList.Mods
                where mod.modPack != null && mod.modPack.name.Equals(modPackName)
                select mod).ToList();

            var modRemoveCount = modsToRemove.Count;

            foreach (var modToRemove in modsToRemove)
            {
                if (modToRemove.source == "FilesAddedByTexTools")
                {
                    Index.DeleteFileDescriptor(modToRemove.fullPath, XivDataFiles.GetXivDataFile(modToRemove.datFile));
                    Index.DeleteFileDescriptor($"{modToRemove.fullPath}.flag", XivDataFiles.GetXivDataFile(modToRemove.datFile));
                }
                if (modToRemove.enabled)
                {
                    await ToggleModStatus(modToRemove.fullPath, false);
                }

                modToRemove.name = string.Empty;
                modToRemove.category = string.Empty;
                modToRemove.fullPath = string.Empty;
                modToRemove.source = string.Empty;
                modToRemove.modPack = null;
                modToRemove.enabled = false;
                modToRemove.data.originalOffset = 0;
                modToRemove.data.dataType = 0;
            }

            modList.emptyCount += modRemoveCount;
            modList.modCount -= modRemoveCount;
            modList.modPackCount -= 1;

            WriteModList(modList);
        }

        public void WriteModList(ModList modList) 
        {
            _modList = null;
            using (var stream = File.Open(_modListDirectory.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            using (var json = new JsonTextWriter(writer))
                _serializer.Serialize(json, modList);
        }

        public ModList GetModList()
        {
            if (_modList != null) 
            {
                return _modList;
            }
            using (var stream = File.OpenRead(_modListDirectory.FullName))
            using (var reader = new StreamReader(stream))
            using (var json = new JsonTextReader(reader))
                return _modList = _serializer.Deserialize<ModList>(json);
        }

        public TTMP NewModPack() {
            return new TTMP(this, _modSource);
        }
    }
}