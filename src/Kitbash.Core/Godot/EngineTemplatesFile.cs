using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

/// <summary>
/// The export templates a release published for one runtime. A <c>.tpz</c>, which is a zip.
/// </summary>
public sealed record EngineTemplatesFile(bool IsMono, string FileName, WebAddress Address);
