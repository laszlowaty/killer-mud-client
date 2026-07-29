namespace MudClient.App.Services;

public interface IPasswordProtector
{
    string Protect(string? plainText);

    string Unprotect(string? protectedText);
}

public sealed class DpapiPasswordProtector : IPasswordProtector
{
    public string Protect(string? plainText) => PasswordProtector.Protect(plainText);

    public string Unprotect(string? protectedText) => PasswordProtector.Unprotect(protectedText);
}
