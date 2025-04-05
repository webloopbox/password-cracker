using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Services
{
    public interface ICheckService
    {
        Task<IActionResult> HandleConnectToCentralServerRequest(HttpContext httpContext);
    }
}