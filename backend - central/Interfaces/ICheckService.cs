using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Interfaces
{
    public interface ICheckService
    {
        Task<IActionResult> HandleConnectToCentralServerRequest(HttpContext httpContext);
    }
}