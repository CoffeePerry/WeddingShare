namespace WeddingShare.Helpers
{
    public class GalleryHelper
    {
        public static string GenerateGalleryIdentifier()
        {
            return Guid.NewGuid().ToString().Replace("-", string.Empty).ToLower();
        }
    }
}