using Lidstroem.Core.Interfaces;

namespace Lidstroem.Infrastructure.Auth;

public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plaintext) =>
        BCrypt.Net.BCrypt.HashPassword(plaintext, WorkFactor);

    public bool Verify(string plaintext, string hash) =>
        BCrypt.Net.BCrypt.Verify(plaintext, hash);
}
