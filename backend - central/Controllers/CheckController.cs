using Microsoft.AspNetCore.Mvc;
using backend___central.Services;
using System.Threading.Tasks;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/calculating-server")]
    public class CheckController : ControllerBase
    {
        private readonly ICheckService calculatingServerService;

        public CheckController(ICheckService calculatingServerService)
        {
            this.calculatingServerService = calculatingServerService;
        }

        [HttpPost("connect")]
        public async Task<IActionResult> ConnectToCentralServer()
        {
            return await calculatingServerService.HandleConnectToCentralServerRequest(HttpContext);
        }
    }
}