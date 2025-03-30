using Microsoft.AspNetCore.Mvc;
using backend___central.Services;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/calculating-server")]
    public class CalculatingServerController(ICalculatingServerService calculatingServerService) : ControllerBase
    {
        private readonly ICalculatingServerService calculatingServerService = calculatingServerService;

        [HttpPost("connect")]
        public async Task<IResult> ConnectToCentralServer()
        {
            return await calculatingServerService.HandleConnectToCentralServerRequest(HttpContext);
        }
    }
}