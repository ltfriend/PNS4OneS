using Microsoft.AspNetCore.Mvc;

namespace PNS4OneS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeleteAppController : ControllerBase
    {
        [HttpPost]
        public IActionResult Post([FromForm] string appId, [FromForm] string sessionId)
        {
            if (!AdminAccount.IsExists)
                return Redirect("/Setup");
            else if (!AdminAccount.CheckSessionId(sessionId))
                return Redirect("/Login");

            if (!string.IsNullOrEmpty(appId))
                Program.ClientAppsManager.DeleteApp(appId);

            return Redirect($"/?SessionId={sessionId}");
        }
    }
}
