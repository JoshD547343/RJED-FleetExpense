using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;   // ← this gives ViewBag

namespace RJED_FleetExpense.Pages
{
    [Authorize]
    public class DashboardModel : PageModel   // ← must inherit PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;

        public DashboardModel(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public void OnGet()
        {
            
        }
    }
}