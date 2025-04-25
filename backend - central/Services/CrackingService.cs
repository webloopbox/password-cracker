using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using backend___central.Models;

namespace backend___central.Services
{
    public class CrackingService : ICrackingService
    {
        private volatile bool passwordFound;
        private readonly IEnumerable<ILogService> logServices;
        private readonly CheckService checkService;
        private readonly ChunkManagerService chunkManager;
        private readonly ServerManagerService serverManager;
        private readonly TaskCoordinatorService taskCoordinator;

        public CrackingService(IEnumerable<ILogService> logServices, CheckService checkService)
        {
            passwordFound = false;
            this.logServices = logServices;
            this.checkService = checkService;
            chunkManager = new ChunkManagerService(logServices);
            serverManager = new ServerManagerService(logServices);
            taskCoordinator = new TaskCoordinatorService(logServices);
        }

        public async Task<IActionResult> HandlBruteForceRequest(HttpContext httpContext)
        {
            DateTime startTime = DateTime.UtcNow; 
            ILogService.LogInfo(logServices, "Made request to crack password using brute force method");
        
            try
            {
                // Read request body
                string bodyContent = await ReadRequestBody(httpContext);
                
                // Parse and validate request
                var (passwordLength, userLogin, parseTime) = await ParseAndValidateRequest(bodyContent, startTime);
                
                // Distribute characters across servers
                var (charPortions, distributionTime) = DistributeCharacters(parseTime, startTime);
                
                // Create and execute server tasks
                var (results, taskSetupTime, processingTime) = await CreateAndExecuteServerTasks(
                    charPortions, passwordLength, userLogin, startTime, distributionTime);
                
                // Process results and create response
                int totalTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                return ProcessResults(results, totalTime, parseTime, distributionTime, taskSetupTime, processingTime);
            }
            catch (Exception ex)
            {
                return HandleBruteForceError(ex, startTime);
            }
        }
        
        private async Task<string> ReadRequestBody(HttpContext httpContext)
        {
            using StreamReader reader = new(httpContext.Request.Body);
            string bodyContent = await reader.ReadToEndAsync();
            
            Console.WriteLine($"Request body content: {bodyContent}");
            ILogService.LogInfo(logServices, bodyContent);
            
            return bodyContent;
        }
        
        private async Task<(int passwordLength, string userLogin, int parseTime)> ParseAndValidateRequest(
            string bodyContent, DateTime startTime)
        {
            DateTime parseStartTime = DateTime.UtcNow;
            
            using JsonDocument document = JsonDocument.Parse(bodyContent);
            JsonElement root = document.RootElement;
            
            if (!root.TryGetProperty("passwordLength", out JsonElement passwordLengthElement) ||
                !root.TryGetProperty("userLogin", out JsonElement userLoginElement))
            {
                ILogService.LogError(logServices, "Invalid request data: Missing required fields");
                throw new ArgumentException("Invalid request data. Missing required fields.");
            }
            
            if (!passwordLengthElement.TryGetInt32(out int passwordLength))
            {
                ILogService.LogError(logServices, "Invalid request data: passwordLength must be an integer");
                throw new ArgumentException("Invalid request data. passwordLength must be an integer.");
            }
        
            string userLogin = userLoginElement.GetString() ?? string.Empty;
            
            DateTime distributionStartTime = DateTime.UtcNow;
            int parseTime = (int)(distributionStartTime - parseStartTime).TotalMilliseconds;
            ILogService.LogInfo(logServices, $"Request parsing completed in {parseTime}ms");
            
            return (passwordLength, userLogin, parseTime);
        }
        
        private (List<string> charPortions, int distributionTime) DistributeCharacters(
            int parseTime, DateTime startTime)
        {
            DateTime distributionStartTime = DateTime.UtcNow;
            int hostsCount = Startup.ServersIpAddresses.Count;
            
            Console.WriteLine($"Number of hosts: {hostsCount}");
            string allChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            int portionSize = allChars.Length / hostsCount;
            
            List<string> charPortions = Enumerable.Range(0, hostsCount)
                .Select(i => allChars.Substring(i * portionSize, Math.Min(portionSize, allChars.Length - i * portionSize)))
                .ToList();
            
            Console.WriteLine($"Server IP Addresses: {string.Join(", ", Startup.ServersIpAddresses)}");
            
            DateTime tasksStartTime = DateTime.UtcNow;
            int distributionTime = (int)(tasksStartTime - distributionStartTime).TotalMilliseconds;
            ILogService.LogInfo(logServices, $"Character distribution completed in {distributionTime}ms - {hostsCount} portions created");
            
            return (charPortions, distributionTime);
        }
        
        private async Task<(IEnumerable<(bool Success, string? Password, string ServerIp, int Time)> results, 
            int taskSetupTime, int processingTime)> CreateAndExecuteServerTasks(
            List<string> charPortions, int passwordLength, string userLogin, 
            DateTime startTime, int distributionTime)
        {
            DateTime tasksStartTime = DateTime.UtcNow;
            
            // Create tasks for all servers
            List<Task<(bool Success, string? Password, string ServerIp, int Time)>> tasks = new();
            
            for (int i = 0; i < Startup.ServersIpAddresses.Count; i++)
            {
                string serverIpAddress = Startup.ServersIpAddresses[i].ToString();
                await ValidateServerConnection(serverIpAddress);
                
                string charRange = charPortions[i];
                tasks.Add(CreateServerTask(serverIpAddress, passwordLength, userLogin, charRange));
            }
            
            DateTime awaitStartTime = DateTime.UtcNow;
            int taskSetupTime = (int)(awaitStartTime - tasksStartTime).TotalMilliseconds;
            ILogService.LogInfo(logServices, $"All {tasks.Count} tasks created in {taskSetupTime}ms - now waiting for completion");
            
            var results = await Task.WhenAll(tasks);
            
            DateTime resultsTime = DateTime.UtcNow;
            int processingTime = (int)(resultsTime - awaitStartTime).TotalMilliseconds;
            ILogService.LogInfo(logServices, $"[BruteForce] Processing: Total = {processingTime} ms | Servers completed = {results.Length}");
            
            return (results, taskSetupTime, processingTime);
        }
        
        private async Task ValidateServerConnection(string serverIpAddress)
        {
            ILogService.LogInfo(logServices, $"Checking connection to server {serverIpAddress}");
            await checkService.HandleCheckIfCanConnectToCalculatingServer(System.Net.IPAddress.Parse(serverIpAddress));
            ILogService.LogInfo(logServices, $"Successfully connected to server {serverIpAddress}");
        }
        
        private Task<(bool Success, string? Password, string ServerIp, int Time)> CreateServerTask(
            string serverIpAddress, int passwordLength, string userLogin, string chars)
        {
            object payload = new
            {
                passwordLength,
                userLogin,
                chars
            };
        
            string payloadJson = JsonSerializer.Serialize(payload);
            ILogService.LogInfo(logServices, $"Sending request to server {serverIpAddress} with character range: {chars.First()}-{chars.Last()}");
        
            return Task.Run(async () =>
            {
                DateTime serverStartTime = DateTime.UtcNow;
                try
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };
                    ILogService.LogInfo(logServices, $"Sending brute force request to {serverIpAddress}");
                    
                    var response = await httpClient.PostAsync(
                        $"http://{serverIpAddress}:5099/api/synchronizing/brute-force",
                        new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
                    );
            
                    DateTime serverResponseTime = DateTime.UtcNow;
                    int serverRequestTime = (int)(serverResponseTime - serverStartTime).TotalMilliseconds;
                    
                    return await ProcessServerResponse(response, serverIpAddress, serverRequestTime);
                }
                catch (Exception ex)
                {
                    DateTime errorTime = DateTime.UtcNow;
                    int errorDuration = (int)(errorTime - serverStartTime).TotalMilliseconds;
                    ILogService.LogError(logServices, 
                        $"[BruteForce] Server {serverIpAddress}: Error | Communication time = {errorDuration} ms | Error: {ex.Message}");
                    return (Success: false, Password: (string?)null, ServerIp: serverIpAddress, Time: -1);
                }
            });
        }
        
                private async Task<(bool Success, string? Password, string ServerIp, int Time)> ProcessServerResponse(
            HttpResponseMessage response, string serverIpAddress, int serverRequestTime)
        {
            // Log initial response received
            ILogService.LogInfo(logServices, 
                $"[BruteForce] Server {serverIpAddress}: Response received | Communication time = {serverRequestTime} ms");
        
            if (!response.IsSuccessStatusCode)
            {
                ILogService.LogError(logServices, $"Failed to synchronize with server {serverIpAddress}. Status code: {response.StatusCode}");
                return (Success: false, Password: (string?)null, ServerIp: serverIpAddress, Time: -1);
            }
        
            string responseContent = await response.Content.ReadAsStringAsync();
            
            // Parse calculation time from response
            int calculatingServerTime = -1;
            try
            {
                using JsonDocument document = JsonDocument.Parse(responseContent);
                if (document.RootElement.TryGetProperty("time", out JsonElement timeElement) && 
                    timeElement.ValueKind == JsonValueKind.Number)
                {
                    calculatingServerTime = timeElement.GetInt32();
                }
                else if (document.RootElement.TryGetProperty("calculationTime", out JsonElement calcTimeElement) && 
                        calcTimeElement.ValueKind == JsonValueKind.Number)
                {
                    calculatingServerTime = calcTimeElement.GetInt32();
                }
            }
            catch (JsonException)
            {
                ILogService.LogInfo(logServices, $"Response from server {serverIpAddress} is not in JSON format");
            }
        
            // Calculate communication time
            int communicationTime = calculatingServerTime > 0 ? serverRequestTime - calculatingServerTime : serverRequestTime;
        
            // Log in the standardized format
            ILogService.LogInfo(logServices, 
                $"[BruteForce] Central: Total = {serverRequestTime} ms" + 
                (calculatingServerTime > 0 ? $" | Calculating: ({serverIpAddress}) Total = {calculatingServerTime} ms" : "") + 
                $" | Communication time = {communicationTime} ms");
        
            var responseData = JsonSerializer.Deserialize<BruteForceResponse>(responseContent);
            if (responseData == null)
            {
                ILogService.LogError(logServices, $"Invalid response from server {serverIpAddress}: Could not parse response");
                return (Success: false, Password: (string?)null, ServerIp: serverIpAddress, Time: -1);
            }
        
            if (responseData.Message == "Password found." && !string.IsNullOrEmpty(responseData.Password))
            {
                // Use the standard log format for consistency
                ILogService.LogInfo(logServices, $"Server {serverIpAddress} completed processing: {responseContent}");
                return (Success: true, responseData.Password, ServerIp: serverIpAddress, Time: calculatingServerTime > 0 ? calculatingServerTime : responseData.Time);
            }
        
            // Use the standard log format for consistency
            ILogService.LogInfo(logServices, $"Server {serverIpAddress} completed processing: {responseContent}");
            return (Success: false, Password: (string?)null, ServerIp: serverIpAddress, Time: calculatingServerTime > 0 ? calculatingServerTime : responseData.Time);
        }
        
        private IActionResult ProcessResults(
            IEnumerable<(bool Success, string? Password, string ServerIp, int Time)> results, 
            int totalTime, int parseTime, int distributionTime, int taskSetupTime, int processingTime)
        {
            var successfulResult = results.FirstOrDefault(r => r.Success);
            
            if (successfulResult.Success)
            {
                return CreateSuccessResponse(successfulResult, totalTime, parseTime, distributionTime, taskSetupTime, processingTime);
            }
            
            return CreateNotFoundResponse(results, totalTime, parseTime, distributionTime, taskSetupTime, processingTime);
        }
        
        private IActionResult CreateSuccessResponse(
            (bool Success, string? Password, string ServerIp, int Time) successfulResult, 
            int totalTime, int parseTime, int distributionTime, int taskSetupTime, int processingTime)
        {
            int communicationTime = totalTime - successfulResult.Time;
            
            // Use the consistent format from HandleSuccessfulResponse
            ILogService.LogInfo(logServices, 
                $"[BruteForce] Central: Total = {totalTime} ms" + 
                $" | Calculating: ({successfulResult.ServerIp}) Total = {successfulResult.Time} ms" + 
                $" | Communication time = {communicationTime} ms");
                
            return new OkObjectResult(new
            {
                Message = "Password found.",
                Password = successfulResult.Password,
                Server = successfulResult.ServerIp,
                ServerExecutionTime = successfulResult.Time,
                TotalExecutionTime = totalTime,
                CommunicationTime = communicationTime,
                Timing = new
                {
                    ParseTime = parseTime,
                    DistributionTime = distributionTime,
                    TaskSetupTime = taskSetupTime,
                    ProcessingTime = processingTime,
                    TotalTime = totalTime
                }
            });
        }
        
        private IActionResult CreateNotFoundResponse(
            IEnumerable<(bool Success, string? Password, string ServerIp, int Time)> results, 
            int totalTime, int parseTime, int distributionTime, int taskSetupTime, int processingTime)
        {
            var validTimes = results.Where(r => r.Time != -1).ToList();
            int avgServerTime = (int)(validTimes.Any() ? validTimes.Average(r => r.Time) : 0);
            int avgCommunicationTime = totalTime - avgServerTime;
            
            // Use the consistent format from HandleSuccessfulResponse
            ILogService.LogInfo(logServices, 
                $"[BruteForce] Central: Total = {totalTime} ms" + 
                (avgServerTime > 0 ? $" | Calculating: (average) Total = {avgServerTime} ms" : "") + 
                $" | Communication time = {avgCommunicationTime} ms");
            
            return new NotFoundObjectResult(new
            {
                Message = "Password not found by any server.",
                TotalExecutionTime = totalTime,
                AverageServerTime = avgServerTime,
                CommunicationTime = avgCommunicationTime,
                ServersTimes = results.Where(r => r.Time != -1)
                    .Select(r => new { Server = r.ServerIp, Time = r.Time })
                    .ToList(),
                Timing = new
                {
                    ParseTime = parseTime,
                    DistributionTime = distributionTime,
                    TaskSetupTime = taskSetupTime,
                    ProcessingTime = processingTime,
                    TotalTime = totalTime
                }
            });
        }
        
        private IActionResult HandleBruteForceError(Exception ex, DateTime startTime)
        {
            int errorTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            ILogService.LogError(logServices, $"Cannot connect to calculating server due to: {ex.Message} (after {errorTime}ms)");
            
            return new ContentResult
            {
                Content = $"An error occurred while trying to connect calculating server: {ex.Message}",
                ContentType = "text/plain",
                StatusCode = 500
            };
        }

        public async Task<IActionResult> HandleDictionaryCracking(HttpContext httpContext)
        {
            try
            {
                int currentLine = 1;
                string username = await ExtractUsername(httpContext);
                int totalLines = await chunkManager.GetDictionaryTotalLines();
                serverManager.ValidateServersAvailability();
                List<CalculatingServerState> serverStates = serverManager.InitializeServerStates();
                while (currentLine <= totalLines && !passwordFound)
                {
                    List<CalculatingServerState> availableServers = serverManager.GetAvailableServers(serverStates);
                    if (!availableServers.Any())
                    {
                        await HandleNoAvailableServers();
                        continue;
                    }
                    currentLine = await ProcessServers(availableServers, currentLine, totalLines, username);
                }
                return CreateFinalResponse();
            }
            catch (Exception ex)
            {
                return HandleCrackingError(ex);
            }
        }

        private static async Task<string> ExtractUsername(HttpContext httpContext)
        {
            IFormCollection form = await httpContext.Request.ReadFormAsync();
            return form["username"].ToString() ??
                throw new ArgumentException("Username is required");
        }

        private async Task<int> ProcessServers(List<CalculatingServerState> servers, int currentLine, int totalLines, string username)
        {
            taskCoordinator.ResetState();
            while (currentLine <= totalLines && !passwordFound)
            {
                foreach (CalculatingServerState server in servers.ToList())
                {
                    if (!taskCoordinator.CanProcessServer(server))
                        continue;
                    if (currentLine > totalLines || passwordFound)
                        break;
                    try
                    {
                        currentLine = AssignChunkToServer(server, currentLine, totalLines, username);
                    }
                    catch (Exception ex)
                    {
                        HandleServerAssignmentError(server, servers, ex);
                    }
                }
                if (await taskCoordinator.ProcessCompletedTasks())
                {
                    passwordFound = true;
                    break;
                }
            }
            if (passwordFound)
                taskCoordinator.CancelAllTasks();
            return currentLine;
        }

        private int AssignChunkToServer(CalculatingServerState server, int currentLine, int totalLines, string username)
        {
            DateTime firstDateTime = DateTime.UtcNow;
            Chunk chunk = chunkManager.CreateChunk(currentLine, totalLines, firstDateTime);
            server.IsBusy = true;
            taskCoordinator.AddServerTask(server, chunk, username);
            currentLine += Startup.Granularity;
            ILogService.LogInfo(logServices,
                $"Assigned chunk {currentLine - Startup.Granularity}-{currentLine} to server {server.IpAddress}");
            return currentLine;
        }

        private void HandleServerAssignmentError(CalculatingServerState server, List<CalculatingServerState> servers, Exception ex)
        {
            ILogService.LogError(logServices,
                $"Failed to assign chunk to server {server.IpAddress}: {ex.Message}");
            serverManager.MarkServerAsFailed(server);
            server.IsBusy = false;
            servers.Remove(server);
        }

        private static async Task HandleNoAvailableServers()
        {
            if (Startup.ServersIpAddresses.Count == 0)
            {
                throw new Exception("All calculating servers have failed. Stopping dictionary cracking.");
            }
            await Task.Delay(1000);
        }

        private IActionResult CreateFinalResponse()
        {
            var resultObject = new
            {
                Message = passwordFound 
                    ? "Password cracking completed successfully." 
                    : "Password not found in dictionary.",
                Status = passwordFound ? "Found" : "NotFound",
            };
                    
            return new JsonResult(resultObject)
            {
                StatusCode = 200,
                ContentType = "application/json"
            };
        }
        
        private IActionResult HandleCrackingError(Exception ex)
        {
            ILogService.LogError(logServices, $"Error during dictionary cracking: {ex.Message}");
                    
            var resultObject = new
            {
                Message = $"An error occurred while cracking password: {ex.Message}",
                StatusCode = 500,
                Time = -1
            };
                    
            return new JsonResult(resultObject)
            {
                StatusCode = 500,
                ContentType = "application/json"
            };
        }

        private class BruteForceResponse
        {
            [JsonPropertyName("Message")]
            public string Message { get; set; } = string.Empty; 

            [JsonPropertyName("Password")]
            public string? Password { get; set; }

            [JsonPropertyName("Time")]
            public int Time { get; set; }
        }
    }
}