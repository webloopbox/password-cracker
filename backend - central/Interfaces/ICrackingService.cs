using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Services
{
    public interface ICrackingService
    {
        Task<IActionResult> HandleBruteForceRequest(HttpContext httpContext);
        Task<IActionResult> HandleDictionaryCracking(HttpContext httpContext);
    }
}