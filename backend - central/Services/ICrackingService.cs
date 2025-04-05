using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Services
{
    public interface ICrackingService
    {
        IActionResult HandleBruteForceCracking(HttpContext httpContext);
        IActionResult HandleDictionaryCracking(HttpContext httpContext);
    }
}