using System.Security.Claims;
using ClientPortalProgressTracker.Models.Entities;
using ClientPortalProgressTracker.Models.ViewModels;
using ClientPortalProgressTracker.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Supabase.Gotrue;

namespace ClientPortalProgressTracker.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly SupabaseAdmin _admin;
    private readonly Supabase.Client _supabase;

    public ProfileController(SupabaseAdmin admin, Supabase.Client supabase)
    {
        _admin = admin;
        _supabase = supabase;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentUserEmail => User.FindFirstValue(ClaimTypes.Email)!;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var db = await _admin.GetAsync();
        var freelancer = await db.From<Freelancer>().Where(f => f.Id == CurrentUserId).Single();

        var vm = new BusinessProfileViewModel
        {
            BusinessName = freelancer?.BusinessName ?? string.Empty,
            FullName = freelancer?.FullName ?? string.Empty,
            LogoUrl = freelancer?.LogoUrl,
            PhoneNumber = freelancer?.PhoneNumber ?? string.Empty,
            Email = freelancer?.Email ?? CurrentUserEmail,
            BankName = freelancer?.BankName,
            AccountNumber = freelancer?.AccountNumber,
            BranchCode = freelancer?.BranchCode
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BusinessProfileViewModel model)
    {
        var db = await _admin.GetAsync();
        var freelancer = await db.From<Freelancer>().Where(f => f.Id == CurrentUserId).Single();
        if (freelancer == null) return NotFound();

        // Preserve existing logo URL if validation fails or no new file is uploaded
        model.LogoUrl = freelancer.LogoUrl;

        if (model.LogoFile != null && model.LogoFile.Length > 0)
        {
            var allowedExt = new[] { ".png", ".jpg", ".jpeg" };
            var ext = Path.GetExtension(model.LogoFile.FileName).ToLowerInvariant();

            if (!allowedExt.Contains(ext))
            {
                ModelState.AddModelError(nameof(model.LogoFile), "Logo must be a PNG or JPG image.");
            }
            else if (model.LogoFile.Length > 2 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(model.LogoFile), "Logo must be 2 MB or smaller.");
            }
        }

        if (!ModelState.IsValid) return View(model);

        if (model.LogoFile != null && model.LogoFile.Length > 0)
        {
            using var ms = new MemoryStream();
            await model.LogoFile.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var ext = Path.GetExtension(model.LogoFile.FileName).ToLowerInvariant();
            var storagePath = $"{CurrentUserId}/logo-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}";

            var bucket = db.Storage.From("logos");
            await bucket.Upload(bytes, storagePath, new Supabase.Storage.FileOptions
            {
                ContentType = model.LogoFile.ContentType,
                Upsert = true
            });

            freelancer.LogoUrl = bucket.GetPublicUrl(storagePath);
        }

        freelancer.BusinessName = model.BusinessName.Trim();
        freelancer.FullName = model.FullName.Trim();
        freelancer.PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty;
        freelancer.Email = model.Email.Trim();
        freelancer.BankName = string.IsNullOrWhiteSpace(model.BankName) ? null : model.BankName.Trim();
        freelancer.AccountNumber = string.IsNullOrWhiteSpace(model.AccountNumber) ? null : model.AccountNumber.Trim();
        freelancer.BranchCode = string.IsNullOrWhiteSpace(model.BranchCode) ? null : model.BranchCode.Trim();

        await db.From<Freelancer>().Update(freelancer);

        TempData["SuccessMessage"] = "Business profile and EFT banking details saved.";
        return RedirectToAction(nameof(Index));
    }


    [HttpGet]
    public IActionResult Security()
    {
        ViewBag.AccountEmail = CurrentUserEmail;
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Security(ChangePasswordViewModel model)
    {
        ViewBag.AccountEmail = CurrentUserEmail;
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _supabase.InitializeAsync();
            var session = await _supabase.Auth.SignIn(CurrentUserEmail, model.CurrentPassword);
            if (session?.User == null)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
                return View(model);
            }

            await _supabase.Auth.Update(new UserAttributes { Password = model.NewPassword });
            TempData["SuccessMessage"] = "Password updated successfully.";
            return RedirectToAction(nameof(Security));
        }
        catch
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect or could not be updated.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutAllDevices()
    {
        try
        {
            await _supabase.InitializeAsync();
            await _supabase.Auth.SignOut();
        }
        catch { }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }
}
