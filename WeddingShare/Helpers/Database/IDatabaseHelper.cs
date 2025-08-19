using WeddingShare.Enums;
using WeddingShare.Models.Database;

namespace WeddingShare.Helpers.Database
{
    public interface IDatabaseHelper
    {
        Task<int> GetGalleryCount();
        Task<IEnumerable<string>> GetGalleryNames();
        Task<List<GalleryModel>> GetAllGalleries();
        Task<int?> GetGalleryId(string identifier);
        Task<int?> GetGalleryIdByName(string name);
        Task<string?> GetGalleryIdentifier(int id);
        Task<string?> GetGalleryName(int id);
        Task<GalleryModel?> GetGallery(int id);
        Task<GalleryModel?> AddGallery(GalleryModel model);
        Task<GalleryModel?> EditGallery(GalleryModel model);
        Task<bool> WipeGallery(GalleryModel model);
        Task<bool> WipeAllGalleries();
        Task<bool> DeleteGallery(GalleryModel model);
        Task<IDictionary<string, long>> GetGalleryItemCount(int? galleryId, GalleryItemState state = GalleryItemState.All, MediaType type = MediaType.All, ImageOrientation orientation = ImageOrientation.None);
        Task<List<GalleryItemModel>> GetAllGalleryItems(int? galleryId, GalleryItemState state = GalleryItemState.All, MediaType type = MediaType.All, ImageOrientation orientation = ImageOrientation.None, GalleryGroup group = GalleryGroup.None, GalleryOrder order = GalleryOrder.Descending, int limit = int.MaxValue, int page = 1);
        Task<int> GetPendingGalleryItemCount(int? galleryId = null);
        Task<List<GalleryItemModel>> GetPendingGalleryItems(int? galleryId = null);
        Task<GalleryItemModel?> GetPendingGalleryItem(int id);
        Task<GalleryItemModel?> GetGalleryItem(int id);
        Task<GalleryItemModel?> GetGalleryItemByChecksum(int galleryId, string checksum);
        Task<GalleryItemModel?> AddGalleryItem(GalleryItemModel model);
        Task<GalleryItemModel?> EditGalleryItem(GalleryItemModel model);
        Task<bool> DeleteGalleryItem(GalleryItemModel model);
        Task<bool> InitOwnerAccount(UserModel model);
        Task<bool> ValidateCredentials(string username, string password);
        Task<List<UserModel>?> GetAllUsers();
        Task<UserModel?> GetUser(int id);
        Task<UserModel?> GetUser(string name);
        Task<UserModel?> AddUser(UserModel model);
        Task<UserModel?> EditUser(UserModel model);
        Task<bool> DeleteUser(UserModel model);
        Task<bool> ChangePassword(UserModel model);
        Task<int> IncrementLockoutCount(int id);
        Task<bool> SetLockout(int id, DateTime? datetime);
        Task<bool> ResetLockoutCount(int id);
        Task<bool> SetMultiFactorToken(int id, string token);
        Task<bool> ResetMultiFactorToDefault();
        Task<bool> Import(string path);
        Task<bool> Export(string path);
        Task<IEnumerable<SettingModel>?> GetAllSettings(int? galleryId = null);
        Task<SettingModel?> GetSetting(string id);
        Task<SettingModel?> GetSetting(string id, int gallery);
        Task<SettingModel?> GetGallerySpecificSetting(string id, int galleryId);
        Task<SettingModel?> AddSetting(SettingModel model, int? galleryId = null);
        Task<SettingModel?> EditSetting(SettingModel model, int? galleryId = null);
        Task<SettingModel?> SetSetting(SettingModel model, int? galleryId = null);
        Task<bool> DeleteSetting(SettingModel model, int? galleryId = null);
        Task<bool> DeleteAllSettings(int? galleryId = null);
        Task<CustomResourceModel?> GetCustomResource(int id);
        Task<List<CustomResourceModel>> GetAllCustomResources();
        Task<CustomResourceModel?> AddCustomResource(CustomResourceModel model);
        Task<CustomResourceModel?> EditCustomResource(CustomResourceModel model);
        Task<bool> DeleteCustomResource(CustomResourceModel model);
        Task<IEnumerable<AuditLogModel>?> GetAuditLogs(string term = "", int limit = 100);
        Task<AuditLogModel?> AddAuditLog(AuditLogModel model);
    }
}