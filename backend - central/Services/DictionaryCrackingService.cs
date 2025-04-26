using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using backend___central.Models;
using backend___central.Interfaces;
using System.Linq;

namespace backend___central.Services
{
    public class DictionaryCrackingService : IDictionaryCrackingService
    {
        private volatile bool passwordFound;
        private readonly IEnumerable<ILogService> logServices;
        private readonly ChunkManagerService chunkManager;
        private readonly ServerManagerService serverManager;
        private readonly TaskCoordinatorService taskCoordinator;
        private readonly IResponseProcessingService responseProcessingService;

        public DictionaryCrackingService(IEnumerable<ILogService> logServices, IResponseProcessingService responseProcessingService)
        {
            this.logServices = logServices;
            this.responseProcessingService = responseProcessingService;
            passwordFound = false;
            chunkManager = new ChunkManagerService(logServices);
            serverManager = new ServerManagerService(logServices);
            taskCoordinator = new TaskCoordinatorService(logServices);
        }

        public async Task<IActionResult> HandleDictionaryCracking(HttpContext httpContext)
        {
            try
            {
                passwordFound = false;
                ILogService.LogInfo(logServices, "Starting dictionary cracking process");
                int currentLine = 1;
                string username = await ExtractUsername(httpContext);
                int totalLines = await GetDictionaryTotalLines();
                List<CalculatingServerState> serverStates = PrepareServersForDictionaryCracking();
                currentLine = await ProcessDictionaryWithServers(currentLine, totalLines, username, serverStates);
                ILogService.LogInfo(logServices, $"Dictionary processing completed. passwordFound={passwordFound}, LastFoundPassword={(TaskCoordinatorService.LastFoundPassword != null ? "available" : "null")}");
                if (passwordFound && TaskCoordinatorService.LastFoundPassword != null)
                {
                    PasswordInfo? passwordInfo = TaskCoordinatorService.LastFoundPassword;
                    ILogService.LogInfo(logServices, $"Found password: {passwordInfo.Value} from server {passwordInfo.ServerIp}, returning success response");
                    return responseProcessingService.ProcessDictionaryResult(true, passwordInfo);
                }
                
                ILogService.LogInfo(logServices, "No password found, returning not found response");
                return responseProcessingService.ProcessDictionaryResult(false, null);
            } 
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error in HandleDictionaryCracking: {ex.Message}");
                return responseProcessingService.HandleError(ex, -1, isDictionary: true);
            }
        }

        public async Task<string> ExtractUsername(HttpContext httpContext)
        {
            IFormCollection form = await httpContext.Request.ReadFormAsync();
            return form["username"].ToString() ?? throw new ArgumentException("Username is required");
        }

        public async Task<int> GetDictionaryTotalLines()
        {
            return await chunkManager.GetDictionaryTotalLines();
        }

        public List<CalculatingServerState> PrepareServersForDictionaryCracking()
        {
            serverManager.ValidateServersAvailability();
            return serverManager.InitializeServerStates();
        }

        public async Task<int> ProcessDictionaryWithServers(int currentLine, int totalLines, string username, List<CalculatingServerState> serverStates)
        {
            try 
            {
                while (currentLine <= totalLines && !passwordFound)
                {
                    List<CalculatingServerState> availableServers = GetAvailableServers(serverStates);
                    if (!availableServers.Any())
                    {
                        await HandleNoAvailableServers();
                        continue;
                    }
                    currentLine = await ProcessServers(availableServers, currentLine, totalLines, username);
                    if (passwordFound || TaskCoordinatorService.LastFoundPassword != null)
                    {
                        ILogService.LogInfo(logServices, "Password found after processing servers, breaking out of dictionary loop");
                        break;
                    }
                }
                if (TaskCoordinatorService.LastFoundPassword != null && !passwordFound)
                {
                    ILogService.LogInfo(logServices, "Password was found but flag wasn't set, fixing...");
                    passwordFound = true;
                }
                return currentLine;
            }
            catch (PasswordFoundException ex)
            {
                ILogService.LogInfo(logServices, $"Caught password found exception: {ex.Message}");
                passwordFound = true;
                return currentLine;
            }
        }

        private List<CalculatingServerState> GetAvailableServers(List<CalculatingServerState> serverStates)
        {
            return serverManager.GetAvailableServers(serverStates);
        }

        private async Task<int> ProcessServers(List<CalculatingServerState> servers, int currentLine, int totalLines, string username)
        {
            ResetTaskCoordinatorState();
            while (currentLine <= totalLines && !passwordFound)
            {
                foreach (CalculatingServerState server in servers.ToList())
                {
                    if (!CanProcessServer(server) || currentLine > totalLines || passwordFound)
                        continue;
                    try
                    {
                        currentLine = AssignChunkToServer(server, currentLine, totalLines, username);
                    }
                    catch (Exception ex)
                    {
                        HandleServerAssignmentError(server, servers, ex);
                    }
                }
                if (await ProcessCompletedTasks())
                {
                    SetPasswordFound();
                    break;
                }
            }
            if (passwordFound)
            {
                CancelAllTasks();
            }
            
            return currentLine;
        }

        private void ResetTaskCoordinatorState()
        {
            taskCoordinator.ResetState();
        }

        private bool CanProcessServer(CalculatingServerState server)
        {
            return taskCoordinator.CanProcessServer(server);
        }

        private int AssignChunkToServer(CalculatingServerState server, int currentLine, int totalLines, string username)
        {
            DateTime firstDateTime = DateTime.UtcNow;
            Chunk chunk = CreateChunk(currentLine, totalLines, firstDateTime);
            MarkServerAsBusy(server);
            AddServerTask(server, chunk, username);
            int nextLine = IncrementLinePosition(currentLine);
            LogChunkAssignment(currentLine, nextLine, server.IpAddress.ToString());
            return nextLine;
        }

        private Chunk CreateChunk(int currentLine, int totalLines, DateTime firstDateTime)
        {
            return chunkManager.CreateChunk(currentLine, totalLines, firstDateTime);
        }

        private static void MarkServerAsBusy(CalculatingServerState server)
        {
            server.IsBusy = true;
        }

        private void AddServerTask(CalculatingServerState server, Chunk chunk, string username)
        {
            taskCoordinator.AddServerTask(server, chunk, username);
        }

        private static int IncrementLinePosition(int currentLine)
        {
            return currentLine + Startup.DictionaryGranularity;
        }

        private void LogChunkAssignment(int startLine, int endLine, string serverIp)
        {
            ILogService.LogInfo(logServices, $"Assigned chunk {startLine}-{endLine} to server {serverIp}");
        }

        private void HandleServerAssignmentError(CalculatingServerState server, List<CalculatingServerState> servers, Exception ex)
        {
            LogServerAssignmentError(server.IpAddress.ToString(), ex.Message);
            MarkServerAsFailed(server);
            MarkServerAsNotBusy(server);
            RemoveServerFromList(server, servers);
        }

        private void LogServerAssignmentError(string serverIp, string errorMessage)
        {
            ILogService.LogError(logServices, $"Failed to assign chunk to server {serverIp}: {errorMessage}");
        }

        private void MarkServerAsFailed(CalculatingServerState server)
        {
            serverManager.MarkServerAsFailed(server);
        }

        private static void MarkServerAsNotBusy(CalculatingServerState server)
        {
            server.IsBusy = false;
        }

        private static void RemoveServerFromList(CalculatingServerState server, List<CalculatingServerState> servers)
        {
            servers.Remove(server);
        }

        private async Task<bool> ProcessCompletedTasks()
        {
            return await taskCoordinator.ProcessCompletedTasks();
        }

        private void SetPasswordFound()
        {
            passwordFound = true;
        }

        private void CancelAllTasks()
        {
            taskCoordinator.CancelAllTasks();
        }

        private static async Task HandleNoAvailableServers()
        {
            if (Startup.ServersIpAddresses.Count == 0)
            {
                throw new Exception("All calculating servers have failed. Stopping dictionary cracking.");
            }
            await Task.Delay(1000);
        }
    }
}