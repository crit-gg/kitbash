namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// The string has to say something. Whitespace counts as nothing, since a file holding
/// <c>name = " "</c> means the same as one holding nothing.
/// </summary>
public sealed class NotBlankRule : ISettingRule<string>
{
    public string Summary => "Cannot be empty";

    public bool Allows(string value, out string? reason)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reason = "This cannot be empty.";
            return false;
        }

        reason = null;
        return true;
    }
}
