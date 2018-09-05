using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecondFamilies.Models;
using SecondFamilies.Models.AccountViewModels;
using SecondFamilies.Services;
using SecondFamilies.Data;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using MimeKit;
using MailKit.Net.Smtp;
using System.Net.Mail;
using System.Net;

namespace SecondFamilies.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;
        private readonly IMessaging _messenger;


        ApplicationDbContext ctx;
        const string DonateSession = "DonateSession";

        private IHostingEnvironment hostingEnv;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext _ctx,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            IMessaging messaging,
            ILogger<AccountController> logger,
            IHostingEnvironment env
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
            ctx = _ctx;
            _messenger = messaging;
            this.hostingEnv = env;
        }

        [TempData]
        public string ErrorMessage { get; set; }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string returnUrl = null)
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    return RedirectToLocal(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToAction(nameof(LoginWith2fa), new { returnUrl, model.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToAction(nameof(Lockout));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> LoginWith2fa(bool rememberMe, string returnUrl = null)
        {
            // Ensure the user has gone through the username & password screen first
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

            if (user == null)
            {
                throw new ApplicationException($"Unable to load two-factor authentication user.");
            }

            var model = new LoginWith2faViewModel { RememberMe = rememberMe };
            ViewData["ReturnUrl"] = returnUrl;

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model, bool rememberMe, string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var authenticatorCode = model.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);

            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, model.RememberMachine);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID {UserId} logged in with 2fa.", user.Id);
                return RedirectToLocal(returnUrl);
            }
            else if (result.IsLockedOut)
            {
                _logger.LogWarning("User with ID {UserId} account locked out.", user.Id);
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                _logger.LogWarning("Invalid authenticator code entered for user with ID {UserId}.", user.Id);
                ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
                return View();
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> LoginWithRecoveryCode(string returnUrl = null)
        {
            // Ensure the user has gone through the username & password screen first
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new ApplicationException($"Unable to load two-factor authentication user.");
            }

            ViewData["ReturnUrl"] = returnUrl;

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginWithRecoveryCode(LoginWithRecoveryCodeViewModel model, string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new ApplicationException($"Unable to load two-factor authentication user.");
            }

            var recoveryCode = model.RecoveryCode.Replace(" ", string.Empty);

            var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID {UserId} logged in with a recovery code.", user.Id);
                return RedirectToLocal(returnUrl);
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User with ID {UserId} account locked out.", user.Id);
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                _logger.LogWarning("Invalid recovery code entered for user with ID {UserId}", user.Id);
                ModelState.AddModelError(string.Empty, "Invalid recovery code entered.");
                return View();
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Lockout()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var callbackUrl = Url.EmailConfirmationLink(user.Id, code, Request.Scheme);
                    await _emailSender.SendEmailConfirmationAsync(model.Email, callbackUrl);

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    _logger.LogInformation("User created a new account with password.");
                    return RedirectToLocal(returnUrl);
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToAction(nameof(Login));
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToAction(nameof(Login));
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in with {Name} provider.", info.LoginProvider);
                return RedirectToLocal(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                ViewData["ReturnUrl"] = returnUrl;
                ViewData["LoginProvider"] = info.LoginProvider;
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                return View("ExternalLogin", new ExternalLoginViewModel { Email = email });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginViewModel model, string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                var info = await _signInManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    throw new ApplicationException("Error loading external login information during confirmation.");
                }
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);
                        return RedirectToLocal(returnUrl);
                    }
                }
                AddErrors(result);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(nameof(ExternalLogin), model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{userId}'.");
            }
            var result = await _userManager.ConfirmEmailAsync(user, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToAction(nameof(ForgotPasswordConfirmation));
                }

                // For more information on how to enable account confirmation and password reset please
                // visit https://go.microsoft.com/fwlink/?LinkID=532713
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.ResetPasswordCallbackLink(user.Id, code, Request.Scheme);
                await _emailSender.SendEmailAsync(model.Email, "Reset Password",
                   $"Please reset your password by clicking here: <a href='{callbackUrl}'>link</a>");
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string code = null)
        {
            if (code == null)
            {
                throw new ApplicationException("A code must be supplied for password reset.");
            }
            var model = new ResetPasswordViewModel { Code = code };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }
            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }
            AddErrors(result);
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }


        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Donate(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Donate(DonateViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            System.IO.DirectoryInfo di = new DirectoryInfo(hostingEnv.WebRootPath + "\\dimage\\");

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }

            if (_signInManager.IsSignedIn(User))
            {
                var useremail = User.Identity.Name;
                var userdata = ctx.Users.Where(c => c.Email == useremail).ToList();
                if (userdata != null)
                {
                    var donateData = new Donate
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Address = model.Address,
                        PhoneNumber = model.PhoneNumber,
                        Email = model.Email == null ? userdata[0].Email : string.Empty,
                        Amount = model.Amount,
                        Allocation = model.Allocation,
                        Item = model.Item,
                        Quantity = model.Quantity,
                        ImageUrl = model.ImageFile,
                        NeedPickup = model.NeedPickup,
                        CanDropOff = model.CanDropOff,
                        DatePickDrop = model.DatePickDrop,
                        DonationType = model.DonationType,
                        DonationStatus = "pending",
                        UserId = userdata[0].Id
                    };
                    ctx.Donate.Add(donateData);
                    ctx.SaveChanges();
                    HttpContext.Session.SetString(DonateSession, donateData.Email);
                    HttpContext.Session.SetString("FirstName", donateData.FirstName);
                    HttpContext.Session.SetString("LastName", donateData.LastName);

                    string _MerchantEmail = "manishkr38@gmail.com";
                    string _ReturnURL = "https://localhost:44396/Account/SuccessfullDonation";
                    string _CancelURL = "https://localhost:44396/";
                    string _CurrencyCode = "USD";
                    int _Amount = Convert.ToInt32(donateData.Amount);
                    string _ItemName = "Donate to SecondFamilies"; //We are using this field to pass the order number
                    int _Discount = 0;
                    double _Tax = 0.0;
                    string _PayPalURL = $"https://www.paypal.com/cgi-bin/webscr?cmd=_xclick&business={_MerchantEmail}&return={_ReturnURL}&cancel_return={_CancelURL}&currency_code={_CurrencyCode}&amount={_Amount}&item_name={_ItemName}&discount_amount={_Discount}&tax={_Tax}";

                    Response.Redirect(_PayPalURL);

                    //return RedirectToLocal(returnUrl);
                    //return RedirectToAction("SuccessfullDonation", "Account", new { area = "" });
                }
            }
            else
            {
                if (ModelState.IsValid)
                {
                    var user = new ApplicationUser
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Address = model.Address,
                        PhoneNumber = model.PhoneNumber
                    };
                    var result = await _userManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        var donateData = new Donate
                        {
                            FirstName = model.FirstName,
                            LastName = model.LastName,
                            Address = model.Address,
                            PhoneNumber = model.PhoneNumber,
                            Email = model.Email == null ? user.Email : string.Empty,
                            Amount = model.Amount,
                            Allocation = model.Allocation,
                            Item = model.Item,
                            Quantity = model.Quantity,
                            ImageUrl = model.ImageFile,
                            NeedPickup = model.NeedPickup,
                            CanDropOff = model.CanDropOff,
                            DatePickDrop = model.DatePickDrop,
                            DonationType = model.DonationType,
                            DonationStatus = "pending",
                            UserId = user.Id
                        };
                        ctx.Donate.Add(donateData);
                        ctx.SaveChanges();

                        _logger.LogInformation("User created a new account with password.");

                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var callbackUrl = Url.EmailConfirmationLink(user.Id, code, Request.Scheme);
                        await _emailSender.SendEmailConfirmationAsync(model.Email, callbackUrl);

                        await _signInManager.SignInAsync(user, isPersistent: false);
                        _logger.LogInformation("User created a new account with password.");
                        //return RedirectToLocal(returnUrl);
                        //return RedirectToAction("SuccessfullDonation", "Account", new { area = "" });
                        HttpContext.Session.SetString(DonateSession, donateData.Email);
                        HttpContext.Session.SetString("FirstName", donateData.FirstName);
                        HttpContext.Session.SetString("LastName", donateData.LastName);
                        string _MerchantEmail = "manishkr38@gmail.com";
                        string _ReturnURL = "https://localhost:44396/Account/SuccessfullDonation";
                        string _CancelURL = "https://localhost:44396/";
                        string _CurrencyCode = "USD";
                        int _Amount = Convert.ToInt32(donateData.Amount);
                        string _ItemName = "Donate to SecondFamilies"; //We are using this field to pass the order number
                        int _Discount = 0;
                        double _Tax = 0.0;
                        string _PayPalURL = $"https://www.paypal.com/cgi-bin/webscr?cmd=_xclick&business={_MerchantEmail}&return={_ReturnURL}&cancel_return={_CancelURL}&currency_code={_CurrencyCode}&amount={_Amount}&item_name={_ItemName}&discount_amount={_Discount}&tax={_Tax}";

                        Response.Redirect(_PayPalURL);

                    }
                    AddErrors(result);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult DonateGoods(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DonateGoods(DonateGoodsViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            //delete all images from dimage folder code
            System.IO.DirectoryInfo di = new DirectoryInfo(hostingEnv.WebRootPath + "\\dimage\\");

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }

            var files = Request.Form.Files;
            long size = 0;
            if (files.Count() > 1)
            {
                foreach (var file in files)
                {
                    var filename = ContentDispositionHeaderValue
                            .Parse(file.ContentDisposition)
                            .FileName
                            .Trim('"');
                    filename = hostingEnv.WebRootPath + "\\dimage\\" + $@"\{filename}";
                    size += file.Length;
                    using (FileStream fs = System.IO.File.Create(filename))
                    {
                        file.CopyTo(fs);
                        fs.Flush();
                    }
                }

            }

            if (_signInManager.IsSignedIn(User))
            {
                var useremail = User.Identity.Name;
                var userdata = ctx.Users.Where(c => c.Email == useremail).ToList();
                if (userdata != null)
                {
                    var donateData = new Donate
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Address = model.Address,
                        PhoneNumber = model.PhoneNumber,
                        Email = model.Email == null ? userdata[0].Email : string.Empty,
                        Amount = model.Amount,
                        Allocation = model.Allocation,
                        Item = model.Item,
                        Quantity = model.Quantity,
                        ImageUrl = model.ImageFile,
                        NeedPickup = model.NeedPickup,
                        CanDropOff = model.CanDropOff,
                        DatePickDrop = model.DatePickDrop,
                        DonationType = model.DonationType,
                        DonationStatus = "pending",
                        UserId = userdata[0].Id
                    };
                    ctx.Donate.Add(donateData);
                    ctx.SaveChanges();
                    await _messenger.SendDonationEmail(donateData, true);
                    return RedirectToAction("SuccessfullDonation");
                }
            }
            else
            {
                if (ModelState.IsValid)
                {
                    var user = new ApplicationUser
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Address = model.Address,
                        PhoneNumber = model.PhoneNumber
                    };
                    var result = await _userManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        var donateData = new Donate
                        {
                            FirstName = model.FirstName,
                            LastName = model.LastName,
                            Address = model.Address,
                            PhoneNumber = model.PhoneNumber,
                            Email = model.Email,
                            Amount = model.Amount,
                            Allocation = model.Allocation,
                            Item = model.Item,
                            Quantity = model.Quantity,
                            ImageUrl = model.ImageFile,
                            NeedPickup = model.NeedPickup,
                            CanDropOff = model.CanDropOff,
                            DatePickDrop = model.DatePickDrop,
                            DonationType = model.DonationType,
                            DonationStatus = "pending",
                            UserId = user.Id
                        };
                        ctx.Donate.Add(donateData);
                        ctx.SaveChanges();

                        _logger.LogInformation("User created a new account with password.");

                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var callbackUrl = Url.EmailConfirmationLink(user.Id, code, Request.Scheme);
                        await _emailSender.SendEmailConfirmationAsync(model.Email, callbackUrl);
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        await _messenger.SendDonationEmail(donateData, true);
                    }
                    AddErrors(result);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> SuccessfullDonation(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            var returnUrlString = HttpContext.Session.GetString(DonateSession);
            var FirstName = HttpContext.Session.GetString("FirstName");
            var LastName = HttpContext.Session.GetString("LastName");
            if (returnUrlString != null)
            {
                System.IO.DirectoryInfo di = new DirectoryInfo(hostingEnv.WebRootPath + "\\dimage\\");

                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                await _messenger.SendDonationEmail(new Donate { Email = returnUrlString, FirstName = FirstName, LastName = LastName }, false);
            }
            return View();
        }

        #endregion
    }
}
