using Microsoft.AspNetCore.Mvc;

namespace PNS4OneS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CreateAppController : ControllerBase
    {
        [HttpPost]
        public IActionResult Post([FromForm] string appName, [FromForm] string sessionId)
        {
            if (!AdminAccount.IsExists)
                return Redirect("/Setup");
            else if (!AdminAccount.CheckSessionId(sessionId))
                return Redirect("/Login");

            if (!string.IsNullOrEmpty(appName))
                Program.ClientAppsManager.CreateApp(appName);

            return Redirect($"/?SessionId={sessionId}");
        }
    }
}
