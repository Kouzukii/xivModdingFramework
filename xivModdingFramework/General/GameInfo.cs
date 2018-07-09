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

using System;
using System.IO;
using xivModdingFramework.General.Enums;

namespace xivModdingFramework
{
    public class GameInfo
    {
        private const string GameVersionFile = "ffxivgame.ver";

        /// <summary>
        /// The directory in which the game is installed.
        /// </summary>
        public DirectoryInfo GameDirectory { get; }


        /// <summary>
        /// The current version of the game.
        /// </summary>
        public Version GameVersion { get; }


        /// <summary>
        /// The language used when parsing the game data.
        /// </summary>
        public XivLanguage GameLanguage { get; }


        /// <summary>
        /// Constructor for GameInfo
        /// </summary>
        /// <param name="gameDirectory">The directory in which the game is installed.</param>
        /// <param name="xivLanguage">The language to use when parsing the game data.</param>
        public GameInfo(DirectoryInfo gameDirectory, XivLanguage xivLanguage)
        {
            GameDirectory = gameDirectory;
            GameLanguage  = xivLanguage;
            GameVersion   = GetGameVersion();

            if (!gameDirectory.FullName.Contains("game\\sqpack\\ffxiv"))
            {
                throw new DirectoryNotFoundException("The given directory is incorrect.\n\nThe directory sould point to the \\game\\sqpack\\ffxiv folder");
            }
        }


        /// <summary>
        /// Gets the games current version.
        /// </summary>
        /// <returns>The game version.</returns>
        private Version GetGameVersion()
        {
            var versionBasePath = GameDirectory.FullName.Substring(0, GameDirectory.FullName.IndexOf("sqpack", StringComparison.Ordinal));
            var versionFile = versionBasePath + "\\" + GameVersionFile;

            var versionData = File.ReadAllLines(versionFile);
            return new Version(versionData[0].Substring(0, versionData[0].LastIndexOf(".", StringComparison.Ordinal)));
        }
    }
}