namespace Workbench.Core.Godot;

/// <summary>
/// The kind of a Godot release. Declared in release order, so comparing two of these
/// ranks a dev snapshot below a release candidate.
/// </summary>
/// <remarks>
/// <para>
/// Closed on purpose. Counted over the 356 releases in the version feed on 2 August 2026,
/// these five are the only labels Godot has ever used: rc 120, beta 94, stable 72, dev 42
/// and alpha 27. A sixth would need a rank, a rank is a decision rather than something to
/// infer, and a label sorted somewhere arbitrary is worse than one refused.
/// </para>
/// <para>
/// gdvm's table omits alpha, which ranks it below dev. Do not repeat that.
/// </para>
/// </remarks>
public enum EngineChannel
{
    Dev,
    Alpha,
    Beta,
    Rc,
    Stable,
}
