using System;
using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite;

/// <summary>
/// Runs the emit step of a generator so that an exception in it becomes an error diagnostic that names the
/// exception, instead of the compiler's CS8785 and no output at all. A failure is a defect of the generator, not of
/// the declarations, and the message says so; cancellation is never swallowed.
/// </summary>
internal static class GeneratorGuard
{
    private static readonly DiagnosticDescriptor Failed = new DiagnosticDescriptor(
        id: "ORMLITE031",
        title: "The generator failed on this input",
        messageFormat:
            "{0} failed on this input and produced no output: {1}: {2}. This is a defect of the generator, not of "
            + "your declarations; report it together with the declaration that triggers it.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static void Run(SourceProductionContext spc, string generator, Action emit)
    {
        try
        {
            emit();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception failure)
        {
            var kind = failure.GetType().Name;
            spc.ReportDiagnostic(Diagnostic.Create(Failed, Location.None, generator, kind, FirstLine(failure)));
        }
    }

    private static string FirstLine(Exception failure)
    {
        var text = failure.Message;
        var end = text.IndexOfAny(['\r', '\n']);
        return end < 0 ? text : text.Substring(0, end);
    }
}
