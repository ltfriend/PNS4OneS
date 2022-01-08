using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PNS4OneS.Pages
{
    public class SetupModel : PageModel
    {
        public bool NoName { get; set; }
        public bool NoPassword { get; set; }

        public IActionResult OnGet()
        {
            NoName = false;
            NoPassword = false;

            if (AdminAccount.IsExists)
                return RedirectToPage("Index");
            else
                return Page();
        }

        public async Task<IActionResult> OnPostAsync(string name, string password)
        {
            NoName = string.IsNullOrEmpty(name);
            NoPassword = string.IsNullOrEmpty(password);

            if (NoName || NoPassword)
                return Page();

            string sessionId = await AdminAccount.CreateAccount(name, password);

            string url = Url.Page("Index", new { SessionId = sessionId });
            return Redirect(url);
        }
    }
}
