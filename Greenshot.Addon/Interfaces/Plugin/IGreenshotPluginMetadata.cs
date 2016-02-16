﻿/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2016 Thomas Braun, Jens Klingen, Robin Krom
 * 
 * For more information see: http://getgreenshot.org/
 * The Greenshot project is hosted on Sourceforge: http://sourceforge.net/projects/greenshot/
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System.ComponentModel;

namespace Greenshot.Addon.Interfaces.Plugin
{
	/// <summary>
	///  Meta-data belonging to the PluginAttribute, which makes it possible to specify type-safe meta-data.
	/// </summary>
	public interface IGreenshotPluginMetadata
	{
		/// <summary>
		/// The name of the plugin
		/// </summary>
		string Name
		{
			get;
		}

		/// <summary>
		/// Name of the creator
		/// </summary>
		[DefaultValue("Greenshot")]
		string CreatedBy
		{
			get;
		}

		[DefaultValue(false)]
		bool Configurable
		{
			get;
		}
	}
}