using System;
using System.Collections.Generic;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MCD.FN.ManageGit.Models;
using System.Text;

namespace MCD.FN.ManageGit
{
    public class BlobUploadService
    {
        private readonly string _repositoryBasePath;
        const string tempFolderName = "tempfiles";
        const string filesDirectoryName = "files";
        const string manifestDirectoryName = "manifest";
        const string manifestFileName = "manifest.json";
        const string localizationFolderName = "localization";
        public const string storeVersionFolderName = "storeVersionFolder";
        public const string storeVersionFileName = "storeversion.json";

        public BlobUploadService()
        {
            _repositoryBasePath = GetBaseRepo();
        }

        public static string GetBaseRepo()
        {
            return Path.Combine(Environment.GetEnvironmentVariable("TEMP"));
        }

        public static string GetConfigFolder(string storeId)
        {
            var tempDirPath = Path.Combine(GetBaseRepo(), tempFolderName, storeId, filesDirectoryName);

            //Deleting the temp directory if it exist
            if (Directory.Exists(tempDirPath))
                Directory.Delete(tempDirPath, true);

            //Creating temp directory
            Directory.CreateDirectory(tempDirPath);

            return tempDirPath;
        }

        public static string GetStoreVersionFolder(string storeId)
        {
            var tempDirPath = Path.Combine(GetBaseRepo(), storeVersionFolderName, storeId);

            //Deleting the temp directory if it exist
            if (Directory.Exists(tempDirPath))
                Directory.Delete(tempDirPath, true);

            //Creating temp directory
            Directory.CreateDirectory(tempDirPath);

            return tempDirPath;
        }

        /// <summary>
        ///  Getting required blob storage container Uri to upload files from appsettings.json values using configuration object.
        /// </summary>
        public static Uri GetBlobStorageConnection(ILogger _logger)
        {
            Uri uri = null;
            try
            {
                var cloudFileServer = Helper.GetEnvironmentVariable("BlobEndpoint");

                if (cloudFileServer.StartsWith("https://") && cloudFileServer.Contains(".blob.core.windows.net/"))
                {
                    var endPoint = Helper.GetEnvironmentVariable("VersionFileEndPoint");

                    var accessToken = Helper.GetEnvironmentVariable("SasAccountKey");

                    uri = new Uri(cloudFileServer).Append(endPoint).AppendQuery(accessToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to read connection details. {ex}");
            }

            return uri;
        }

        public static void UploadConfigFolder(string storeId, ILogger _logger)
        {
            _logger.LogInformation($"Uploading config files from store: {storeId}");

            if (!IsValidMarket(storeId, _logger))
                _logger.LogError($"Error: StoreId not valid: {storeId}");

            _logger.LogInformation($"Connecting to existing blob container.");

            var uri = GetBlobStorageConnection(_logger);

            if (uri != null)
            {
                var cloudBlobContainer = new CloudBlobContainer(uri);

                try
                {
                    var localPath = Path.Combine(GetBaseRepo(), tempFolderName, storeId);

                    var localFilePath = Path.Combine(localPath, filesDirectoryName);

                    var isManifest = CreateManifest(localFilePath, localPath, storeId, _logger);

                    if (!isManifest)
                        _logger.LogError($"Error: Creating manifest file failed.");

                    var isUploaded = UploadFilesToBlob(cloudBlobContainer, localPath, _logger);

                    if (!isUploaded)
                        _logger.LogError($"Error: Uploading the config files to blob failed.");

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error: Migration to blob failed. {ex}");
                }
            }
        }

        public static bool CreateManifest(string sourcePath, string destinationPath, string storeId, ILogger _logger)
        {
            try
            {
                _logger.LogInformation($"Creating manifest.");

                if (Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(Path.Combine(destinationPath, manifestDirectoryName));
                    var pathManifestDir = Path.Combine(destinationPath, manifestDirectoryName, manifestFileName);

                    var manifestFile = BuildPackageContentsFromFolderStructure(sourcePath, _logger);

                    var utcNow = DateTime.UtcNow;

                    var cloudManifestModel = new CloudManifestModel
                    {
                        Version = Guid.NewGuid().ToString("n"),
                        PackageType = "CONFIG",
                        ProductVersion = storeId,
                        Contents = manifestFile
                    };

                    var manifestJson = JsonConvert.SerializeObject(cloudManifestModel, Formatting.Indented);

                    File.WriteAllText(pathManifestDir, manifestJson);

                    _logger.LogInformation($@"Manifest file [{pathManifestDir}] generated successfully.");

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: Creating manifest failed. {ex}");
            }

            return false;
        }

        /// <summary>
        /// Builds package contents from an existing folder structure.
        /// </summary>
        /// <param name="productFolderPath">The path to the folder containing the product files.</param>
        /// <returns>The <see cref="IEnumerable{T}"/> of <see cref="PackageContent"/> describing the package contents.</returns>
        private static IEnumerable<ManifestFile> BuildPackageContentsFromFolderStructure(string productFolderPath, ILogger _logger)
        {
            try
            {
                if (!productFolderPath.EndsWith("\\"))
                    productFolderPath = productFolderPath + "\\";

                var manifestFile = Directory
               .GetFiles(productFolderPath, "*.*", SearchOption.AllDirectories)
               .Select(file => new ManifestFile
               {
                   Path = FormatFileNameForManifest(file, productFolderPath),
                   Version = CalculateMd5(file),
                   Operation = FileOperation.ADD.ToString(),
                   Target = "ALL"
               }).ToList();

                return manifestFile;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: Building manifest content failed. {ex}");
                return new List<ManifestFile>().DefaultIfEmpty();
            }

        }

        /// <summary>
        /// Formats file name for manifest by stripping off parent folder path from file path
        /// and replacing back slashes ("\") with forward slashes ("/").
        /// </summary>
        /// <param name="filePath">The full file path.</param>
        /// <param name="parentFolder">The parent folder path.</param>
        /// <returns></returns>
        private static string FormatFileNameForManifest(string filePath, string parentFolder)
        {
            return filePath.Replace(parentFolder, "").Replace("\\", "/");
        }

        /// <summary>
        /// Calculates file MD5
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>MD5 string hash</returns>
        private static string CalculateMd5(string filePath)
        {
            byte[] hashBytes;

            using (var md5 = MD5.Create())
            {
                var fileBytes = File.ReadAllBytes(filePath);
                hashBytes = md5.ComputeHash(fileBytes);
            }

            var hashMd5 = ConvertBytesToString(hashBytes);

            return hashMd5;
        }

        /// <summary>
        /// Converts a byte array to string.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <returns>The string representation of the byte array.</returns>
        private static string ConvertBytesToString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLower();
        }

        private static bool IsValidMarket(string storeId, ILogger _logger)
        {
            if (storeId.Length > 2)
            {
                var market = storeId.Substring(0, 2);
                const string caseInsensitiveLetterPattern = @"^[a-zA-Z]+$";
                if (!Regex.IsMatch(market, caseInsensitiveLetterPattern))
                {
                    _logger.LogInformation("Store Id does not contain market name.");
                    return false;
                }
            }
            return true;
        }

        public static void UpdateFilesBeforeUploadToBlob(string destinationFolder, string marketName, ILogger _logger)
        {
            var localizationFolderPath = Path.Combine(destinationFolder, localizationFolderName);
            var sourceFolder = Path.Combine(localizationFolderPath, marketName);

            try
            {
                if (Directory.Exists(sourceFolder))
                {
                    foreach (string dir in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
                    {
                        var targetFolder = Path.Combine(destinationFolder, dir.Substring(sourceFolder.Length + 1));
                        if (!(Directory.Exists(targetFolder)))
                            Directory.CreateDirectory(targetFolder);
                        foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            var fileName = file.Substring(file.LastIndexOf("\\") + 1);
                            var targetFile = Path.Combine(targetFolder, fileName);
                            File.Copy(file, targetFile, true);
                        }
                    }
                    foreach (var file in Directory.GetFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        File.Copy(file, Path.Combine(destinationFolder, Path.GetFileName(file)), true);
                    }
                    Directory.Delete(localizationFolderPath, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: Updating the files to local failed. {ex}");
            }
        }

        public static void CreateLocalFolder(string currentFolderPath, string tempDirPath)
        {
            if (currentFolderPath[0] == '/')
                currentFolderPath = currentFolderPath.Substring(1);

            currentFolderPath = currentFolderPath.Replace("/", @"\").Trim();

            if (!string.IsNullOrEmpty(currentFolderPath))
            {
                currentFolderPath = Path.Combine(tempDirPath, currentFolderPath);

                Directory.CreateDirectory(currentFolderPath);
            }
        }

        public static void CreateLocalFile(string currentFilePath, string tempDirPath, string content)
        {
            if (currentFilePath[0] == '/')
                currentFilePath = currentFilePath.Substring(1);

            currentFilePath = currentFilePath.Replace("/", @"\").Trim();

            var startIndex = currentFilePath.LastIndexOf(@"\");
            var currentFileName = currentFilePath.Substring(startIndex + 1);

            if ((currentFileName != ".gitignore") && (currentFileName.ToLower() != storeVersionFileName))
            {
                //Saving files locally
                using (FileStream fs = File.Create(Path.Combine(tempDirPath, currentFilePath)))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes(content);
                    // Adding content to the file.
                    fs.Write(info, 0, info.Length);
                }
            }
        }

        public static void UploadStoreVersionFile(string storeId, ILogger _logger)
        {
            _logger.LogInformation($"Saving storeversion file from store: {storeId}");

            if (!IsValidMarket(storeId, _logger))
                _logger.LogError($"Error: StoreId not valid: {storeId}");

            _logger.LogInformation($"Connecting to existing blob container.");

            var uri = GetBlobStorageConnection(_logger);

            if (uri != null)
            {
                var cloudBlobContainer = new CloudBlobContainer(uri);

                try
                {
                    var localPath = Path.Combine(GetBaseRepo(), storeVersionFolderName, storeId);

                    var localFilePath = Path.Combine(localPath, filesDirectoryName);

                    var isUploaded = UploadFilesToBlob(cloudBlobContainer, localPath, _logger);

                    if (!isUploaded)
                        _logger.LogError($"Error: Uploading the storeversion file to blob failed.");

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error: Uploading the storeversion file to blob failed. {ex}");
                }
            }
        }        
        
        public static bool UploadFilesToBlob(CloudBlobContainer cloudBlobContainer, string sourcePath, ILogger _logger)
        {
            if (Directory.Exists(sourcePath))
            {
                try
                {
                    var localDirectories = Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories);

                    _logger.LogInformation($"Uploading the files to blob.");

                    foreach (string file in localDirectories)
                    {
                        string foldername = sourcePath.Substring(sourcePath.LastIndexOf('\\') + 1);
                        string cloudfilename = file.Replace(sourcePath, "");
                        cloudfilename = foldername + cloudfilename;

                        var blockBlob = cloudBlobContainer.GetBlockBlobReference(cloudfilename);
                        blockBlob.UploadFromFile(file);
                    }

                    _logger.LogInformation($"Uploading the files to blob succeeded.");

                    //deleting store file directory from local
                    Directory.Delete(sourcePath, true);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Uploading the files to blob failed. {ex}");
                }
            }

            return false;
        }
    }
}