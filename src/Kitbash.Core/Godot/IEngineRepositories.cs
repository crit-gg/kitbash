namespace Kitbash.Core.Godot;

/// <summary>The repository behind an address, made once and kept.</summary>
public interface IEngineRepositories
{
    /// <summary>The official Godot list for null, otherwise the repository at that address.</summary>
    IEngineRepository For(EngineRepositoryAddress? address);
}
