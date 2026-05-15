using WebMoi.Models.Entities;


namespace WebMoi.Service
{
    public interface ITokenService
    {
        string CreateAccessToken(User user);
        string CreateRefreshToken();
    }
}