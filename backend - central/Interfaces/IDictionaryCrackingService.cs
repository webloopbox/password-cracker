using System.Collections.Generic;
using System.Threading.Tasks;
using backend___central.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Interfaces
{

    public interface IDictionaryCrackingService
    {
        Task<IActionResult> HandleDictionaryCracking(HttpContext httpContext);
        Task<string> ExtractUsername(HttpContext httpContext);
        Task<int> GetDictionaryTotalLines();
        List<CalculatingServerState> PrepareServersForDictionaryCracking();
        Task<int> ProcessDictionaryWithServers(int currentLine, int totalLines, string username, List<CalculatingServerState> serverStates);
    }
}