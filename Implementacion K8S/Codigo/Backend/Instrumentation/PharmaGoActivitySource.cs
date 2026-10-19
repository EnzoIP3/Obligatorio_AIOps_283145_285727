using System.Diagnostics;

namespace Instrumentation;

/// <summary>
/// Shared ActivitySource for application-level spans. ASP.NET Core and HTTP
/// instrumentation create the transport spans; this source is available for
/// business operations that need an explicit span.
/// </summary>
public static class PharmaGoActivitySource
{
    public const string Name = "PharmaGo";

    public static readonly ActivitySource Source = new(Name);
}
