using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using backend___central.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

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
            ILogService.LogInfo(logServices, "Made request to crack password using brute force method");

            using StreamReader reader = new(httpContext.Request.Body);
            string bodyContent = await reader.ReadToEndAsync();

            Console.WriteLine($"Request body content: {bodyContent}");

            try
            {
                ILogService.LogInfo(logServices, bodyContent);

                // Deserialize the JSON directly into a JsonDocument to safely handle JsonElement
                using JsonDocument document = JsonDocument.Parse(bodyContent);
                JsonElement root = document.RootElement;

                // Validate required fields
                if (!root.TryGetProperty("passwordLength", out JsonElement passwordLengthElement) ||
                    !root.TryGetProperty("userLogin", out JsonElement userLoginElement))
                {
                    return new ContentResult
                    {
                        Content = "Invalid request data. Missing required fields.",
                        ContentType = "text/plain",
                        StatusCode = 400
                    };
                }

                // Extract values safely
                if (!passwordLengthElement.TryGetInt32(out int passwordLength))
                {
                    return new ContentResult
                    {
                        Content = "Invalid request data. passwordLength must be an integer.",
                        ContentType = "text/plain",
                        StatusCode = 400
                    };
                }

                string userLogin = userLoginElement.GetString() ?? string.Empty;

                // Get the number of hosts dynamically
                int hostsCount = Startup.ServersIpAddresses.Count;

                Console.WriteLine($"Number of hosts: {hostsCount}");

                // Generate character portions based on granularity and hosts count
                var allChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                int portionSize = allChars.Length / hostsCount;
                var charPortions = Enumerable.Range(0, hostsCount)
                                             .Select(i => allChars.Substring(i * portionSize, Math.Min(portionSize, allChars.Length - i * portionSize)))
                                             .ToList();

                using HttpClient httpClient = new();
                httpClient.Timeout = TimeSpan.FromHours(2);

                Console.WriteLine($"Server IP Addresses: {string.Join(", ", Startup.ServersIpAddresses)}");

                var tasks = new List<Task<(bool Success, string? Password, string ServerIp)>>();
                for (int i = 0; i < Startup.ServersIpAddresses.Count; i++)
                {
                    string serverIpAddress = Startup.ServersIpAddresses[i].ToString();
                    await checkService.HandleCheckIfCanConnectToCalculatingServer(System.Net.IPAddress.Parse(serverIpAddress));

                    Console.WriteLine($"Making request to --> http://{serverIpAddress}:5099/api/synchronizing/brute-force");

                    var payload = new
                    {
                        passwordLength,
                        userLogin,
                        chars = charPortions[i]
                    };

                    string payloadJson = JsonSerializer.Serialize(payload);

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var response = await httpClient.PostAsync(
                                $"http://{serverIpAddress}:5099/api/synchronizing/brute-force",
                                new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
                            );

                            if (!response.IsSuccessStatusCode)
                            {
                                ILogService.LogError(logServices, $"Failed to synchronize with server {serverIpAddress}. Status code: {response.StatusCode}");
                                return (Success: false, Password: null, ServerIp: serverIpAddress);
                            }

                            string responseContent = await response.Content.ReadAsStringAsync();
                            try
                            {
                                var responseData = JsonSerializer.Deserialize<BruteForceResponse>(responseContent);
                                if (responseData != null && responseData.Message == "Password found." && !string.IsNullOrEmpty(responseData.Password))
                                {
                                    ILogService.LogInfo(logServices, $"Password found by server {serverIpAddress}: {responseData.Password}");
                                    return (Success: true, responseData.Password, ServerIp: serverIpAddress);
                                }
                                else
                                {
                                    ILogService.LogInfo(logServices, $"No password found by server {serverIpAddress}.");
                                    return (Success: false, Password: null, ServerIp: serverIpAddress);
                                }
                            }
                            catch (JsonException ex)
                            {
                                ILogService.LogError(logServices, $"Failed to parse response from server {serverIpAddress}: {ex.Message}");
                                return (Success: false, Password: null, ServerIp: serverIpAddress);
                            }
                        }
                        catch (Exception ex)
                        {
                            ILogService.LogError(logServices, $"Error communicating with server {serverIpAddress}: {ex.Message}");
                            return (Success: false, Password: null, ServerIp: serverIpAddress);
                        }
                    }));
                }

                var results = await Task.WhenAll(tasks);

                var successfulResult = results.FirstOrDefault(r => r.Success);
                if (successfulResult.Success)
                {
                    return new OkObjectResult(new
                    {
                        Message = "Password found.",
                        Password = successfulResult.Password,
                        Server = successfulResult.ServerIp
                    });
                }

                return new NotFoundObjectResult(new
                {
                    Message = "Password not found by any server."
                });
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Cannot connect to calculating server due to: {ex.Message}");
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
            object chunk = chunkManager.CreateChunk(currentLine, totalLines);
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

        private ContentResult CreateFinalResponse()
        {
            return new ContentResult
            {
                Content = passwordFound ?
                    "Password cracking completed successfully." :
                    "Password not found in dictionary.",
                StatusCode = 200
            };
        }

        private ContentResult HandleCrackingError(Exception ex)
        {
            ILogService.LogError(logServices, $"Error during dictionary cracking: {ex.Message}");
            return new ContentResult
            {
                Content = $"An error occurred while cracking password: {ex.Message}",
                ContentType = "text/plain",
                StatusCode = 500
            };
        }

        private class BruteForceResponse
        {
            [JsonPropertyName("Message")]
            public string Message { get; set; }

            [JsonPropertyName("Password")]
            public string? Password { get; set; }
        }
    }
}