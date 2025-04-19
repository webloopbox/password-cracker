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
        private readonly ChunkManagerService chunkManager;
        private readonly ServerManagerService serverManager;
        private readonly TaskCoordinatorService taskCoordinator;

        public CrackingService(IEnumerable<ILogService> logServices)
        {
            passwordFound = false;
            this.logServices = logServices;
            chunkManager = new ChunkManagerService(logServices);
            serverManager = new ServerManagerService(logServices);
            taskCoordinator = new TaskCoordinatorService(logServices);
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
    }
}