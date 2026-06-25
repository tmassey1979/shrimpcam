namespace ShrimpCam.Core.Authentication;

public interface IPasswordPolicy
{
    bool IsSatisfiedBy(string password);
}
