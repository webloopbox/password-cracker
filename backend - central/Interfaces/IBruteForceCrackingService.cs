using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using backend___central.Models;

namespace backend___central.Interfaces
{
    public interface IBruteForceCrackingService
    {
        Task<IActionResult> HandleBruteForceRequest(HttpContext httpContext);
        BruteForceRequestData ParseAndValidateRequest(string bodyContent);
        CrackingCharPackage DistributeCharacters();
        Task<ServerTaskResult> ExecuteTasks(CrackingCharPackage charPackage, BruteForceRequestData credentials);
    }
}