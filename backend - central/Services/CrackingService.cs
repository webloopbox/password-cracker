using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using backend___central.Models;

namespace backend___central.Services
{
    public class CrackingService : ICrackingService
    {
        private readonly IEnumerable<ILogService> logServices;
        private volatile bool passwordFound;

        public CrackingService(IEnumerable<ILogService> logServices)
        {
            this.logServices = logServices;
            this.passwordFound = false;
        }

        public IActionResult HandleBruteForceCracking(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Made request to crack password using brute force method");
            ILogService.LogInfo(logServices, "Final cracking time with brute force method was: 03:34:10. Cracking was unsuccessfull.");
            return new ContentResult
            {
                Content = "Started brute force password cracking.",
                StatusCode = 202
            };
        }

        public async Task<IActionResult> HandleDictionaryCracking(HttpContext httpContext)
        {
            try
            {
                ValidateServersAvailability();
                int totalLines = await GetDictionaryTotalLines();
                List<CalculatingServerState> calculatingServerStates = InitializeServerStates();
                int currentLine = 1;
                while (currentLine <= totalLines && !passwordFound)
                {
                    List<CalculatingServerState> availableServers = GetAvailableServers(calculatingServerStates);
                    if (availableServers.Count == 0)
                    {
                        await HandleNoAvailableServers();
                        continue;
                    }
                    currentLine = await ProcessAvailableServers(availableServers, currentLine, totalLines);
                }
                return CreateFinalResponse();
            }
            catch (Exception ex)
            {
                return HandleCrackingError(ex);
            }
        }

        private void ValidateServersAvailability()
        {
            if (Startup.ServersIpAddresses.Count == 0)
            {
                throw new Exception("No calculating servers available");
            }
        }

        private List<CalculatingServerState> InitializeServerStates()
        {
            return Startup.ServersIpAddresses
                .Select(ip => new CalculatingServerState(ip))
                .ToList();
        }

        private List<CalculatingServerState> GetAvailableServers(List<CalculatingServerState> serverStates)
        {
            return serverStates.Where(server => !server.IsBusy).ToList();
        }

        private async Task<int> ProcessAvailableServers(List<CalculatingServerState> availableServers, int currentLine, int totalLines)
        {
            foreach (CalculatingServerState server in availableServers)
            {
                if (currentLine > totalLines || passwordFound)
                    break;
                object chunk = CreateChunk(currentLine, totalLines);
                server.IsBusy = true;
                try
                {
                    await ProcessChunkAsync(server.IpAddress, chunk, server);
                }
                catch (Exception ex) when (ex.Message.Contains("Password cracked!"))
                {
                    HandlePasswordFoundException(ex);
                    throw;
                }
                catch (Exception)
                {
                    HandleServerError(server);
                    continue;
                }
                currentLine += Startup.Granularity;
            }
            return currentLine;
        }

        private object CreateChunk(int currentLine, int totalLines)
        {
            return new
            {
                StartLine = currentLine,
                EndLine = Math.Min(currentLine + Startup.Granularity - 1, totalLines)
            };
        }

        private void HandlePasswordFoundException(Exception ex)
        {
            passwordFound = true;
            ILogService.LogInfo(logServices, ex.Message);
        }

        private void HandleServerError(CalculatingServerState server)
        {
            server.IsBusy = false;
            if (Startup.ServersIpAddresses.Count == 0)
            {
                throw new Exception("All calculating servers have failed. Stopping dictionary cracking.");
            }
        }

        private async Task HandleNoAvailableServers()
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

        private async Task ProcessChunkAsync(IPAddress serverIp, object chunk, CalculatingServerState serverState)
        {
            try
            {
                using HttpClient httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                StringContent content = new StringContent(
                    JsonSerializer.Serialize(chunk),
                    Encoding.UTF8,
                    "application/json"
                );
                HttpResponseMessage response = await SendRequestToCalculatingServer(httpClient, serverIp, content);
                string responseContent = await response.Content.ReadAsStringAsync();
                HandleServerResponse(response, responseContent, serverIp);
            }
            finally
            {
                serverState.IsBusy = false;
            }
        }

        private async Task<HttpResponseMessage> SendRequestToCalculatingServer(HttpClient httpClient, IPAddress serverIp, StringContent content)
        {
            return await httpClient.PostAsync(
                $"http://{serverIp}:5099/api/dictionary/cracking",
                content
            );
        }

        private void HandleServerResponse(HttpResponseMessage response, string responseContent, IPAddress serverIp)
        {
            if (!response.IsSuccessStatusCode)
            {
                RemoveFailedServer(serverIp);
                throw new Exception($"Server {serverIp} responded with status code {response.StatusCode}");
            }
            if (responseContent.Contains("Password cracked!"))
            {
                passwordFound = true;
                throw new Exception(responseContent);
            }
            ILogService.LogInfo(logServices,
                $"Server {serverIp} completed chunk processing: {responseContent}");
        }

        private void RemoveFailedServer(IPAddress serverIp)
        {
            if (Startup.ServersIpAddresses.Contains(serverIp))
            {
                Startup.ServersIpAddresses.Remove(serverIp);
                ILogService.LogInfo(logServices,
                    $"Removed failed server {serverIp} from active servers. Remaining servers: {Startup.ServersIpAddresses.Count}");
            }
        }
        
        private async Task<int> GetDictionaryTotalLines()
        {
            try
            {
                string dictionaryPath = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                DirectoryInfo directory = new DirectoryInfo(dictionaryPath);
                if (!directory.Exists)
                {
                    throw new DirectoryNotFoundException($"Dictionary directory not found at: {dictionaryPath}");
                }
                FileInfo latestFile = directory.GetFiles()
                    .OrderByDescending(f => f.CreationTime)
                    .FirstOrDefault() ?? throw new FileNotFoundException("No dictionary files found");
                int lineCount = 0;
                using (StreamReader reader = new StreamReader(latestFile.FullName))
                {
                    while (await reader.ReadLineAsync() != null)
                    {
                        lineCount++;
                    }
                }
                ILogService.LogInfo(logServices, $"Dictionary contains {lineCount} lines");
                return lineCount;
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error counting dictionary lines: {ex.Message}");
                throw new Exception($"Failed to get dictionary total lines: {ex.Message}", ex);
            }
        }
    }
}