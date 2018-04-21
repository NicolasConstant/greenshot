﻿/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2016 Thomas Braun, Jens Klingen, Robin Krom
 * 
 * For more information see: http://getgreenshot.org/
 * The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using Greenshot.IniFile;
using Greenshot.Plugin;
using GreenshotPlugin.Core;

namespace GreenshotLutimPlugin
{
    /// <summary>
    /// A collection of Lutim helper methods
    /// </summary>
    public static class LutimUtils
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(LutimUtils));
        private const string SmallUrlPattern = "http://i.Lutim.com/{0}s.jpg";
        private static readonly LutimConfiguration Config = IniConfig.GetIniSection<LutimConfiguration>();
        private const string AuthUrlPattern = "https://api.Lutim.com/oauth2/authorize?response_type=token&client_id={ClientId}&state={State}";
        private const string TokenUrl = "https://api.Lutim.com/oauth2/token";

        /// <summary>
        /// Check if we need to load the history
        /// </summary>
        /// <returns></returns>
        public static bool IsHistoryLoadingNeeded()
        {
            return false; //TODO reactivate this

            //Log.InfoFormat("Checking if Lutim cache loading needed, configuration has {0} Lutim hashes, loaded are {1} hashes.", Config.LutimUploadHistory.Count, Config.runtimeLutimHistory.Count);
            //return Config.runtimeLutimHistory.Count != Config.LutimUploadHistory.Count;
        }

        /// <summary>
        /// Load the complete history of the Lutim uploads, with the corresponding information
        /// </summary>
        public static void LoadHistory()
        {
            if (!IsHistoryLoadingNeeded())
            {
                return;
            }

            bool saveNeeded = false;

            // Load the ImUr history
            foreach (string hash in Config.LutimUploadHistory.Keys.ToList())
            {
                if (Config.runtimeLutimHistory.ContainsKey(hash))
                {
                    // Already loaded
                    continue;
                }

                try
                {
                    var deleteHash = Config.LutimUploadHistory[hash];
                    LutimInfo lutimInfo = RetrieveLutimInfo(hash, deleteHash);
                    if (lutimInfo != null)
                    {
                        RetrieveLutimThumbnail(lutimInfo);
                        Config.runtimeLutimHistory[hash] = lutimInfo;
                    }
                    else
                    {
                        Log.InfoFormat("Deleting unknown Lutim {0} from config, delete hash was {1}.", hash, deleteHash);
                        Config.LutimUploadHistory.Remove(hash);
                        Config.runtimeLutimHistory.Remove(hash);
                        saveNeeded = true;
                    }
                }
                catch (WebException wE)
                {
                    bool redirected = false;
                    if (wE.Status == WebExceptionStatus.ProtocolError)
                    {
                        HttpWebResponse response = (HttpWebResponse)wE.Response;

                        if (response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            Log.Error("Lutim loading forbidden", wE);
                            break;
                        }
                        // Image no longer available?
                        if (response.StatusCode == HttpStatusCode.Redirect)
                        {
                            Log.InfoFormat("Lutim image for hash {0} is no longer available, removing it from the history", hash);
                            Config.LutimUploadHistory.Remove(hash);
                            Config.runtimeLutimHistory.Remove(hash);
                            redirected = true;
                        }
                    }
                    if (!redirected)
                    {
                        Log.Error("Problem loading Lutim history for hash " + hash, wE);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Problem loading Lutim history for hash " + hash, e);
                }
            }
            if (saveNeeded)
            {
                // Save needed changes
                IniConfig.Save();
            }
        }

       
        /// <summary>
        /// Do the actual upload to Lutim
        /// For more details on the available parameters, see: http://api.Lutim.com/resources_anon
        /// </summary>
        /// <param name="surfaceToUpload">ISurface to upload</param>
        /// <param name="outputSettings">OutputSettings for the image file format</param>
        /// <param name="title">Title</param>
        /// <param name="filename">Filename</param>
        /// <returns>LutimInfo with details</returns>
        public static LutimInfo UploadToLutim(ISurface surfaceToUpload, SurfaceOutputSettings outputSettings, string title, string filename)
        {
            IDictionary<string, object> otherParameters = new Dictionary<string, object>();
            // add title
            if (title != null && Config.AddTitle)
            {
                otherParameters["title"] = title;
            }
            // add filename
            if (filename != null && Config.AddFilename)
            {
                otherParameters["name"] = filename;
            }
            string responseString = null;
            if (Config.AnonymousAccess)
            {
                // add key, we only use the other parameters for the AnonymousAccess
                //otherParameters.Add("key", Lutim_ANONYMOUS_API_KEY);
                HttpWebRequest webRequest = NetworkHelper.CreateWebRequest(Config.LutimApi3Url + "/upload.xml?" + NetworkHelper.GenerateQueryParameters(otherParameters), HTTPMethod.POST);
                webRequest.ContentType = "image/" + outputSettings.Format;
                webRequest.ServicePoint.Expect100Continue = false;

                try
                {
                    using (var requestStream = webRequest.GetRequestStream())
                    {
                        ImageOutput.SaveToStream(surfaceToUpload, requestStream, outputSettings);
                    }

                    using (WebResponse response = webRequest.GetResponse())
                    {
                        LogRateLimitInfo(response);
                        var responseStream = response.GetResponseStream();
                        if (responseStream != null)
                        {
                            using (StreamReader reader = new StreamReader(responseStream, true))
                            {
                                responseString = reader.ReadToEnd();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Upload to Lutim gave an exeption: ", ex);
                    throw;
                }
            }
            else
            {

                var oauth2Settings = new OAuth2Settings
                {
                    AuthUrlPattern = AuthUrlPattern,
                    TokenUrl = TokenUrl,
                    RedirectUrl = "https://Lutim.com",
                    CloudServiceName = "Lutim",
                    AuthorizeMode = OAuth2AuthorizeMode.EmbeddedBrowser,
                    BrowserSize = new Size(680, 880),
                    RefreshToken = Config.RefreshToken,
                    AccessToken = Config.AccessToken,
                    AccessTokenExpires = Config.AccessTokenExpires
                };

                // Copy the settings from the config, which is kept in memory and on the disk

                try
                {
                    var webRequest = OAuth2Helper.CreateOAuth2WebRequest(HTTPMethod.POST, Config.LutimApi3Url + "/upload.xml", oauth2Settings);
                    otherParameters["image"] = new SurfaceContainer(surfaceToUpload, outputSettings, filename);

                    NetworkHelper.WriteMultipartFormData(webRequest, otherParameters);

                    responseString = NetworkHelper.GetResponseAsString(webRequest);
                }
                finally
                {
                    // Copy the settings back to the config, so they are stored.
                    Config.RefreshToken = oauth2Settings.RefreshToken;
                    Config.AccessToken = oauth2Settings.AccessToken;
                    Config.AccessTokenExpires = oauth2Settings.AccessTokenExpires;
                    Config.IsDirty = true;
                    IniConfig.Save();
                }
            }
            if (string.IsNullOrEmpty(responseString))
            {
                return null;
            }
            return LutimInfo.ParseResponse(responseString);
        }

        /// <summary>
        /// Retrieve the thumbnail of an Lutim image
        /// </summary>
        /// <param name="lutimInfo"></param>
        public static void RetrieveLutimThumbnail(LutimInfo lutimInfo)
        {
            //TODO see if relevant
            //if (lutimInfo.SmallSquare == null)
            //{
            //    Log.Warn("Lutim URL was null, not retrieving thumbnail.");
            //    return;
            //}
            //Log.InfoFormat("Retrieving Lutim image for {0} with url {1}", lutimInfo.Hash, lutimInfo.SmallSquare);
            //HttpWebRequest webRequest = NetworkHelper.CreateWebRequest(string.Format(SmallUrlPattern, lutimInfo.Hash), HTTPMethod.GET);
            //webRequest.ServicePoint.Expect100Continue = false;
            //// Not for getting the thumbnail, in anonymous modus
            ////SetClientId(webRequest);
            //using (WebResponse response = webRequest.GetResponse())
            //{
            //    LogRateLimitInfo(response);
            //    Stream responseStream = response.GetResponseStream();
            //    if (responseStream != null)
            //    {
            //        lutimInfo.Image = ImageHelper.FromStream(responseStream);
            //    }
            //}
        }

        /// <summary>
        /// Retrieve information on an Lutim image
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="deleteHash"></param>
        /// <returns>LutimInfo</returns>
        public static LutimInfo RetrieveLutimInfo(string hash, string deleteHash)
        {
            string url = Config.LutimApi3Url + "/image/" + hash + ".xml";
            Log.InfoFormat("Retrieving Lutim info for {0} with url {1}", hash, url);
            HttpWebRequest webRequest = NetworkHelper.CreateWebRequest(url, HTTPMethod.GET);
            webRequest.ServicePoint.Expect100Continue = false;
            string responseString = null;
            try
            {
                using (WebResponse response = webRequest.GetResponse())
                {
                    LogRateLimitInfo(response);
                    var responseStream = response.GetResponseStream();
                    if (responseStream != null)
                    {
                        using (StreamReader reader = new StreamReader(responseStream, true))
                        {
                            responseString = reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (WebException wE)
            {
                if (wE.Status == WebExceptionStatus.ProtocolError)
                {
                    if (((HttpWebResponse)wE.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                }
                throw;
            }
            LutimInfo lutimInfo = null;
            if (responseString != null)
            {
                Log.Debug(responseString);
                lutimInfo = LutimInfo.ParseResponse(responseString);
                lutimInfo.DeleteHash = deleteHash;
            }
            return lutimInfo;
        }

        /// <summary>
        /// Delete an Lutim image, this is done by specifying the delete hash
        /// </summary>
        /// <param name="lutimInfo"></param>
        public static void DeleteLutimImage(LutimInfo lutimInfo)
        {
            Log.InfoFormat("Deleting Lutim image for {0}", lutimInfo.DeleteHash);

            try
            {
                string url = Config.LutimApi3Url + "/image/" + lutimInfo.DeleteHash + ".xml";
                HttpWebRequest webRequest = NetworkHelper.CreateWebRequest(url, HTTPMethod.DELETE);
                webRequest.ServicePoint.Expect100Continue = false;
                string responseString = null;
                using (WebResponse response = webRequest.GetResponse())
                {
                    LogRateLimitInfo(response);
                    var responseStream = response.GetResponseStream();
                    if (responseStream != null)
                    {
                        using (StreamReader reader = new StreamReader(responseStream, true))
                        {
                            responseString = reader.ReadToEnd();
                        }
                    }
                }
                Log.InfoFormat("Delete result: {0}", responseString);
            }
            catch (WebException wE)
            {
                // Allow "Bad request" this means we already deleted it
                if (wE.Status == WebExceptionStatus.ProtocolError)
                {
                    if (((HttpWebResponse)wE.Response).StatusCode != HttpStatusCode.BadRequest)
                    {
                        throw;
                    }
                }
            }
            // Make sure we remove it from the history, if no error occured
            Config.runtimeLutimHistory.Remove(lutimInfo.Hash);
            Config.LutimUploadHistory.Remove(lutimInfo.Hash);
            lutimInfo.Image = null;
        }

        /// <summary>
        /// Helper for logging
        /// </summary>
        /// <param name="nameValues"></param>
        /// <param name="key"></param>
        private static void LogHeader(IDictionary<string, string> nameValues, string key)
        {
            if (nameValues.ContainsKey(key))
            {
                Log.InfoFormat("{0}={1}", key, nameValues[key]);
            }
        }

        /// <summary>
        /// Log the current rate-limit information
        /// </summary>
        /// <param name="response"></param>
        private static void LogRateLimitInfo(WebResponse response)
        {
            IDictionary<string, string> nameValues = new Dictionary<string, string>();
            foreach (string key in response.Headers.AllKeys)
            {
                if (!nameValues.ContainsKey(key))
                {
                    nameValues.Add(key, response.Headers[key]);
                }
            }
            LogHeader(nameValues, "X-RateLimit-Limit");
            LogHeader(nameValues, "X-RateLimit-Remaining");
            LogHeader(nameValues, "X-RateLimit-UserLimit");
            LogHeader(nameValues, "X-RateLimit-UserRemaining");
            LogHeader(nameValues, "X-RateLimit-UserReset");
            LogHeader(nameValues, "X-RateLimit-ClientLimit");
            LogHeader(nameValues, "X-RateLimit-ClientRemaining");

            // Update the credits in the config, this is shown in a form
            int credits;
            if (nameValues.ContainsKey("X-RateLimit-Remaining") && int.TryParse(nameValues["X-RateLimit-Remaining"], out credits))
            {
                Config.Credits = credits;
            }
        }
    }
}
