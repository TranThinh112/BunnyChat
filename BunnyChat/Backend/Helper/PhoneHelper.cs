namespace BunnyChat.Helper
{
    //hỗ trợ lưu sđt khi nhập 0 tự lưu thành +84
    public static class PhoneHelper
    {
        public static string? NormalizeToInternational(string? phone)
        {
            // kiểm tra rỗng 
            if (string.IsNullOrWhiteSpace(phone))
                return null;
            // bỏ ký tự thwaff 
            phone = phone.Trim()
                         .Replace(" ", "")
                         .Replace("-", "")
                         .Replace(".", "");

            // nếu bắt đầu bằng số 0, SubString ghép chuỗi => +84
            if (phone.StartsWith("0"))
                return "+84" + phone.Substring(1);

            if (phone.StartsWith("84"))
                return "+" + phone;

            return phone;
        }
    }
}