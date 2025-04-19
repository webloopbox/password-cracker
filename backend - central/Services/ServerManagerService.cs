using System;
using System.Collections.Generic;
using System.Linq;
using backend___central.Models;

namespace backend___central.Services
{
    public class ServerManagerService
    {
        private readonly IEnumerable<ILogService> logServices;

        public ServerManagerService(IEnumerable<ILogService> logServices)
        {
            this.logServices = logServices;
        }

        public void ValidateServersAvailability()
        {
            if (!Startup.ServersIpAddresses.Any())
                throw new Exception("No calculating servers available");
        }

        public List<CalculatingServerState> InitializeServerStates()
        {
            return Startup.ServersIpAddresses
                .Select(ip => new CalculatingServerState(ip))
                .ToList();
        }

        public List<CalculatingServerState> GetAvailableServers(List<CalculatingServerState> servers)
        {
            return servers.Where(server => !server.IsBusy).ToList();
        }

        public void MarkServerAsFailed(CalculatingServerState server)
        {
            if (Startup.ServersIpAddresses.Contains(server.IpAddress))
            {
                Startup.ServersIpAddresses.Remove(server.IpAddress);
                ILogService.LogInfo(logServices,
                    $"Removed failed server {server.IpAddress}. Remaining: {Startup.ServersIpAddresses.Count}");
            }
        }
    }
}