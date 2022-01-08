using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PNS4OneS.KeyStorage;

namespace PNS4OneS.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string SessionId { get; set; }

        public bool NoClientApps = true;

        public ClientAppsManager ClientAppsManager
        {
            get { return Program.ClientAppsManager; }
        }

        public IActionResult OnGet()
        {
            if (!AdminAccount.IsExists)
                return RedirectToPage("Setup");
            else if (!AdminAccount.CheckSessionId(SessionId))
                return RedirectToPage("Login");
            else
                return Page();
        }
    }
}