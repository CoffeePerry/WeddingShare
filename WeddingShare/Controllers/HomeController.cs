using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WeddingShare.Constants;
using WeddingShare.Extensions;
using WeddingShare.Helpers;
using WeddingShare.Helpers.Database;
using WeddingShare.Models;

namespace WeddingShare.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly ISettingsHelper _settings;
        private readonly IDatabaseHelper _database;
        private readonly IDeviceDetector _deviceDetector;
        private readonly IAuditHelper _audit;
        private readonly ILogger _logger;
        private readonly IStringLocalizer<Lang.Translations> _localizer;

        public HomeController(ISettingsHelper settings, IDatabaseHelper database, IDeviceDetector deviceDetector, IAuditHelper audit, ILogger<HomeController> logger, IStringLocalizer<Lang.Translations> localizer)
        {
            _settings = settings;
            _database = database;
            _deviceDetector = deviceDetector;
            _audit = audit;
            _logger = logger;
            _localizer = localizer;
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index()
        {
            var model = new Views.Home.IndexModel();

            try
            {
                var deviceType = HttpContext.Session.GetString(SessionKey.DeviceType);
                if (string.IsNullOrWhiteSpace(deviceType))
                {
                    deviceType = (await _deviceDetector.ParseDeviceType(Request.Headers["User-Agent"].ToString())).ToString();
                    HttpContext.Session.SetString(SessionKey.DeviceType, deviceType ?? "Desktop");
                }

                if (await _settings.GetOrDefault(Settings.Basic.SingleGalleryMode, false))
                {
                    var key = await _settings.GetOrDefault(Settings.Gallery.SecretKey, string.Empty, 1);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        return RedirectToAction("Index", "Gallery", new { identifier = "default" });
                    }
                }

                model.GalleryNames = await _settings.GetOrDefault(Settings.GallerySelector.Dropdown, false) ? (await _database.GetGalleryNames()).Where(x => !x.Equals("all", StringComparison.OrdinalIgnoreCase)) : new List<string>();
                if (await _settings.GetOrDefault(Settings.GallerySelector.HideDefaultOption, false))
                {
                    model.GalleryNames = model.GalleryNames.Where(x => !x.Equals("default", StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_localizer["Homepage_Load_Error"].Value} - {ex?.Message}");
            }

            return View(model);
        }

        [HttpGet]
        [Route("CookiePolicy")]
        [Route("Home/CookiePolicy")]
        public async Task<IActionResult> CookiePolicy()
        {
            ViewBag.CompanyName = await _settings.GetOrDefault(Settings.Basic.Title, "WeddingShare");
            ViewBag.SiteHostname = await _settings.GetOrDefault(Settings.Basic.BaseUrl, "www.wedding-share.org");
            ViewBag.CustomPolicy = await _settings.GetOrDefault(Settings.Policies.CookiePolicy, string.Empty);

            return View("~/Views/Home/CookiePolicy.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> SetIdentity(string name, string? emailAddress)
        {
            try
            {
                var emailRequired = await _settings.GetOrDefault(Settings.IdentityCheck.RequireEmail, false);

                if (string.IsNullOrWhiteSpace(name) || HtmlSanitizer.MayContainXss(name))
                {
                    return Json(new { success = false, reason = 1 });
                }
                else if (emailRequired && (string.IsNullOrWhiteSpace(emailAddress) || EmailValidationHelper.IsValid(emailAddress) == false || HtmlSanitizer.MayContainXss(emailAddress)))
                {
                    return Json(new { success = false, reason = 2 });
                }
                else
                {
                    HttpContext.Session.SetString(SessionKey.ViewerIdentity, name);
                    HttpContext.Session.SetString(SessionKey.ViewerEmailAddress, emailAddress ?? string.Empty);

                    return Json(new { success = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_localizer["Identity_Session_Error"].Value}: '{name}'");
            }

            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> LogCookieApproval()
        {
            try
            {
                var ipAddress = Request.HttpContext.TryGetIpAddress();

                return Json(new { success = await _audit.LogAction("Visitor", $"{_localizer["Audit_CookieConsentApproved"].Value}: {ipAddress}") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_localizer["Cookie_Audit_Error"].Value}");
            }

            return Json(new { success = false });
        }
    }
}