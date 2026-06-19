using System.Globalization; //Dùng để làm việc với Unicode và ký tự có dấu.
using System.Text; //Dùng cho: StringBuilder để nối chuỗi hiệu quả hơn.

namespace BunnyChat.Helper
{
    //hỗ trợ 
    public static class StringHelper
    {
        //hỗ trợ bỏ dấu tiếng việt
        public static string RemoveVietnameseDiacritics(string text)
        {
            //kiểm tra chuỗi rỗng. trả về ""
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Chuyển Unicode sang FormD
            text = text.Normalize(NormalizationForm.FormD);

            // Console.WriteLine(text);

            // Tạo StringBuilder => Dùng để ghép lại chuỗi sau khi bỏ dấu.
            var sb = new StringBuilder();

            // Duyệt từng ký tự
            foreach (char c in text)
            {
                // Lấy loại Unicode
                var unicodeCategory =
                    CharUnicodeInfo.GetUnicodeCategory(c);

                // Bỏ các dấu. NonSpacingMark Là các dấu tronmg tiếng Việt.
                //  Nếu gặp dấu: KHÔNG thêm vào. Nếu là chữ: THÊM vào
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            // Chuyển lại FormC. Ghép chuỗi về unicode
            return sb.ToString()
                     .Normalize(NormalizationForm.FormC)
                     .Replace('đ', 'd')
                     .Replace('Đ', 'D');
        }
    }
}
