using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using backend___calculating.Models;
using System.Security.Cryptography;
using backend___calculating.Interfaces;

namespace backend___calculating.Services
{
    public class DictionaryService : IDictionaryService
    {
        private const int TimeLimit = 60000;
        private readonly IEnumerable<ILogService> logServices;
        private string DictionaryDirectory { get; set; }
        private string[] DirectoryFiles { get; set; }
        private readonly IPasswordRepository _passwordRepository;

        public string? FoundPassword { get; private set; }
        public bool IsPasswordFound { get; private set; }

        public DictionaryService(IEnumerable<ILogService> logServices, IPasswordRepository passwordRepository)
        {
            DictionaryDirectory = "";
            DirectoryFiles = Array.Empty<string>();
            this.logServices = logServices;
            _passwordRepository = passwordRepository;
            IsPasswordFound = false;
            FoundPassword = null;
        }

        public async Task<ActionResult> SynchronizeDictionaryResult(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Synchronizing dictionary with central server");
            try
            {
                DictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                HandleCreateDirectory();
                IFormCollection iFormCollection = await httpContext.Request.ReadFormAsync();
                IFormFile? iFormFile = iFormCollection.Files.GetFile("file");
                HandleValidateFile(iFormFile);
                string fileName = await HandleSaveFile(iFormFile);
                return new ContentResult
                {
                    Content = $"Successfully synchronized dictionary. Filename: {Path.GetFileName(fileName)}.txt, Path: {DictionaryDirectory}",
                    ContentType = "text/plain",
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error while synchronizing dictionary: {ex.Message}");
                return new ContentResult
                {
                    Content = $"An error occurred while synchronizing dictionary pack file: {ex.Message}",
                    ContentType = "text/plain",
                    StatusCode = 500
                };
            }
        }

        public async Task<ActionResult> StartCrackingResult(HttpContext httpContext)
        {
            DateTime startTime = DateTime.UtcNow;
            try
            {
                var (username, chunkInfo) = await ReadAndDeserializeRequest(httpContext);
                List<string> selectedPasswords = await ReadPasswordsFromDictionary(chunkInfo);
                ValidateSelectedPasswords(selectedPasswords, chunkInfo);
                await CheckPasswordsAgainstDatabase(selectedPasswords, username);
                LogSuccessfulPasswordLoading(selectedPasswords, chunkInfo);
                DateTime endTime = DateTime.UtcNow;
                int processingTime = (int)(endTime - startTime).TotalMilliseconds;
                return JsonSuccessResponse("Password not found!", processingTime);
            }
            catch (Exception ex)
            {
                DateTime endTime = DateTime.UtcNow;
                int processingTime = (int)(endTime - startTime).TotalMilliseconds;
                if (ex.Message.Contains("Password found!"))
                {
                    return JsonSuccessResponse(ex.Message, processingTime);
                }
                ILogService.LogError(logServices, $"Error while cracking using dictionary: {ex.Message}");
                return JsonErrorResponse(ex.Message, processingTime);
            }
        }

        private static JsonResult JsonSuccessResponse(string message, int processingTime)
        {
            object resultObject = new
            {
                Message = message,
                Status = 200,
                Time = processingTime
            };
            return new JsonResult(resultObject)
            {
                StatusCode = 200,
                ContentType = "application/json"
            };
        }

        private static JsonResult JsonErrorResponse(string errorMessage, int processingTime)
        {
            object resultObject = new
            {
                Message = $"An error occurred while cracking using dictionary: {errorMessage}",
                Status = 500,
                Time = processingTime
            };
            return new JsonResult(resultObject)
            {
                StatusCode = 500,
                ContentType = "application/json"
            };
        }

        private async Task CheckPasswordsAgainstDatabase(List<string> passwords, string username)
        {
            DateTime startTime = DateTime.UtcNow;
            int totalPasswords = passwords.Count;
            int checkedPasswords = 0;

            foreach (string password in passwords)
            {
                int elapsedTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                if (elapsedTime >= TimeLimit)
                {
                    ILogService.LogInfo(logServices, $"Dictionary check time limit reached ({TimeLimit}ms). Stopping after {checkedPasswords} passwords");
                    break;
                }
                checkedPasswords++;
                string hashedPassword = CalculateMD5Hash(password);
                // ILogService.LogInfo(logServices,
                //     $"Checking password [{checkedPasswords}/{totalPasswords}]: '{password}' (MD5: {hashedPassword})");
                bool isMatch = await _passwordRepository.CheckPassword(username, hashedPassword);
                if (isMatch)
                {
                    ILogService.LogInfo(logServices, $"Password found for user '{username}': {password}");
                    FoundPassword = password;
                    IsPasswordFound = true;
                    throw new Exception($"Password found! {password}");
                }
            }
        }

        private static string CalculateMD5Hash(string input)
        {
            using MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

        private async Task<(string username, ChunkInfo chunkInfo)> ReadAndDeserializeRequest(HttpContext httpContext)
        {
            using StreamReader reader = new(httpContext.Request.Body);
            string jsonBody = await reader.ReadToEndAsync();
            try
            {
                Dictionary<string, JsonElement> request = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBody)
                    ?? throw new Exception("Request body is empty");
                if (!request.TryGetValue("username", out JsonElement usernameElement))
                    throw new Exception("Username not found in request");
                if (!request.TryGetValue("chunk", out JsonElement chunkElement))
                    throw new Exception("Chunk information not found in request");
                string username = usernameElement.GetString()
                    ?? throw new Exception("Invalid username format");
                ChunkInfo chunkInfo = JsonSerializer.Deserialize<ChunkInfo>(chunkElement.GetRawText())
                    ?? throw new Exception("Invalid chunk information format");
                // ILogService.LogInfo(logServices, $"Received request for user: {username} with chunk: {chunkInfo.StartLine}-{chunkInfo.EndLine}");
                return (username, chunkInfo);
            }
            catch (JsonException ex)
            {
                throw new Exception($"Invalid JSON format: {ex.Message}");
            }
        }

        private async Task<List<string>> ReadPasswordsFromDictionary(ChunkInfo chunkInfo)
        {
            DictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
            SetDirectoryFiles();
            string latestDictionaryHash = GetLatestDictionaryHash("dictionary cracking");
            string dictionaryPath = Path.Combine(DictionaryDirectory, latestDictionaryHash);
            if (!File.Exists(dictionaryPath))
            {
                throw new FileNotFoundException($"Dictionary file not found at path: {dictionaryPath}");
            }
            List<string> selectedPasswords = new();
            using FileStream fileStream = new(dictionaryPath, FileMode.Open, FileAccess.Read);
            using StreamReader streamReader = new(fileStream);
            await SkipLinesToStartPosition(streamReader, chunkInfo.StartLine);
            await ReadRequiredLines(streamReader, selectedPasswords, chunkInfo);
            // ILogService.LogInfo(logServices, "Started cracking password ussing dictionary pack");
            return selectedPasswords;
        }

        private static async Task SkipLinesToStartPosition(StreamReader streamReader, int startLine)
        {
            int currentLine = 0;
            while (currentLine < startLine - 1 && await streamReader.ReadLineAsync() != null)
            {
                currentLine++;
            }
        }

        private static async Task ReadRequiredLines(StreamReader streamReader, List<string> selectedPasswords, ChunkInfo chunkInfo)
        {
            int currentLine = chunkInfo.StartLine - 1;
            string? line;
            while ((line = await streamReader.ReadLineAsync()) != null && currentLine < chunkInfo.EndLine)
            {
                currentLine++;
                selectedPasswords.Add(line);
            }
        }

        private static void ValidateSelectedPasswords(List<string> selectedPasswords, ChunkInfo chunkInfo)
        {
            if (selectedPasswords.Count == 0)
            {
                throw new Exception($"No words found between lines {chunkInfo.StartLine} and {chunkInfo.EndLine}");
            }
        }

        private void LogSuccessfulPasswordLoading(List<string> selectedPasswords, ChunkInfo chunkInfo)
        {
            // ILogService.LogInfo(logServices,
            //     $"Successfully loaded {selectedPasswords.Count} words from dictionary (lines {chunkInfo.StartLine}-{chunkInfo.EndLine})");
        }

        private async Task<string> HandleSaveFile(IFormFile? iFormFile)
        {
            bool isDictionaryExists = GetIsDictionaryExists(iFormFile);
            if (isDictionaryExists && iFormFile != null)
            {
                return iFormFile.FileName;
            }
            return await HandleSaveDictionaryPack(iFormFile);
        }

        private async Task<string> HandleSaveDictionaryPack(IFormFile? iFormFile)
        {
            if (iFormFile == null)
            {
                ILogService.LogError(logServices, "Dictionary file is null");
                throw new ArgumentNullException(nameof(iFormFile), "Dictionary file cannot be null");
            }
            string dictionaryLocation = Path.Combine(DictionaryDirectory, iFormFile.FileName);
            if (File.Exists(dictionaryLocation))
            {
                ILogService.LogInfo(logServices, $"Dictionary file '{dictionaryLocation}' already exists. Skipping save.");
                return dictionaryLocation;
            }
            using (FileStream fileStream = new(dictionaryLocation, FileMode.CreateNew, FileAccess.Write))
            {
                await iFormFile.CopyToAsync(fileStream);
                await fileStream.FlushAsync();
            }
            FileInfo fileInfo = new(dictionaryLocation);
            if (fileInfo.Length == 0)
            {
                File.Delete(dictionaryLocation);
                ILogService.LogError(logServices, "Created dictionary file is empty");
                throw new Exception("Failed to save dictionary - file is empty");
            }
            ILogService.LogInfo(logServices, $"Successfully saved new dictionary. Size: {fileInfo.Length} bytes");
            return dictionaryLocation;
        }

        private void HandleValidateFile(IFormFile? iFormFile)
        {
            bool isFormFileNotExists = iFormFile == null || iFormFile.Length == 0;
            bool isFormFileNameInvalid = iFormFile != null && !iFormFile.FileName.EndsWith(".txt");
            if (isFormFileNotExists || isFormFileNameInvalid)
            {
                ILogService.LogError(logServices, $"Received dictionary pack was invalid while trying to synchronize dictionary");
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

        private bool GetIsDictionaryExists(IFormFile? iFormFile)
        {
            try
            {
                if (iFormFile != null)
                {
                    string centralLatestDictionaryHash = iFormFile.FileName;
                    string calculatingLatestDictionaryHash = GetLatestDictionaryHash("synchronizing dictionary");
                    return calculatingLatestDictionaryHash.Equals(centralLatestDictionaryHash);
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void SetDirectoryFiles()
        {
            string[] directoryFiles = Directory.GetFiles(DictionaryDirectory);
            if (directoryFiles.Length <= 0)
            {
                ILogService.LogError(logServices, $"No files found in directory path while trying to start dictionary cracking");
                throw new Exception("No files found in directory path.");
            }
            DirectoryFiles = directoryFiles;
        }

        private string GetLatestDictionaryHash(string operation)
        {
            FileInfo? latestFile = DirectoryFiles
                .Select(file => new FileInfo(file))
                .OrderByDescending(fileInfo => fileInfo.CreationTime)
                .FirstOrDefault();
            if (latestFile == null)
            {
                ILogService.LogError(logServices, $"No valid dictionary hash files found while trying to ${operation} dictionary");
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
    }
}