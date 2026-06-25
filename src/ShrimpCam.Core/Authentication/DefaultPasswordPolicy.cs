namespace ShrimpCam.Core.Authentication;

public sealed class DefaultPasswordPolicy : IPasswordPolicy
{
    public bool IsSatisfiedBy(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            return false;
        }

        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;

        foreach (var character in password)
        {
            hasUpper |= char.IsUpper(character);
            hasLower |= char.IsLower(character);
            hasDigit |= char.IsDigit(character);
        }

        return hasUpper && hasLower && hasDigit;
    }
}
