using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using backend___central.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Services
{
    public class DictionarySynchronizingService : IDictionarySynchronizingService
    {
        private readonly IEnumerable<ILogService> logServices;
        private string Operation { get; set; }
        public string DictionaryDirectory { get; set; }
        private string[] DirectoryFiles { get; set; }

        public DictionarySynchronizingService(IEnumerable<ILogService> logServices)
        {
            Operation = "";
            DictionaryDirectory = "";
            DirectoryFiles = Array.Empty<string>();
            this.logServices = logServices;
        }

        public string GetCurrentDictionaryHashResult()
        {
            try
            {
                Operation = "getting current dictionary hash";
                DictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                HandleCreateDirectory();
                SetDirectoryFiles();
                return GetLatestDictionaryHash();
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error while retrieving current dictionary hash: {ex.Message}");
                return "";
            }
        }

        public IActionResult GetCurrentDictionaryPackResult()
        {
            ILogService.LogInfo(logServices, "Made request to get actual dictionary pack file");
            try
            {
                Operation = "getting current dictionary pack file";
                DictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                HandleCreateDirectory();
                SetDirectoryFiles();
                string latestFilePath = Path.Combine(DictionaryDirectory, GetLatestDictionaryHash());
                FileStream fileStream = new(latestFilePath, FileMode.Open, FileAccess.Read);
                return new FileStreamResult(fileStream, "application/txt");
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error while retrieving current dictionary pack: {ex.Message}");
                return new ContentResult {
                    Content = $"An error occurred while retrieving dictionary: {ex.Message}",
                    ContentType = "text/plain",
                    StatusCode = 500
                };
            }
        }

        public async Task<IActionResult> SynchronizeDictionaryResult(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Made request to synchronize dictionary");
            try
            {
                Operation = "synchronize dictionary";
                DictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                IFormCollection iFormCollection = await httpContext.Request.ReadFormAsync();
                IFormFile? iFormFile = iFormCollection.Files.GetFile("file");
                HandleValidateFile(iFormFile);
                string fileName = await HandleSaveFile(iFormFile);
                SynchronizeDictionaryWithConnectedServers(iFormFile, fileName);
                return new ContentResult {
                    Content = $"Successfully synchronized dictionary. Filename: {Path.GetFileName(fileName)}, Path: {DictionaryDirectory}",
                    ContentType = "text/plain",
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error while synchronizing dictionary: {ex.Message}");
                return new ContentResult {
                    Content = $"An error occurred while synchronizing dictionary pack file: {ex.Message}",
                    ContentType = "text/plain",
                    StatusCode = 500
                };
            }
        }

        public string GetLatestDictionaryHash()
        {
            FileInfo? latestFile = DirectoryFiles
                .Select(file => new FileInfo(file))
                .OrderByDescending(fileInfo => fileInfo.CreationTime)
                .FirstOrDefault();
            if (latestFile == null)
            {
                ILogService.LogError(logServices, $"No valid dictionary hash files found while trying to {Operation}");
                throw new Exception("No valid dictionary hash files found.");
            }
            string fileName = latestFile.Name;
            int startIndex = fileName.IndexOf("dictionary-");
            if (startIndex >= 0)
            {
                fileName = fileName[startIndex..];
            }
            return fileName;
        }

        public static MultipartFormDataContent CreateFormData(Stream fileStream, string fileName)
        {
            MultipartFormDataContent formData = new()
            {
                { new StreamContent(fileStream), "file", fileName }
            };
            return formData;
        }

        public void SetDirectoryFiles()
        {
            string[] directoryFiles = Directory.GetFiles(DictionaryDirectory);
            if (directoryFiles.Length <= 0)
            {
                ILogService.LogError(logServices, $"No files found in directory path while trying to {Operation}");
                throw new Exception("No files found in directory path.");
            }
            DirectoryFiles = directoryFiles;
        }

        private void SynchronizeDictionaryWithConnectedServers(IFormFile? iFormFile, string fileName)
        {
            if (iFormFile == null) return;
            List<IPAddress> serversToRemove = new();
            List<IPAddress> connectedServers = Startup.ServersIpAddresses;
            foreach (IPAddress connectedServer in connectedServers)
            {
                bool isSuccess = TrySynchronizeWithServer(iFormFile, fileName, connectedServer).Result;
                if (!isSuccess)
                {
                    serversToRemove.Add(connectedServer);
                }
            }
            RemoveUnresponsiveServers(serversToRemove);
        }

        private async Task<bool> TrySynchronizeWithServer(IFormFile iFormFile, string fileName, IPAddress connectedServer)
        {
            try
            {
                using HttpClient httpClient = new();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                using MemoryStream memoryStream = new();
                await iFormFile.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                using MultipartFormDataContent formData = CreateFormData(memoryStream, fileName);
                string serverUrl = $"http://{connectedServer}:5099/api/dictionary/synchronizing";
                HttpResponseMessage response = await httpClient.PostAsync(serverUrl, formData);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Server {connectedServer} responded with status code {response.StatusCode}");
                }
                ILogService.LogInfo(logServices, $"Successfully synchronized dictionary with server {connectedServer}");
                return true;
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Failed to synchronize dictionary with server {connectedServer}: {ex.Message}");
                return false;
            }
        }

        private void RemoveUnresponsiveServers(List<IPAddress> serversToRemove)
        {
            foreach (IPAddress serverToRemove in serversToRemove)
            {
                Startup.ServersIpAddresses.Remove(serverToRemove);
                ILogService.LogInfo(logServices, $"Removed unresponsive server {serverToRemove} from the list");
            }
        }

        private async Task<string> HandleSaveFile(IFormFile? iFormFile)
        {
            HandleCreateDirectory();
            return await HandleSaveDictionaryPack(iFormFile);
        }

        private async Task<string> HandleSaveDictionaryPack(IFormFile? iFormFile)
        {
            if (iFormFile == null)
            {
                throw new ArgumentNullException(nameof(iFormFile), "File cannot be null");
            }
            string dictionaryLocation = Path.Combine(DictionaryDirectory, GetNewDictionaryPackName());
            using (FileStream fileStream = new (dictionaryLocation, FileMode.CreateNew))
            {
                await iFormFile.CopyToAsync(fileStream);
                await fileStream.FlushAsync(); 
            }
            FileInfo fileInfo = new (dictionaryLocation);
            if (fileInfo.Length == 0)
            {
                ILogService.LogError(logServices, "Created file is empty");
                throw new Exception("Failed to save dictionary content - file is empty");
            }
            ILogService.LogInfo(logServices, $"Successfully saved new dictionary with size: {fileInfo.Length} bytes");
            return dictionaryLocation;
        }

        private void HandleValidateFile(IFormFile? iFormFile)
        {
            bool isFormFileNotExists = iFormFile == null || iFormFile.Length == 0;
            bool isFormFileNameInvalid = iFormFile != null && !iFormFile.FileName.EndsWith(".txt");
            if (isFormFileNotExists || isFormFileNameInvalid)
            {
                ILogService.LogError(logServices, $"Received dictionary pack was invalid while trying to {Operation}");
                throw new Exception("Invalid dictionary pack content or format.");
            }
        }

        private void HandleCreateDirectory()
        {
            if (!Directory.Exists(DictionaryDirectory))
            {
                Directory.CreateDirectory(DictionaryDirectory);
                ILogService.LogInfo(logServices, "Created dictionary directory");
            }
        }

        private static string GetNewDictionaryPackName()
        {
            byte[] guidByteArray = Guid.NewGuid().ToByteArray();
            byte[] hashByteArray = SHA256.HashData(guidByteArray);
            string hash = Convert.ToHexString(hashByteArray)[..16];
            string currentDate = DateTime.Now.ToString("yyyy-MM-dd");
            return $"dictionary-{hash}-{currentDate}.txt";
        }
    }
}