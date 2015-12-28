﻿/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2015 Thomas Braun, Jens Klingen, Robin Krom
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

using GreenshotOfficePlugin.OfficeExport;
using GreenshotPlugin.Configuration;
using GreenshotPlugin.Core;
using GreenshotPlugin.Interfaces;
using GreenshotPlugin.Interfaces.Destination;
using GreenshotPlugin.Interfaces.Plugin;

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using GreenshotPlugin.Extensions;

namespace GreenshotOfficePlugin.Destinations
{
	/// <summary>
	/// Description of PowerpointDestination.
	/// </summary>
	[Destination(PowerpointDesignation), PartNotDiscoverable]
	public sealed class PowerpointDestination : AbstractDestination
	{
		public const string PowerpointDesignation = "Powerpoint";
		private static readonly Serilog.ILogger LOG = Serilog.Log.Logger.ForContext(typeof(PowerpointDestination));
		private static readonly BitmapSource PresentationIcon;
		private static readonly BitmapSource ApplicationIcon;
		
		static PowerpointDestination()
		{
			var exePath = PluginUtils.GetExePath("POWERPNT.EXE");
			if (exePath != null && File.Exists(exePath))
			{
				WindowDetails.AddProcessToExcludeFromFreeze("POWERPNT");
				ApplicationIcon = PluginUtils.GetCachedExeIcon(exePath, 0).ToBitmapSource();
				PresentationIcon = PluginUtils.GetCachedExeIcon(exePath, 1).ToBitmapSource();
				IsActive = true;
			}
		}

		/// <summary>
		/// Tells if the destination can be used
		/// </summary>
		public static bool IsActive
		{
			get;
			private set;
		}

		[Import]
		private IOfficeConfiguration OfficeConfiguration
		{
			get;
			set;
		}

		[Import]
		private IGreenshotLanguage GreenshotLanguage
		{
			get;
			set;
		}

		protected override void Initialize()
		{
			base.Initialize();
			Export = async (exportContext, capture, token) => await ExportCaptureAsync(capture, null);
			Text = Text = $"Export to {PowerpointDesignation}";
			Designation = PowerpointDesignation;
			Icon = ApplicationIcon;
		}

		/// <summary>
		/// Load the current documents to export to
		/// </summary>
		/// <param name="caller1"></param>
		/// <param name="token"></param>
		/// <returns>Task</returns>
		public override Task RefreshAsync(IExportContext caller1, CancellationToken token = default(CancellationToken))
		{
			Children.Clear();
			return Task.Factory.StartNew(
				// this will use current synchronization context
				() =>
				{
					foreach (var presentation in PowerpointExporter.GetPowerpointPresentations())
					{
						var powerpointDestination = new PowerpointDestination
						{
							Icon = PresentationIcon,
							Export = async (caller, capture, exportToken) => await ExportCaptureAsync(capture, presentation),
							Text = presentation,
							OfficeConfiguration = OfficeConfiguration,
							GreenshotLanguage = GreenshotLanguage
						};
						Children.Add(powerpointDestination);
					}
				},
				token,
				TaskCreationOptions.None,
				TaskScheduler.FromCurrentSynchronizationContext()
			);
		}

		private Task<INotification> ExportCaptureAsync(ICapture capture, string presentationName)
		{
			INotification returnValue = new Notification
			{
				NotificationType = NotificationTypes.Success,
				Source = PowerpointDesignation,
				SourceType = SourceTypes.Destination,
				Text = $"Exported to {PowerpointDesignation}"
			};
			string tmpFile = capture.CaptureDetails.Filename;
			var imageSize = System.Drawing.Size.Empty;
			if (tmpFile == null || capture.Modified || !Regex.IsMatch(tmpFile, @".*(\.png|\.gif|\.jpg|\.jpeg|\.tiff|\.bmp)$"))
			{
				tmpFile = ImageOutput.SaveNamedTmpFile(capture, capture.CaptureDetails, new SurfaceOutputSettings().PreventGreenshotFormat());
				imageSize = capture.Image.Size;
			}
			try
			{
				if (presentationName != null)
				{
					PowerpointExporter.ExportToPresentation(presentationName, tmpFile, imageSize, capture.CaptureDetails.Title);
				}
				else
				{
					PowerpointExporter.InsertIntoNewPresentation(tmpFile, imageSize, capture.CaptureDetails.Title);
				}
			}
			catch (Exception ex)
			{
				LOG.Error(ex, "Powerpoint export failed");
				returnValue.NotificationType = NotificationTypes.Fail;
				returnValue.ErrorText = ex.Message;
				returnValue.Text = string.Format(GreenshotLanguage.DestinationExportFailed, PowerpointDesignation);
				return Task.FromResult(returnValue);
			}
			return Task.FromResult(returnValue);
		}
	}
}