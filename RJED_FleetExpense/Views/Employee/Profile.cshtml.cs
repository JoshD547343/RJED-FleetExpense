using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;

namespace RJED_FleetExpense.Pages.EmployeeDashboard
{
    [Authorize(Roles = "Employee")]
    public class ProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public EmployeeProfileViewModel Profile { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            Profile = new EmployeeProfileViewModel
            {
                Name = user.FullName ?? user.UserName ?? "",
                PhoneNumber = user.PhoneNumber ?? "",
                EmployeeId = user.Id ?? "",  // only if you added this property to ApplicationUser
                FullName = user.FullName ?? user.UserName ?? "User",
                Email = user.Email ?? "",
                UserName = user.UserName ?? ""
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            user.FullName = Profile.Name?.Trim();
            user.PhoneNumber = Profile.PhoneNumber?.Trim();
            // user.EmployeeId = Profile.EmployeeId?.Trim();  // if exists

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["StatusMessage"] = "Profile updated successfully.";
                return RedirectToPage();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }
    }
}