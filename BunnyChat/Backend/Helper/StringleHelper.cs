using System.Globalization;
using System.Text;

namespace BunnyChat.Helper
{
    public static class StringHelper
    {
        public static string RemoveVietnameseDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder();

            foreach (char c in text)
            {
                var unicodeCategory =
                    CharUnicodeInfo.GetUnicodeCategory(c);

                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString()
                     .Normalize(NormalizationForm.FormC)
                     .Replace('đ', 'd')
                     .Replace('Đ', 'D');
        }
    }
}