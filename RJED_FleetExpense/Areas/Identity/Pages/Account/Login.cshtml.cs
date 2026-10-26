#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Services;

namespace RJED_FleetExpense.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IAuditLogService _auditLog;

        // (fails needed to trigger, lock duration in seconds — null = permanent)
        private static readonly (int FailsNeeded, int? LockSeconds)[] LockStages =
        {
            (3, 30),
            (1, 60),
            (1, 600),
            (1, null),
        };

        // Role -> dashboard path. Checked in priority order.
        // Keys are normalized (lowercase, no "_", "-" or spaces).
        // !! Make sure each path matches a REAL route in your project !!
        private static readonly (string[] RoleKeys, string Path)[] RouteMap =
        {
            (new[] { "superadmin" },                               "/Dashboard/Dashboard"),
            (new[] { "admin" },                                    "/Admin/Dashboard"),
            (new[] { "supervisor" },                               "/Supervisor/Dashboard"),
            (new[] { "departmenthead", "depthead", "department" }, "/Department/Dashboard"),
            (new[] { "accounting", "accountant", "accounts" },     "/Accounting/Dashboard"),
            (new[] { "employee" },                                 "/Employee/Dashboard"),
        };

        private static string Norm(string s) =>
            new string(s.Where(c => c != '_' && c != '-' && !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            IAuditLogService auditLog)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _auditLog = auditLog;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public long LockoutRemainingSeconds { get; set; } = 0;

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
                ModelState.AddModelError(string.Empty, ErrorMessage);

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
                return Page();

            // 1. Find user
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                await _auditLog.LogLoginAsync(null, "LOGIN_FAILED",
                    $"Login attempt for unknown email: {Input.Email}");
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // 2. Inactive?
            if (!user.IsActive)
            {
                await _auditLog.LogLoginAsync(user.Id, "LOGIN_FAILED",
                    $"Login attempt on deactivated account: {Input.Email}");
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Contact support.");
                return Page();
            }

            // 3. Currently locked?
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                var remaining = (user.LockoutEnd.Value - DateTimeOffset.UtcNow).TotalSeconds;

                if (user.LockoutStage >= 4)
                {
                    _logger.LogWarning("Permanently locked account login attempt: {Email}", Input.Email);
                    await _auditLog.LogLoginAsync(user.Id, "LOGIN_BLOCKED",
                        $"Login attempt on permanently locked account: {Input.Email}");
                    ModelState.AddModelError(string.Empty, "Account permanently locked. Contact support.");
                    return Page();
                }

                LockoutRemainingSeconds = (long)Math.Ceiling(remaining);

                string timeDisplay = remaining >= 60
                    ? $"{(int)(remaining / 60)} min {(int)(remaining % 60)}s"
                    : $"{(int)Math.Ceiling(remaining)}s";

                _logger.LogWarning("Locked account login attempt: {Email}, {Time} remaining", Input.Email, timeDisplay);
                await _auditLog.LogLoginAsync(user.Id, "LOGIN_BLOCKED",
                    $"Login attempt while locked. {timeDisplay} remaining. Email: {Input.Email}");

                ModelState.AddModelError(string.Empty, $"Account locked. Try again in {timeDisplay}.");
                return Page();
            }

            // 4. Check password
            bool passwordCorrect = await _userManager.CheckPasswordAsync(user, Input.Password);

            if (passwordCorrect)
            {
                user.LockoutStage = 0;
                user.LockoutEnd = null;
                await _userManager.UpdateAsync(user);
                await _userManager.ResetAccessFailedCountAsync(user);

                // Resolve the role/redirect BEFORE signing in, so a user with
                // no valid role never gets a cookie.
                var roles = await _userManager.GetRolesAsync(user);
                _logger.LogInformation("User {Email} roles from DB: [{Roles}]",
                    Input.Email, string.Join(", ", roles));

                var normalizedRoles = roles.Select(Norm).ToHashSet();

                string redirectPath = null;
                foreach (var (roleKeys, path) in RouteMap)
                {
                    if (roleKeys.Any(normalizedRoles.Contains))
                    {
                        redirectPath = path;
                        break;
                    }
                }

                if (redirectPath == null)
                {
                    _logger.LogWarning("No matching role for {Email}. DB roles: {Roles}",
                        Input.Email, string.Join(", ", roles));
                    ModelState.AddModelError(string.Empty,
                        "Your account has no assigned role. Contact administrator.");
                    return Page();
                }

                await _signInManager.SignInAsync(user, Input.RememberMe);

                await _auditLog.LogLoginAsync(user.Id, "LOGIN_SUCCESS",
                    $"User logged in successfully: {Input.Email}");

                _logger.LogInformation("Redirecting {Email} to {Path}", Input.Email, redirectPath);
                return LocalRedirect(redirectPath);
            }

            // 5. Wrong password
            await _userManager.AccessFailedAsync(user);
            user = await _userManager.FindByEmailAsync(Input.Email);

            var currentStageIndex = Math.Min(user.LockoutStage, LockStages.Length - 1);
            var (failsNeeded, lockSeconds) = LockStages[currentStageIndex];
            var currentFails = user.AccessFailedCount;

            // 6. Threshold reached?
            if (currentFails >= failsNeeded)
            {
                user.LockoutStage = Math.Min(user.LockoutStage + 1, 4);

                if (lockSeconds == null)
                {
                    user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                    await _userManager.UpdateAsync(user);
                    await _userManager.ResetAccessFailedCountAsync(user);

                    _logger.LogWarning("Account permanently locked: {Email}", Input.Email);
                    await _auditLog.LogLoginAsync(user.Id, "ACCOUNT_LOCKED",
                        $"Account permanently locked after repeated failures: {Input.Email}");

                    ModelState.AddModelError(string.Empty,
                        "Account permanently locked due to repeated failures. Contact support.");
                    return Page();
                }

                user.LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(lockSeconds.Value);
                await _userManager.UpdateAsync(user);
                await _userManager.ResetAccessFailedCountAsync(user);

                LockoutRemainingSeconds = lockSeconds.Value;

                string lockMsg = user.LockoutStage switch
                {
                    1 => "Too many failed attempts. Account locked for 30 seconds.",
                    2 => "Too many failed attempts. Account locked for 1 minute.",
                    3 => "Too many failed attempts. Account locked for 10 minutes.",
                    _ => "Account permanently locked. Contact support.",
                };

                _logger.LogWarning("Account locked (stage {Stage}): {Email}", user.LockoutStage, Input.Email);
                await _auditLog.LogLoginAsync(user.Id, "ACCOUNT_LOCKED",
                    $"Account locked at stage {user.LockoutStage} for {lockSeconds.Value}s: {Input.Email}");

                ModelState.AddModelError(string.Empty, lockMsg);
                return Page();
            }

            // 7. Not locked yet
            int attemptsLeft = failsNeeded - currentFails;
            _logger.LogWarning("Failed login attempt for {Email}. {Attempts} attempt(s) left.", Input.Email, attemptsLeft);

            await _auditLog.LogLoginAsync(user.Id, "LOGIN_FAILED",
                $"Wrong password for {Input.Email}. {attemptsLeft} attempt(s) left before lockout (stage {user.LockoutStage}).");

            ModelState.AddModelError(string.Empty,
                $"Invalid password. {attemptsLeft} attempt(s) remaining before lockout.");

            return Page();
        }
    }
}