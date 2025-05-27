using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using backend___central.Models;
using backend___central.Interfaces;

namespace backend___central.Services
{
    public class BruteForceCrackingService : IBruteForceCrackingService
    {
        private readonly IEnumerable<ILogService> logServices;
        private readonly IServerCommunicationService serverCommunicationService;
        private readonly IResponseProcessingService responseProcessingService;

        public BruteForceCrackingService(
            IEnumerable<ILogService> logServices,
            IServerCommunicationService serverCommunicationService,
            IResponseProcessingService responseProcessingService)
        {
            this.logServices = logServices;
            this.serverCommunicationService = serverCommunicationService;
            this.responseProcessingService = responseProcessingService;
        }

        public async Task<IActionResult> HandleBruteForceRequest(HttpContext httpContext)
        {
            DateTime startTime = DateTime.UtcNow;
            LogBruteForceStart();
            try
            {
                string bodyContent = await ReadRequestBody(httpContext);
                BruteForceRequestData credentials = ParseAndValidateRequest(bodyContent);
                CrackingCharPackage charPackage = DistributeCharacters();
                ServerTaskResult taskResult = await ExecuteTasks(charPackage, credentials);
                int totalTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                CrackingResult? successResult = taskResult.Results.FirstOrDefault(r => r.Success);
                bool passwordFound = successResult != null && successResult.Success;
                string? serverIp = successResult?.ServerIp;
                return responseProcessingService.ProcessResults(taskResult, credentials, charPackage, totalTime);
            }
            catch (Exception ex)
            {
                int errorTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                return responseProcessingService.HandleError(ex, errorTime);
            }
        }

        private void LogBruteForceStart()
        {
            ILogService.LogInfo(logServices, "Made request to crack password using brute force method");
        }

        private async Task<string> ReadRequestBody(HttpContext httpContext)
        {
            using StreamReader reader = new(httpContext.Request.Body);
            string bodyContent = await reader.ReadToEndAsync();
            ILogService.LogInfo(logServices, bodyContent);
            return bodyContent;
        }

        public BruteForceRequestData ParseAndValidateRequest(string bodyContent)
        {
            DateTime parseStartTime = DateTime.UtcNow;
            using JsonDocument document = JsonDocument.Parse(bodyContent);
            JsonElement root = document.RootElement;
            ValidateRequiredFields(root, out JsonElement passwordLengthElement, out JsonElement userLoginElement);
            ValidatePasswordLength(passwordLengthElement, out int passwordLength);
            string userLogin = userLoginElement.GetString() ?? string.Empty;
            DateTime distributionStartTime = DateTime.UtcNow;
            int parseTime = (int)(distributionStartTime - parseStartTime).TotalMilliseconds;
            ILogService.LogInfo(logServices, $"Request parsing completed in {parseTime}ms");
            return new BruteForceRequestData(parseTime, userLogin, passwordLength);
        }

        private void ValidateRequiredFields(JsonElement root, out JsonElement passwordLengthElement, out JsonElement userLoginElement)
        {
            if (!root.TryGetProperty("passwordLength", out passwordLengthElement) || !root.TryGetProperty("userLogin", out userLoginElement))
            {
                ILogService.LogError(logServices, "Invalid request data: Missing required fields");
                throw new ArgumentException("Invalid request data. Missing required fields.");
            }
        }

        private void ValidatePasswordLength(JsonElement passwordLengthElement, out int passwordLength)
        {
            if (!passwordLengthElement.TryGetInt32(out passwordLength))
            {
                ILogService.LogError(logServices, "Invalid request data: passwordLength must be an integer");
                throw new ArgumentException("Invalid request data. passwordLength must be an integer.");
            }
        }

        public CrackingCharPackage DistributeCharacters()
        {
            string allChars = GetAllAvailableCharacters();
            int totalCharCount = allChars.Length;
            int granularity = GetGranularity();
            int portionSize = CalculatePortionSize(totalCharCount, granularity);
            List<string> charPortions = CreateCharacterPortions(allChars, totalCharCount, portionSize);
            return new CrackingCharPackage(charPortions);
        }

        private static string GetAllAvailableCharacters()
        {
            return "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        }

        private static int GetGranularity()
        {
            return Startup.BruteForceGranularity > 0 ? Startup.BruteForceGranularity : 5;
        }

        private static int CalculatePortionSize(int totalCharCount, int granularity)
        {
            return Math.Max(1, totalCharCount / granularity);
        }

        private static List<string> CreateCharacterPortions(string allChars, int totalCharCount, int portionSize)
        {
            List<string> charPortions = new();
            for (int i = 0; i < totalCharCount; i += portionSize)
            {
                int currentPortionSize = Math.Min(portionSize, totalCharCount - i);
                string portion = allChars.Substring(i, currentPortionSize);
                charPortions.Add(portion);
            }
            return charPortions;
        }

        public async Task<ServerTaskResult> ExecuteTasks(CrackingCharPackage charPackage, BruteForceRequestData credentials)
        {
            DateTime tasksStartTime = DateTime.UtcNow;
            List<string> serverIPs = GetServerIPs();
            ValidateServerCount(serverIPs);
            List<Task<CrackingResult>> tasks = await serverCommunicationService.CreateTasksForPortions(
                charPackage.CharPortions,
                credentials.PasswordLength,
                credentials.UserLogin,
                serverIPs);
            DateTime awaitStartTime = DateTime.UtcNow;
            int taskSetupTime = (int)(awaitStartTime - tasksStartTime).TotalMilliseconds;
            CrackingResult[] results = await ExecuteAllTasks(tasks);
            DateTime resultsTime = DateTime.UtcNow;
            int processingTime = (int)(resultsTime - awaitStartTime).TotalMilliseconds;
            LogTaskProcessingCompleted(results.Length, serverIPs.Count, processingTime);
            return new ServerTaskResult(taskSetupTime, processingTime, results);
        }

        private static List<string> GetServerIPs()
        {
            return Startup.ServersIpAddresses.Select(ip => ip.ToString()).ToList();
        }

        private static void ValidateServerCount(List<string> serverIPs)
        {
            if (serverIPs.Count == 0)
            {
                throw new InvalidOperationException("No servers available for processing.");
            }
        }

        private static async Task<CrackingResult[]> ExecuteAllTasks(List<Task<CrackingResult>> tasks)
        {
            return await Task.WhenAll(tasks);
        }

        private void LogTaskProcessingCompleted(int completedTasksCount, int serverCount, int processingTime)
        {
            ILogService.LogInfo(logServices,
                $"[BruteForce] Processing: Total = {processingTime} ms | " +
                $"Tasks completed = {completedTasksCount} | " +
                $"Servers used = {serverCount}");
        } 
    }
}