using Microsoft.AspNetCore.Mvc;

namespace PNS4OneS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UpdateClientKeyController : ControllerBase
    {
        [HttpPost]
        public IActionResult Post([FromForm] string appId, [FromForm] string sessionId)
        {
            if (!AdminAccount.IsExists)
                return Redirect("/Setup");
            else if (!AdminAccount.CheckSessionId(sessionId))
                return Redirect("/Login");

            if (string.IsNullOrEmpty(appId))
                return BadRequest();

            if (!Program.ClientAppsManager.UpdateClientKey(appId, out string clientKey))
                return new InternalServerErrorResult();

            return Content(clientKey, "text/plain");
        }
    }
}
