using System.Net.Mail;
using System.Text.RegularExpressions;

namespace WeddingShare.Helpers
{
    public class EmailValidationHelper
    {
        public static bool IsValid(string? email)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                try
                {
                    var mail = new MailAddress(email);
                    return Regex.IsMatch(mail.Address, @"^.+?\@.+?\..+?$", RegexOptions.Compiled);
                }
                catch { }
            }
                
            return false;
        }
    }
}