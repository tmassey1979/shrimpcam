namespace ShrimpCam.Core.Settings;

public sealed record EditableSettingsValidationResult(
    bool IsValid,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public static EditableSettingsValidationResult Success() =>
        new(true, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));

    public static EditableSettingsValidationResult Failure(IReadOnlyDictionary<string, string[]> errors) =>
        new(false, errors);
}
