using OtpNet;

namespace Toolshop.Playwright.Tests.Utilities;

/// <summary>
/// Computes the current RFC 6238 TOTP code for a Base32 secret, matching the app's own backend
/// (<c>PragmaRX\Google2FA</c>, SHA1/30s/6-digit defaults - the same defaults <c>Otp.NET</c> uses).
/// </summary>
public static class TotpCodeGenerator
{
    public static string ComputeCurrentCode(string secret)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.ComputeTotp();
    }
}
