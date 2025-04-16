using Microsoft.AspNetCore.Mvc;
using backend___central.Services;
using System.Threading.Tasks;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/calculating-server")]
    public class CheckController : ControllerBase
    {
        private readonly ICheckService checkService;

        public CheckController(ICheckService checkService)
        {
            this.checkService = checkService;
        }

        [HttpPost("connect")]
        public async Task<IActionResult> ConnectToCentralServer()
        {
            return await checkService.HandleConnectToCentralServerRequest(HttpContext);
        }
    }
}