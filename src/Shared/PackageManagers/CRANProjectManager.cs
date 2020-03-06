﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;

namespace Microsoft.OpenSource.Shared
{
    class CRANProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CRAN_ENDPOINT = "https://cran.r-project.org";

        /// <summary>
        /// Download one CRAN package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<string> DownloadVersion(PackageURL purl)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            string downloadedPath = null;

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Error("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPath;
            }

            // Current Version
            try
            {
                var url = $"{ENV_CRAN_ENDPOINT}/src/contrib/{packageName}_{packageVersion}.tar.gz";
                var result = await WebClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl.ToString());
                downloadedPath = await ExtractArchive($"cran-{packageName}@{packageVersion}", await result.Content.ReadAsByteArrayAsync());
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error downloading CRAN package: {ex.Message}. Checking archives instead.");
                downloadedPath = null;
            }
            if (downloadedPath != null)
            {
                return downloadedPath;
            }

            // Archive Version - Only continue here if needed
            try
            {
                var url = $"{ENV_CRAN_ENDPOINT}/src/contrib/Archive/{packageName}/{packageName}_{packageVersion}.tar.gz";
                Console.WriteLine(url);
                var result = await WebClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl.ToString());
                downloadedPath = await ExtractArchive($"cran-{packageName}@{packageVersion}", await result.Content.ReadAsByteArrayAsync());
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error downloading CRAN package: {0}", ex.Message);
                downloadedPath = null;
            }
            return downloadedPath;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var versionList = new List<string>();

                // Get the latest version
                var html = await WebClient.GetAsync($"{ENV_CRAN_ENDPOINT}/web/packages/{packageName}/index.html");
                html.EnsureSuccessStatusCode();
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(await html.Content.ReadAsStringAsync());
                var tds = document.QuerySelectorAll("td");
                for (int i = 0; i < tds.Length; i++)
                {
                    if (tds[i].TextContent == "Version:")
                    {
                        versionList.Add(tds[i + 1]?.TextContent?.Trim());
                        break;
                    }
                }

                // Get the remaining versions
                html = await WebClient.GetAsync($"{ENV_CRAN_ENDPOINT}/src/contrib/Archive/{packageName}/");
                html.EnsureSuccessStatusCode();
                document = await parser.ParseDocumentAsync(await html.Content.ReadAsStringAsync());
                tds = document.QuerySelectorAll("a");
                foreach (var td in tds)
                {
                    var href = td.GetAttribute("href");
                    if (href.Contains(".tar.gz"))
                    {
                        var version = href.Replace(".tar.gz", "");
                        version = version.Replace(packageName + "_", "");
                        Logger.Debug("Identified {0} version {1}.", packageName, version);
                        versionList.Add(version);
                    }
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error enumerating CRAN package: {ex.Message}");
                return Array.Empty<string>();
            }
        }
        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_CRAN_ENDPOINT}/web/packages/{packageName}/index.html");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching CRAN metadata: {ex.Message}");
                return null;
            }
        }
    }
}