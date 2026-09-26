namespace Battlegrounds.Content;

public sealed class ModValidator
{
    public ModValidationReport Validate(string modDirectory) =>
        new DirectoryModValidator().Validate(modDirectory);
}
