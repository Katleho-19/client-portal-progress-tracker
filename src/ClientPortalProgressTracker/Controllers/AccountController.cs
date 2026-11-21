using System.Security.Claims;
using ClientPortalProgressTracker.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Supabase.Gotrue;

namespace ClientPortalProgressTracker.Controllers;

public class AccountController : Controller
{
    private readonly Supabase.Client _supabase;

    public AccountController(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!model.AgreeToTerms)
        {
            ModelState.AddModelError(nameof(model.AgreeToTerms), "You must agree to the Terms of Service and Privacy Policy.");
        }

        if (!ModelState.IsValid) return View(model);

        try
        {
            await _supabase.InitializeAsync();
            var session = await _supabase.Auth.SignUp(
                model.Email.Trim(),
                model.Password,
                new SignUpOptions
                {
                    Data = new Dictionary<string, object>
                    {
                        { "full_name", model.FullName.Trim() },
                        { "business_name", model.BusinessName.Trim() }
                    }
                });

            if (session?.User != null && !string.IsNullOrEmpty(session.AccessToken))
            {
                await SignInCookieAsync(session.User.Id!, session.User.Email ?? model.Email, model.FullName);
                return RedirectToAction("Index", "Home");
            }

            TempData["SuccessMessage"] = "Account created! Please confirm your email before signing in.";
            return RedirectToAction(nameof(Login));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Registration failed: {ex.Message}");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _supabase.InitializeAsync();
            var session = await _supabase.Auth.SignIn(model.Email.Trim(), model.Password);

            if (session?.User == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            var fullName = session.User.UserMetadata?.TryGetValue("full_name", out var nameObj) == true
                ? nameObj?.ToString() ?? model.Email
                : model.Email;

            await SignInCookieAsync(session.User.Id!, session.User.Email ?? model.Email, fullName);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
        catch
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _supabase.InitializeAsync();
            var resetUrl = Url.Action(nameof(ResetPassword), "Account", null, Request.Scheme)
                ?? $"{Request.Scheme}://{Request.Host}/Account/ResetPassword";

            await _supabase.Auth.ResetPasswordForEmail(new ResetPasswordForEmailOptions(model.Email.Trim())
            {
                RedirectTo = resetUrl
            });
        }
        catch
        {
            // Intentionally ignore to avoid revealing registered emails
        }

        ViewBag.LinkSent = true;
        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPassword() => View(new ResetPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        ModelState.Remove(nameof(model.RefreshToken));

        if (!ModelState.IsValid) return View(model);

        try
        {
            await _supabase.InitializeAsync();

            if (!string.IsNullOrEmpty(model.RefreshToken))
            {
                await _supabase.Auth.SetSession(model.AccessToken, model.RefreshToken);
            }
            else
            {
                await _supabase.Auth.VerifyTokenHash(model.AccessToken, Constants.EmailOtpType.Recovery);
            }

            await _supabase.Auth.Update(new UserAttributes { Password = model.NewPassword });

            ViewBag.PasswordUpdated = true;
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Could not reset password: {ex.Message}");
            return View(model);
        }
    }



    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInCookieAsync(string userId, string email, string fullName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, fullName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
