namespace ShrimpCam.Core.Audit;

public static class AuditEventTypes
{
    public const string AuthorizationDenied = "AuthorizationDenied";
    public const string BootstrapAdministrator = "BootstrapAdministrator";
    public const string BackupRestored = "BackupRestored";
    public const string SettingsUpdated = "SettingsUpdated";
    public const string SignIn = "SignIn";
    public const string SignOut = "SignOut";
}
