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
        
            using StreamReader reader = new(httpContext.Request.Body);
            string bodyContent = await reader.ReadToEndAsync();
        
            Console.WriteLine($"Request body content: {bodyContent}");
        
            try
            {
                ILogService.LogInfo(logServices, bodyContent);
        
                // Parse and validate request
                DateTime parseStartTime = DateTime.UtcNow;
                using JsonDocument document = JsonDocument.Parse(bodyContent);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("passwordLength", out JsonElement passwordLengthElement) ||
                    !root.TryGetProperty("userLogin", out JsonElement userLoginElement))
                {
                    ILogService.LogError(logServices, "Invalid request data: Missing required fields");
                    return new ContentResult
                    {
                        Content = "Invalid request data. Missing required fields.",
                        ContentType = "text/plain",
                        StatusCode = 400
                    };
                }
                if (!passwordLengthElement.TryGetInt32(out int passwordLength))
                {
                    ILogService.LogError(logServices, "Invalid request data: passwordLength must be an integer");
                    return new ContentResult
                    {
                        Content = "Invalid request data. passwordLength must be an integer.",
                        ContentType = "text/plain",
                        StatusCode = 400
                    };
                }
        
                string userLogin = userLoginElement.GetString() ?? string.Empty;
                int hostsCount = Startup.ServersIpAddresses.Count;
        
                // Distribution setup time
                DateTime distributionStartTime = DateTime.UtcNow;
                int parseTime = (int)(distributionStartTime - parseStartTime).TotalMilliseconds;
                ILogService.LogInfo(logServices, $"Request parsing completed in {parseTime}ms");
                
                Console.WriteLine($"Number of hosts: {hostsCount}");
                string allChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                int portionSize = allChars.Length / hostsCount;
                List<string> charPortions = Enumerable.Range(0, hostsCount)
                                             .Select(i => allChars.Substring(i * portionSize, Math.Min(portionSize, allChars.Length - i * portionSize)))
                                             .ToList();
        
                using HttpClient httpClient = new();
                httpClient.Timeout = TimeSpan.FromHours(2);
        
                Console.WriteLine($"Server IP Addresses: {string.Join(", ", Startup.ServersIpAddresses)}");
                
                DateTime tasksStartTime = DateTime.UtcNow;
                int distributionTime = (int)(tasksStartTime - distributionStartTime).TotalMilliseconds;
                ILogService.LogInfo(logServices, $"Character distribution completed in {distributionTime}ms - {hostsCount} portions created");
        
                // Create tasks for all servers
                List<Task<(bool Success, string? Password, string ServerIp, int Time)>> tasks = new();
                for (int i = 0; i < Startup.ServersIpAddresses.Count; i++)
                {
                    string serverIpAddress = Startup.ServersIpAddresses[i].ToString();
                    ILogService.LogInfo(logServices, $"Checking connection to server {serverIpAddress}");
                    await checkService.HandleCheckIfCanConnectToCalculatingServer(System.Net.IPAddress.Parse(serverIpAddress));
                    ILogService.LogInfo(logServices, $"Successfully connected to server {serverIpAddress}");
        
                    object payload = new
                    {
                        passwordLength,
                        userLogin,
                        chars = charPortions[i]
                    };
        
                    string payloadJson = JsonSerializer.Serialize(payload);
                    ILogService.LogInfo(logServices, $"Sending request to server {serverIpAddress} with character range: {charPortions[i].First()}-{charPortions[i].Last()}");
        
                    tasks.Add(Task.Run(async () =>
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
                            ILogService.LogInfo(logServices, $"Received response from {serverIpAddress} in {serverRequestTime}ms");
        
                            if (!response.IsSuccessStatusCode)
                            {
                                ILogService.LogError(logServices, $"Failed to synchronize with server {serverIpAddress}. Status code: {response.StatusCode}");
                                return (Success: false, Password: (string?)null, ServerIp: serverIpAddress, Time: -1);
                            }
        
                            string responseContent = await response.Content.ReadAsStringAsync();
                            var responseData = JsonSerializer.Deserialize<BruteForceResponse>(responseContent);
        
                            if (responseData == null || responseData.Time == -1)
                            {
                                ILogService.LogError(logServices, $"Invalid response from server {serverIpAddress}: Invalid execution time");
                                return (Success: false, Password: (string?)null, ServerIp: serverIpAddress, Time: -1);
                            }
        
                            if (responseData.Message == "Password found." && !string.IsNullOrEmpty(responseData.Password))
                            {
                                ILogService.LogInfo(logServices,
                                    $"Password found by server {serverIpAddress}: {responseData.Password} in {responseData.Time}ms");
                                return (Success: true, responseData.Password, ServerIp: serverIpAddress, Time: responseData.Time);
                            }
        
                            ILogService.LogInfo(logServices,
                                $"No password found by server {serverIpAddress}. Time taken: {responseData.Time}ms");
                            return (Success: false, Password: (string?)null, ServerIp: serverIpAddress, Time: responseData.Time);
                        }
                        catch (Exception ex)
                        {
                            DateTime errorTime = DateTime.UtcNow;
                            int errorDuration = (int)(errorTime - serverStartTime).TotalMilliseconds;
                            ILogService.LogError(logServices, $"Error communicating with server {serverIpAddress} after {errorDuration}ms: {ex.Message}");
                            return (Success: false, Password: (string?)null, ServerIp: serverIpAddress, Time: -1);
                        }
                    }));
                }
        
                DateTime awaitStartTime = DateTime.UtcNow;
                int taskSetupTime = (int)(awaitStartTime - tasksStartTime).TotalMilliseconds;
                ILogService.LogInfo(logServices, $"All {tasks.Count} tasks created in {taskSetupTime}ms - now waiting for completion");
        
                var results = await Task.WhenAll(tasks);
                
                DateTime resultsTime = DateTime.UtcNow;
                int processingTime = (int)(resultsTime - awaitStartTime).TotalMilliseconds;
                ILogService.LogInfo(logServices, $"All servers completed in {processingTime}ms");
                
                var successfulResult = results.FirstOrDefault(r => r.Success);
                int totalTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        
                if (successfulResult.Success)
                {
                    ILogService.LogInfo(logServices, $"Total brute force operation completed successfully in {totalTime}ms");
                    return new OkObjectResult(new
                    {
                        Message = "Password found.",
                        Password = successfulResult.Password,
                        Server = successfulResult.ServerIp,
                        ServerExecutionTime = successfulResult.Time,
                        TotalExecutionTime = totalTime,
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
        
                ILogService.LogInfo(logServices, $"Total brute force operation completed with no password found in {totalTime}ms");
                return new NotFoundObjectResult(new
                {
                    Message = "Password not found by any server.",
                    TotalExecutionTime = totalTime,
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
            catch (Exception ex)
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