namespace Kitbash.Settings;

/// <summary>How the launcher comes up. Read once, as the launcher starts.</summary>
public interface IStartupSettings
{
    StartPage StartPage { get; }
}
