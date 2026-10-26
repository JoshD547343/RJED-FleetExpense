using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using RJED_FleetExpense.Data;

namespace RJED_FleetExpense.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        // Stage definitions: how many fails trigger this stage, and how long the lock lasts
        private static readonly (int FailsNeeded, int? LockSeconds)[] LockStages =
        {
            (3, 30),    // stage 0 → stage 1: 3 fails = 30s lock
            (1, 60),    // stage 1 → stage 2: 1 fail  = 1 min lock
            (1, 600),   // stage 2 → stage 3: 1 fail  = 10 min lock
            (1, null),  // stage 3 → stage 4: 1 fail  = permanent
        };

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();
        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            // 1. Find user
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // 2. Check inactive
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Contact support.");
                return Page();
            }

            // 3. Check if currently locked out (from a previous lockout that hasn't expired)
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                // Permanent lock
                if (user.LockoutStage >= 4)
                {
                    ModelState.AddModelError(string.Empty,
                        "Account permanently locked. Contact support.");
                    return Page();
                }

                var remaining = (user.LockoutEnd.Value - DateTimeOffset.UtcNow).TotalSeconds;
                string timeDisplay = remaining >= 60
                    ? $"{(int)(remaining / 60)} min {(int)(remaining % 60)}s"
                    : $"{(int)Math.Ceiling(remaining)}s";

                ModelState.AddModelError(string.Empty,
                    $"Account locked. Try again in {timeDisplay}.");
                return Page();
            }

            // 4. Check password manually (so we fully control fail counting)
            bool passwordCorrect = await _userManager.CheckPasswordAsync(user, Input.Password);

            if (passwordCorrect)
            {
                // Reset everything on success
                user.LockoutStage = 0;
                user.LockoutEnd = null;
                await _userManager.UpdateAsync(user);
                await _userManager.ResetAccessFailedCountAsync(user);

                // Sign in
                await _signInManager.SignInAsync(user, Input.RememberMe);

                // Add FullName claim if missing
                var existingClaims = await _userManager.GetClaimsAsync(user);
                if (!existingClaims.Any(c => c.Type == "FullName"))
                {
                    await _userManager.AddClaimAsync(user,
                        new Claim("FullName", user.FullName ?? user.Email ?? ""));
                }

                await _signInManager.RefreshSignInAsync(user);
                return LocalRedirect(returnUrl);
            }

            // 5. Wrong password — increment fail count manually
            await _userManager.AccessFailedAsync(user);

            // Re-fetch to get updated fail count
            user = (await _userManager.FindByEmailAsync(Input.Email))!;

            var currentStageIndex = Math.Min(user.LockoutStage, LockStages.Length - 1);
            var (failsNeeded, _) = LockStages[currentStageIndex];
            var currentFails = user.AccessFailedCount;

            if (currentFails >= failsNeeded)
            {
                // Trigger lockout for this stage
                var (_, lockSeconds) = LockStages[currentStageIndex];

                user.LockoutStage = Math.Min(user.LockoutStage + 1, 4);

                if (lockSeconds == null)
                {
                    // Permanent
                    user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                    await _userManager.UpdateAsync(user);
                    await _userManager.ResetAccessFailedCountAsync(user);

                    ModelState.AddModelError(string.Empty,
                        "Account permanently locked due to repeated failures. Contact support.");
                    return Page();
                }

                user.LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(lockSeconds.Value);
                await _userManager.UpdateAsync(user);
                await _userManager.ResetAccessFailedCountAsync(user);

                string lockMsg = user.LockoutStage switch
                {
                    1 => "Too many failed attempts. Account locked for 30 seconds.",
                    2 => "Too many failed attempts. Account locked for 1 minute.",
                    3 => "Too many failed attempts. Account locked for 10 minutes.",
                    _ => "Account permanently locked. Contact support.",
                };

                ModelState.AddModelError(string.Empty, lockMsg);
                return Page();
            }

            // Not yet locked — show remaining attempts for this stage
            int attemptsLeft = failsNeeded - currentFails;
            ModelState.AddModelError(string.Empty,
                $"Invalid password. {attemptsLeft} attempt(s) remaining before lockout.");

            return Page();
        }
    }
}