using System.Data.Common;

namespace Neemle.OrmLite;

public partial interface IMaintenanceProvider
{
    /// <summary>
    /// Renders the integrity check of the whole database or of <paramref name="table"/>. A whole-database form that can
    /// bound its own work is asked for <paramref name="maxFindings"/> + 1 rows; the core reads at most that many.
    /// </summary>
    string RenderIntegrityCheck(IntegrityCheckMode mode, EntityDescriptor? table, int maxFindings) =>
        throw new NotSupportedException("this maintenance provider does not render an integrity check.");

    /// <summary>The finding the current row of the check reports, or null for a row that reports none (SQLite's <c>ok</c>).</summary>
    IntegrityFinding? ReadIntegrityFinding(IntegrityCheckMode mode, DbDataReader row) =>
        throw new NotSupportedException("this maintenance provider does not read integrity findings.");

    /// <summary>
    /// The most findings the engine itself reports for this form of the check, whatever was asked for, or null when the
    /// form can report every finding up to the request's limit. A check that reaches the cap is reported as
    /// <see cref="IntegrityCheckResult.Truncated"/>: its list may be incomplete. Null by default.
    /// </summary>
    int? IntegrityEngineCap(IntegrityCheckMode mode, EntityDescriptor? table) => null;
}
