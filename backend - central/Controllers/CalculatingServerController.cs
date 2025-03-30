using backend___central.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/calculating-server")]
    public class CalculatingServerController(ICalculatingServerService calculatingServerService) : ControllerBase
    {
        private readonly ICalculatingServerService calculatingServerService = calculatingServerService;

        [HttpPost("connect")]
        public IResult ConnectToCentralServer()
        {
            return calculatingServerService.HandleConnectToCentralServerRequest(HttpContext);
        }
    }
}