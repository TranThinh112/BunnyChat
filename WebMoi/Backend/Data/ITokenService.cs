using System;
using WebMoi.Models.Entities;

/// <summary>
/// Summary description for Class1
/// </summary>
namespace WebMoi.Data
{
	public interface ITokenService
	{
		string CreateAccessToken(User user, IList<String> roles);

		RefreshToken CreaterRefreshToken(string ipAddress);

    }
}
