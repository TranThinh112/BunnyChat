using BunnyChat.Models.Entities;


namespace BunnyChat.Service
{
    public interface ITokenService
    {
        string CreateAccessToken(User user);
        string CreateRefreshToken();
    }
}