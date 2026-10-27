namespace Tscs;

/// <summary>
/// Explicitly implemented compiler target values. Numeric values match the
/// frozen TS7 option declarations; unsupported targets are not treated as
/// accepted options.
/// </summary>
public enum CompilerTarget
{
    Unspecified=0,
    Es2015=2
}

/// <summary>
/// Effective JavaScript module mode. This does not implement TypeScript's
/// other module transformations or default inference.
/// </summary>
public enum CompilerModule
{
    Unspecified=0,
    Es2015=5
}

/// <summary>
/// Immutable, typed options shared between CLI configuration and the loaded
/// Program snapshot. A new Program is needed after options or sources change.
/// </summary>
public sealed record CompilerOptions
{
    public bool NoEmit { get; init; }
    public bool NoLib { get; init; }
    public CompilerTarget Target { get; init; }
    public CompilerModule Module { get; init; }
}
