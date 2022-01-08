using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PNS4OneS.Pages
{
    public class LoginModel : PageModel
    {
        public bool NoName { get; set; }
        public bool NoPassword { get; set; }
        public bool PasswordIncorrect { get; set; }

        public IActionResult OnGet()
        {
            NoName = false;
            NoPassword = false;
            PasswordIncorrect = false;

            if (!AdminAccount.IsExists)
                return RedirectToPage("Setup");
            else
                return Page();
        }

        public IActionResult OnPost(string name, string password)
        {
            NoName = string.IsNullOrEmpty(name);
            NoPassword = string.IsNullOrEmpty(password);

            if (NoName || NoPassword)
                return Page();

            string sessionId = AdminAccount.Auth(name, password);
            PasswordIncorrect = sessionId == "";

            if (PasswordIncorrect)
                return Page();

            string url = Url.Page("Index", new { SessionId = sessionId });
            return Redirect(url);
        }
    }
}
