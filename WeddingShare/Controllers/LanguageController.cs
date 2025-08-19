using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using WeddingShare.Constants;
using WeddingShare.Helpers;
using WeddingShare.Models;

namespace WeddingShare.Controllers
{
    [AllowAnonymous]
    public class LanguageController : Controller
    {
        private readonly ISettingsHelper _settings;
        private readonly ILanguageHelper _languageHelper;
        private readonly ILogger<LanguageController> _logger;

        public LanguageController(ISettingsHelper settings, ILanguageHelper languageHelper, ILogger<LanguageController> logger)
        {
            _settings = settings;
            _languageHelper = languageHelper;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var options = new List<SupportedLanguage>();

            try
            {
                var defaultLang = HttpContext.Session.GetString(SessionKey.SelectedLanguage);
                if (string.IsNullOrWhiteSpace(defaultLang))
                {
                    defaultLang = await _languageHelper.GetOrFallbackCulture(string.Empty, await _settings.GetOrDefault(Settings.Languages.Default, "en-GB"));
                }

                options = (await _languageHelper.DetectSupportedCulturesAsync())
                    .Select(x => new SupportedLanguage() { Key = x.Name, Value = $"{(x.EnglishName.Contains("(") ? x.EnglishName.Substring(0, x.EnglishName.IndexOf("(")) : x.EnglishName).Trim()} ({x.Name})", Selected = string.Equals(defaultLang, x.Name, StringComparison.OrdinalIgnoreCase) })
                    .OrderBy(x => x.Value.ToLower())
                    .ToList();
            }
            catch { }

            return Json(new { supported = options });
        }

        [HttpPost]
        public async Task<IActionResult> ChangeDisplayLanguage(string culture)
        {
            try
            {
                culture = await _languageHelper.GetOrFallbackCulture(culture, await _settings.GetOrDefault(Settings.Languages.Default, "en-GB"));

                HttpContext.Session.SetString(SessionKey.SelectedLanguage, culture);
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
                );

                return Json(new { success = true });
            }
            catch (Exception ex) 
            {
                _logger.LogWarning(ex, $"Failed to set display language to '{culture}' - {ex?.Message}");

                culture = "en-GB";

                HttpContext.Session.SetString(SessionKey.SelectedLanguage, culture);
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
                );
            }

            return Json(new { success = false });
        }
    }
}