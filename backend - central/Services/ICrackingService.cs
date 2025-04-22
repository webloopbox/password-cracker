using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace backend___central.Services
{
    public interface ICrackingService
    {
        Task<IActionResult> HandlBruteForceRequest(HttpContext httpContext);
        Task<IActionResult> HandleDictionaryCracking(HttpContext httpContext);
    }
}