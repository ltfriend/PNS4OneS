using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PNS4OneS.Pages
{
    public class ChangePasswordModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string SessionId { get; set; }

        public bool NoPassword { get; set; }
        public bool PasswordIncorrect { get; set; }

        public IActionResult OnGet()
        {
            NoPassword = false;
            PasswordIncorrect = false;

            if (!AdminAccount.IsExists)
                return RedirectToPage("Setup");
            else if (!AdminAccount.CheckSessionId(SessionId))
                return RedirectToPage("Login");
            else
                return Page();
        }

        public async Task<IActionResult> OnPostAsync(string oldPassword, string password, string sessionId)
        {
            if (!AdminAccount.IsExists)
                return RedirectToPage("Setup");
            else if (!AdminAccount.CheckSessionId(sessionId))
                return RedirectToPage("Login");

            NoPassword = string.IsNullOrEmpty(password);

            if (NoPassword)
                return Page();

            PasswordIncorrect = !AdminAccount.CheckPassword(oldPassword);
            if (PasswordIncorrect)
                return Page();

            string newSessionId = await AdminAccount.ChangePassword(password);

            string url = Url.Page("Index", new { SessionId = newSessionId });
            return Redirect(url);
        }
    }
}