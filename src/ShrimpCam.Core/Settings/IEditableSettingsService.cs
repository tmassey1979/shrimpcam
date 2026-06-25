namespace ShrimpCam.Core.Settings;

public interface IEditableSettingsService
{
    Task<EditableSettings> GetCurrentAsync(CancellationToken cancellationToken);

    EditableSettingsValidationResult Validate(EditableSettings settings);

    Task<EditableSettings> UpdateAsync(EditableSettings settings, CancellationToken cancellationToken);
}
