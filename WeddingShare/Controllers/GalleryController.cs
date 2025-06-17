using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WeddingShare.Attributes;
using WeddingShare.Constants;
using WeddingShare.Enums;
using WeddingShare.Extensions;
using WeddingShare.Helpers;
using WeddingShare.Helpers.Database;
using WeddingShare.Helpers.Notifications;
using WeddingShare.Models;
using WeddingShare.Models.Database;
using static WeddingShare.Constants.Notifications;

namespace WeddingShare.Controllers
{
    [AllowAnonymous]
    public class GalleryController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ISettingsHelper _settings;
        private readonly IDatabaseHelper _database;
        private readonly IFileHelper _fileHelper;
        private readonly IDeviceDetector _deviceDetector;
        private readonly IImageHelper _imageHelper;
        private readonly INotificationHelper _notificationHelper;
        private readonly IEncryptionHelper _encryptionHelper;
        private readonly Helpers.IUrlHelper _urlHelper;
        private readonly ILogger _logger;
        private readonly IStringLocalizer<Lang.Translations> _localizer;

        private readonly string ImagesDirectory;
        private readonly string TempDirectory;
        private readonly string UploadsDirectory;
        private readonly string ThumbnailsDirectory;

        public GalleryController(IWebHostEnvironment hostingEnvironment, ISettingsHelper settings, IDatabaseHelper database, IFileHelper fileHelper, IDeviceDetector deviceDetector, IImageHelper imageHelper, INotificationHelper notificationHelper, IEncryptionHelper encryptionHelper, Helpers.IUrlHelper urlHelper, ILogger<GalleryController> logger, IStringLocalizer<Lang.Translations> localizer)
        {
            _hostingEnvironment = hostingEnvironment;
            _settings = settings;
            _database = database;
            _fileHelper = fileHelper;
            _deviceDetector = deviceDetector;
            _imageHelper = imageHelper;
            _notificationHelper = notificationHelper;
            _encryptionHelper = encryptionHelper;
            _urlHelper = urlHelper;
            _logger = logger;
            _localizer = localizer;

            ImagesDirectory = Path.Combine(_hostingEnvironment.WebRootPath, Directories.Images);
            TempDirectory = Path.Combine(_hostingEnvironment.WebRootPath, Directories.TempFiles);
            UploadsDirectory = Path.Combine(_hostingEnvironment.WebRootPath, Directories.Uploads);
            ThumbnailsDirectory = Path.Combine(_hostingEnvironment.WebRootPath, Directories.Thumbnails);
        }

        [HttpPost]
        public async Task<IActionResult> Login(string id = "default", string? key = null)
        {
            id = id.Trim();

            var append = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("id", id)
            };

            GalleryModel? gallery = await _database.GetGallery(id);
            if (gallery == null)
            {
                if (await _settings.GetOrDefault(Settings.Basic.GuestGalleryCreation, false))
                { 
                    if (await _database.GetGalleryCount() < await _settings.GetOrDefault(Settings.Basic.MaxGalleryCount, 1000000))
                    {
                        await _database.AddGallery(new GalleryModel()
                        {
                            Name = id.ToLower(),
                            SecretKey = key,
                            Owner = 0
                        });
                    }
                    else
                    {
                        return new RedirectToActionResult("Index", "Error", new { Reason = ErrorCode.GalleryLimitReached }, false);
                    }
                }
                else
                {
                    return new RedirectToActionResult("Index", "Error", new { Reason = ErrorCode.GalleryCreationNotAllowed }, false);
                }
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                var enc = _encryptionHelper.IsEncryptionEnabled();
                append.Add(new KeyValuePair<string, string>("key", enc ? _encryptionHelper.Encrypt(key) : key));
                append.Add(new KeyValuePair<string, string>("enc", enc.ToString().ToLower()));
            }

            var redirectUrl = _urlHelper.GenerateFullUrl(HttpContext.Request, "/Gallery", append);

            return new JsonResult(new { success = true, redirectUrl });
        }

        [HttpGet]
        [RequiresSecretKey]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(string id = "default", string? key = null, ViewMode? mode = null, GalleryGroup group = GalleryGroup.None, GalleryOrder order = GalleryOrder.Descending, GalleryFilter filter = GalleryFilter.All, string? culture = null, bool partial = false)
        {
            id = (!string.IsNullOrWhiteSpace(id) && !await _settings.GetOrDefault(Settings.Basic.SingleGalleryMode, false)) ? id.Trim().ToLower() : "default";

            if (!string.IsNullOrWhiteSpace(culture))
            { 
                try
                {
                    HttpContext.Session.SetString(SessionKey.SelectedLanguage, culture);
                    Response.Cookies.Append(
                        CookieRequestCultureProvider.DefaultCookieName,
                        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
                    );
                }
                catch { }
            }

            try
            {
                ViewBag.ViewMode = mode ?? (ViewMode)await _settings.GetOrDefault(Settings.Gallery.DefaultView, (int)ViewMode.Default, id);
            }
            catch
            {
                ViewBag.ViewMode = ViewMode.Default;
            }

            var deviceType = HttpContext.Session.GetString(SessionKey.DeviceType);
            if (string.IsNullOrWhiteSpace(deviceType))
            {
                deviceType = (await _deviceDetector.ParseDeviceType(Request.Headers["User-Agent"].ToString())).ToString();
                HttpContext.Session.SetString(SessionKey.DeviceType, deviceType ?? "Desktop");
            }

            ViewBag.IsMobile = !string.Equals("Desktop", deviceType, StringComparison.OrdinalIgnoreCase);

            var galleryPath = Path.Combine(UploadsDirectory, id);
            _fileHelper.CreateDirectoryIfNotExists(galleryPath);
            _fileHelper.CreateDirectoryIfNotExists(Path.Combine(galleryPath, "Pending"));

            GalleryModel? gallery = await _database.GetGallery(id);
            if (gallery != null)
            {
                ViewBag.GalleryId = gallery.Name;

                var secretKey = await _settings.GetOrDefault(Settings.Gallery.SecretKey, string.Empty, gallery.Name);
                ViewBag.SecretKey = secretKey;

                var currentPage = 1;
                try
                {
                    currentPage = int.Parse((Request.Query.ContainsKey("page") && !string.IsNullOrWhiteSpace(Request.Query["page"])) ? Request.Query["page"].ToString().ToLower() : "1");
                }
                catch { }

                var mediaType = MediaType.All;
                if (mode == ViewMode.Slideshow)
                {
                    mediaType = MediaType.Image;
                }
                else
                {
                    switch (filter)
                    {
                        case GalleryFilter.Images:
                            mediaType = MediaType.Image;
                            break;
                        case GalleryFilter.Videos:
                            mediaType = MediaType.Video;
                            break;
                        default:
                            mediaType = MediaType.All;
                            break;
                    }
                }

                var orientation = ImageOrientation.None;
                switch (filter)
                {
                    case GalleryFilter.Landscape:
                        orientation = ImageOrientation.Landscape;
                        break;
                    case GalleryFilter.Portrait:
                        orientation = ImageOrientation.Portrait;
                        break;
                    case GalleryFilter.Square:
                        orientation = ImageOrientation.Square;
                        break;
                    default:
                        orientation = ImageOrientation.None;
                        break;
                }

                var itemsPerPage = await _settings.GetOrDefault(Settings.Gallery.ItemsPerPage, 50, gallery?.Name);
                var allowedFileTypes = (await _settings.GetOrDefault(Settings.Gallery.AllowedFileTypes, ".jpg,.jpeg,.png,.mp4,.mov", gallery?.Name)).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var items = (await _database.GetAllGalleryItems(gallery?.Id, GalleryItemState.Approved, mediaType, orientation, group, order, itemsPerPage, currentPage))?.Where(x => allowedFileTypes.Any(y => string.Equals(Path.GetExtension(x.Title).Trim('.'), y.Trim('.'), StringComparison.OrdinalIgnoreCase)));

                var userPermissions = User?.Identity?.GetUserPermissions() ?? AccessPermissions.None;
                var isGalleryAdmin = User?.Identity != null && User.Identity.IsAuthenticated && userPermissions.HasFlag(AccessPermissions.Gallery_Upload);

                FileUploader? fileUploader = null;
                if (!string.Equals("All", gallery?.Name, StringComparison.OrdinalIgnoreCase) && (await _settings.GetOrDefault(Settings.Gallery.Upload, true, gallery?.Name) || isGalleryAdmin))
                {
                    var uploadActvated = isGalleryAdmin;
                    try
                    {
                        if (!uploadActvated)
                        { 
                            var periods = (await _settings.GetOrDefault(Settings.Gallery.UploadPeriod, "1970-01-01 00:00", gallery?.Name))?.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (periods != null)
                            { 
                                var now = DateTime.UtcNow;
                                foreach (var period in periods)
                                {
                                    var timeRanges = period?.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                    if (timeRanges != null && timeRanges.Length > 0)
                                    {
                                        var startDate = DateTime.Parse(timeRanges[0]).ToUniversalTime();

                                        if (timeRanges.Length == 2)
                                        {
                                            var endDate = DateTime.Parse(timeRanges[1]).ToUniversalTime();
                                            if (now >= startDate && now < endDate)
                                            {
                                                uploadActvated = true;
                                                break;
                                            }
                                        }
                                        else if (timeRanges.Length == 1 && now >= startDate)
                                        {
                                            uploadActvated = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch 
                    {
                        uploadActvated = true;
                    }

                    if (uploadActvated)
                    { 
                        fileUploader = new FileUploader(gallery?.Name ?? "default", secretKey, "/Gallery/UploadImage", await _settings.GetOrDefault(Settings.IdentityCheck.RequireIdentityForUpload, false));
                    }
                }

                var itemCounts = await _database.GetGalleryItemCount(gallery?.Id, GalleryItemState.All, mediaType, orientation);
                var model = new PhotoGallery()
                {
                    GalleryId = gallery?.Id,
                    GalleryName = gallery?.Name,
                    Images = items?.Select(x => new PhotoGalleryImage() 
                    {
                        Id = x.Id, 
                        GalleryId = x.GalleryId,
                        GalleryName = x.GalleryName,
                        Name = Path.GetFileName(x.Title),
                        UploadedBy = x.UploadedBy,
                        UploadDate = x.UploadedDate,
                        ImagePath = $"/{Path.Combine(UploadsDirectory, x.GalleryName).Remove(_hostingEnvironment.WebRootPath).Replace('\\', '/').TrimStart('/')}/{x.Title}",
                        ThumbnailPath = $"/{Path.Combine(ThumbnailsDirectory, x.GalleryName).Remove(_hostingEnvironment.WebRootPath).Replace('\\', '/').TrimStart('/')}/{Path.GetFileNameWithoutExtension(x.Title)}.webp",
                        ThumbnailPathFallback = $"/{ThumbnailsDirectory.Remove(_hostingEnvironment.WebRootPath).Replace('\\', '/').TrimStart('/')}/{Path.GetFileNameWithoutExtension(x.Title)}.webp",
                        MediaType = x.MediaType
                    })?.ToList(),
                    CurrentPage = currentPage,
                    ApprovedCount = (int)itemCounts["Approved"],
                    PendingCount = (int)itemCounts["Pending"],
                    ItemsPerPage = itemsPerPage,
                    FileUploader =  fileUploader,
                    ViewMode = (ViewMode)ViewBag.ViewMode,
                    GroupBy = group,
                    OrderBy = order,
                    Pagination = order != GalleryOrder.Random,
                    LoadScripts = !partial
                };
            
                return partial ? PartialView("~/Views/Gallery/GalleryWrapper.cshtml", model) : View(model);
            }

            return new RedirectToActionResult("Index", "Error", new { Reason = ErrorCode.InvalidGalleryId }, false);
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage()
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;

            try
            {
                string galleryId = (Request?.Form?.FirstOrDefault(x => string.Equals("Id", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString()?.ToLower() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(galleryId))
                {
                    return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Invalid_Gallery_Id"].Value } });
                }
                
                var gallery = await _database.GetGallery(galleryId);
                if (gallery != null)
                {
                    var secretKey = await _settings.GetOrDefault(Settings.Gallery.SecretKey, string.Empty, galleryId);
                    string key = (Request?.Form?.FirstOrDefault(x => string.Equals("SecretKey", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(secretKey) && !string.Equals(secretKey, key))
                    {
                        return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Invalid_Secret_Key_Warning"].Value } });
                    }

                    string uploadedBy = HttpContext.Session.GetString(SessionKey.ViewerIdentity)?.Trim() ?? "Anonymous";
                
                    var files = Request?.Form?.Files;
                    if (files != null && files.Count > 0)
                    {
                        var requiresReview = await _settings.GetOrDefault(Settings.Gallery.RequireReview, true, galleryId);

                        var uploaded = 0;
                        var errors = new List<string>();
                        foreach (IFormFile file in files)
                        {
                            try
                            {
                                var extension = Path.GetExtension(file.FileName);
                                var maxGallerySize = await _settings.GetOrDefault(Settings.Gallery.MaxSizeMB, 1024L, galleryId) * 1000000;
                                var maxFilesSize = await _settings.GetOrDefault(Settings.Gallery.MaxFileSizeMB, 50L, galleryId) * 1000000;
                                var galleryPath = Path.Combine(UploadsDirectory, gallery.Name);

                                var allowedFileTypes = (await _settings.GetOrDefault(Settings.Gallery.AllowedFileTypes, ".jpg,.jpeg,.png,.mp4,.mov", galleryId)).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                                if (!allowedFileTypes.Any(x => string.Equals(x.Trim('.'), extension.Trim('.'), StringComparison.OrdinalIgnoreCase)))
                                {
                                    errors.Add($"{_localizer["File_Upload_Failed"].Value}. {_localizer["Invalid_File_Type"].Value}");
                                }
                                else if (file.Length > maxFilesSize)
                                {
                                    errors.Add($"{_localizer["File_Upload_Failed"].Value}. {_localizer["Max_File_Size"].Value} {maxFilesSize} bytes");
                                }
                                else if ((_fileHelper.GetDirectorySize(galleryPath) + file.Length) > maxGallerySize)
                                {
                                    errors.Add($"{_localizer["File_Upload_Failed"].Value}. {_localizer["Gallery_Full"].Value} {maxGallerySize} bytes");
                                }
                                else
                                {
                                    var fileName = _fileHelper.SanitizeFilename($"{(!string.IsNullOrWhiteSpace(uploadedBy) ? $"{uploadedBy.Replace(" ", "_")}-" : string.Empty)}{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
                                    galleryPath = requiresReview ? Path.Combine(galleryPath, "Pending") : galleryPath;
                                    
                                    _fileHelper.CreateDirectoryIfNotExists(galleryPath);

                                    var filePath = Path.Combine(galleryPath, fileName);
                                    if (!string.IsNullOrWhiteSpace(filePath))
                                    {
                                        var isDemoMode = await _settings.GetOrDefault(Settings.IsDemoMode, false);
                                        if (!isDemoMode)
                                        {
                                            await _fileHelper.SaveFile(file, filePath, FileMode.Create);
                                        }
                                        else
                                        {
                                            System.IO.File.Copy(Path.Combine(ImagesDirectory, $"DemoImage.png"), filePath, true);
                                        }

                                        var checksum = await _fileHelper.GetChecksum(filePath);
                                        if (await _settings.GetOrDefault(Settings.Gallery.PreventDuplicates, true, galleryId) && (string.IsNullOrWhiteSpace(checksum) || await _database.GetGalleryItemByChecksum(gallery.Id, checksum) != null))
                                        {
                                            errors.Add($"{_localizer["File_Upload_Failed"].Value}. {_localizer["Duplicate_Item_Detected"].Value}");
                                            _fileHelper.DeleteFileIfExists(filePath);
                                        }
                                        else
                                        {
                                            var gallerySavePath = Path.Combine(ThumbnailsDirectory, gallery.Name);

                                            _fileHelper.CreateDirectoryIfNotExists(ThumbnailsDirectory);
                                            _fileHelper.CreateDirectoryIfNotExists(gallerySavePath);

                                            var savePath = Path.Combine(gallerySavePath, $"{Path.GetFileNameWithoutExtension(filePath)}.webp");
                                            await _imageHelper.GenerateThumbnail(filePath, savePath, await _settings.GetOrDefault(Settings.Basic.ThumbnailSize, 720));
                                            
                                            var item = await _database.AddGalleryItem(new GalleryItemModel()
                                            {
                                                GalleryId = gallery.Id,
                                                Title = fileName,
                                                UploadedBy = uploadedBy,
                                                UploadedDate = await _fileHelper.GetCreationDatetime(filePath),
                                                Checksum = checksum,
                                                MediaType = _imageHelper.GetMediaType(filePath),
                                                Orientation = await _imageHelper.GetOrientation(savePath),
                                                State = requiresReview ? GalleryItemState.Pending : GalleryItemState.Approved,
                                                FileSize = file.Length,
                                            });

                                            if (item?.Id > 0)
                                            { 
                                                uploaded++;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"{_localizer["Save_To_Gallery_Failed"].Value} - {ex?.Message}");
                            }
                        }

						Response.StatusCode = (int)HttpStatusCode.OK;

						return Json(new { success = uploaded > 0, uploaded, uploadedBy, requiresReview, errors });
                    }
                    else
                    {
                        return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["No_Files_For_Upload"].Value } });
                    }
                }
                else
                {
                    return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Gallery_Does_Not_Exist"].Value } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_localizer["Image_Upload_Failed"].Value} - {ex?.Message}");
            }

            return Json(new { success = false, uploaded = 0 });
        }

        [HttpPost]
        public async Task<IActionResult> UploadCompleted()
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;

            try
            {
                string galleryId = (Request?.Form?.FirstOrDefault(x => string.Equals("Id", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString()?.ToLower() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(galleryId))
                {
                    return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Invalid_Gallery_Id"].Value } });
                }

                var gallery = await _database.GetGallery(galleryId);
                if (gallery != null)
                {
                    var secretKey = await _settings.GetOrDefault(Settings.Gallery.SecretKey, string.Empty, galleryId);
                    string key = (Request?.Form?.FirstOrDefault(x => string.Equals("SecretKey", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(secretKey) && !string.Equals(secretKey, key))
                    {
                        return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Invalid_Secret_Key_Warning"].Value } });
                    }

                    var uploadedBy = HttpContext.Session.GetString(SessionKey.ViewerIdentity) ?? "Anonymous";
                    var requiresReview = await _settings.GetOrDefault(Settings.Gallery.RequireReview, true, galleryId);

                    int uploaded = int.Parse((Request?.Form?.FirstOrDefault(x => string.Equals("Count", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString() ?? "0");
                    if (uploaded > 0 && requiresReview && await _settings.GetOrDefault(Notifications.Alerts.PendingReview, true))
                    {
                        await _notificationHelper.Send(_localizer["New_Items_Pending_Review"].Value, $"{uploaded} new item(s) have been uploaded to gallery '{gallery.Name}' by '{(!string.IsNullOrWhiteSpace(uploadedBy) ? uploadedBy : "Anonymous")}' and are awaiting your review.", _urlHelper.GenerateBaseUrl(HttpContext?.Request, "/Account"));
                    }

                    Response.StatusCode = (int)HttpStatusCode.OK;

                    return Json(new { success = true, counters = new { total = gallery?.TotalItems ?? 0, approved = gallery?.ApprovedItems ?? 0, pending = gallery?.PendingItems ?? 0 }, uploaded, uploadedBy, requiresReview });
                }
                else
                {
                    return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Gallery_Does_Not_Exist"].Value } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_localizer["Image_Upload_Failed"].Value} - {ex?.Message}");
            }

            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> DownloadGallery(int id, string? secretKey, string? group)
        {
            try
            {
                var gallery = await _database.GetGallery(id);
                if (gallery != null)
                {
                    secretKey = secretKey ?? string.Empty;

                    var gallerySecret = await _settings.GetOrDefault(Settings.Gallery.SecretKey, string.Empty, gallery.Name);
                    if (!secretKey.Equals(gallerySecret))
                    {
                        return Json(new { success = false, message = _localizer["Failed_Download_Gallery_Invalid_Key"].Value });
                    }

                    if (await _settings.GetOrDefault(Settings.Gallery.Download, true, gallery?.Name) || (User?.Identity != null && User.Identity.IsAuthenticated))
                    {
                        var galleryDir = id > 0 ? Path.Combine(UploadsDirectory, gallery.Name) : UploadsDirectory;
                        if (_fileHelper.DirectoryExists(galleryDir))
                        {
                            var keepFiles = new List<string>();
                            if (!string.IsNullOrWhiteSpace(group))
                            {
                                var groupParts = group.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                                if (groupParts != null && groupParts.Length == 2)
                                {
                                    var items = await _database.GetAllGalleryItems(id, GalleryItemState.Approved);
                                    foreach (GalleryGroup type in Enum.GetValues(typeof(GalleryGroup)))
                                    {
                                        if (((int)type).ToString().Equals(groupParts[0]))
                                        {
                                            try
                                            {
                                                IEnumerable<IGrouping<string, GalleryItemModel>>? filtered = null;
                                                switch (type)
                                                {
                                                    case GalleryGroup.Date:
                                                        filtered = items?.GroupBy(x => x.UploadedDate != null ? x.UploadedDate.Value.ToString("dddd, d MMMM yyyy") : "Unknown");
                                                        break;
                                                    case GalleryGroup.MediaType:
                                                        filtered = items?.GroupBy(x => x.MediaType.ToString());
                                                        break;
                                                    case GalleryGroup.Uploader:
                                                        filtered = items?.GroupBy(x => x.UploadedBy ?? "Anonymous");
                                                        break;
                                                }

                                                if (filtered != null)
                                                {
                                                    foreach (var f in filtered)
                                                    {
                                                        if (f.Key.Equals(groupParts[1]))
                                                        {
                                                            if (f.Any())
                                                            {
                                                                keepFiles.AddRange(f.Select(x => x.Title));
                                                            }

                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                            catch { }

                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    return Json(new { success = false, message = _localizer["Failed_Download_Gallery"].Value });
                                }
                            }

                            _fileHelper.CreateDirectoryIfNotExists(TempDirectory);

                            var tempZipFile = Path.Combine(TempDirectory, $"{gallery.Name}-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.zip");
                            ZipFile.CreateFromDirectory(galleryDir, tempZipFile, CompressionLevel.Optimal, false);

                            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                            {
                                using (var fs = new FileStream(tempZipFile, FileMode.Open, FileAccess.ReadWrite))
                                using (var archive = new ZipArchive(fs, ZipArchiveMode.Update, false))
                                {
                                    foreach (var entry in archive.Entries.Where(x => x.FullName.StartsWith("Pending/", StringComparison.OrdinalIgnoreCase) || x.FullName.StartsWith("Rejected/", StringComparison.OrdinalIgnoreCase)).ToList())
                                    {
                                        entry.Delete();
                                    }

                                    if (keepFiles.Any())
                                    {
                                        foreach (var entry in archive.Entries.Where(x => !keepFiles.Exists(y => Path.GetFileName(y).Equals(x.Name))).ToList())
                                        {
                                            entry.Delete();
                                        }
                                    }
                                }
                            }

                            return Json(new { success = true, filename = $"/temp/{Path.GetFileName(tempZipFile)}" });
                        }
                    }
                    else
                    {
                        return Json(new { success = false, message = _localizer["Download_Gallery_Not_Allowed"].Value });
                    }
                }
                else
                {
                    return Json(new { success = false, message = _localizer["Failed_Download_Gallery"].Value });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_localizer["Failed_Download_Gallery"].Value} - {ex?.Message}");
            }

            return Json(new { success = false });
        }
    }
}