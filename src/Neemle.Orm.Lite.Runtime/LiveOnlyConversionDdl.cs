#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neemle.Orm.Lite;

public static class LiveOnlyConversionDdl
{
    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]{0,62}\z", RegexOptions.CultureInvariant);

    private static readonly Regex VersionName = new(@"\A[a-z0-9][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    private static readonly Regex KeyTerm = new(
        @"\A(?:(?<plain>[a-z][a-z0-9_]{0,62})|lower\((?<lowered>[a-z][a-z0-9_]{0,62})\))\z", RegexOptions.CultureInvariant);

    public static string Up(
        string table, IReadOnlyList<string> keyTerms, string softColumn, string totalIndex, string liveIndex, bool liveIsDeclared, string version)
    {
        var key = Check(table, keyTerms, softColumn, totalIndex, liveIndex, version);
        var create = "CREATE UNIQUE INDEX IF NOT EXISTS \"" + liveIndex + "\" ON \"" + table + "\" ("
            + string.Join(", ", key.Terms) + ") WHERE \"" + softColumn + "\" IS NULL";
        return "DO $lc$ DECLARE owner_constraint name; BEGIN"
            + " PERFORM set_config('lock_timeout', '5s', true);"
            + " IF to_regclass('\"" + totalIndex + "\"') IS NOT NULL THEN"
            + " " + create + ";"
            + " COMMENT ON INDEX \"" + liveIndex + "\" IS '" + version + "';"
            + " SELECT c.conname INTO owner_constraint FROM pg_constraint c"
            + " WHERE c.conrelid = to_regclass('\"" + table + "\"') AND c.conindid = to_regclass('\"" + totalIndex + "\"')"
            + " AND c.contype IN ('u', 'p');"
            + " IF owner_constraint IS NULL THEN DROP INDEX \"" + totalIndex + "\";"
            + " ELSE EXECUTE format('ALTER TABLE \"" + table + "\" DROP CONSTRAINT %I', owner_constraint); END IF;"
            + " ELSE " + create + "; END IF;"
            + " END $lc$";
    }

    public static string Down(
        string table, IReadOnlyList<string> keyTerms, string softColumn, string totalIndex, string liveIndex, bool liveIsDeclared, string version)
    {
        var key = Check(table, keyTerms, softColumn, totalIndex, liveIndex, version);
        var present = string.Join(" AND ", key.Columns.Select(c => "\"" + c + "\" IS NOT NULL"));
        var texts = string.Join(", ", key.Terms.Select(t => t + "::text"));
        var groups = string.Join(", ", key.Terms);
        return "DO $lc$ DECLARE duplicate_key text; BEGIN"
            + " PERFORM set_config('lock_timeout', '5s', true);"
            + " IF coalesce(obj_description(to_regclass('\"" + liveIndex + "\"'), 'pg_class'), '') = '" + version + "' THEN"
            + " SELECT concat_ws(', ', " + texts + ") INTO duplicate_key FROM \"" + table + "\""
            + " WHERE " + present + " GROUP BY " + groups + " HAVING count(*) > 1 LIMIT 1;"
            + " IF duplicate_key IS NOT NULL THEN"
            + " RAISE EXCEPTION 'cannot recreate unique index " + totalIndex + " on " + table + " (" + string.Join(", ", keyTerms)
            + "): duplicate key % exists, nothing was deleted', quote_literal(duplicate_key);"
            + " END IF;"
            + " CREATE UNIQUE INDEX IF NOT EXISTS \"" + totalIndex + "\" ON \"" + table + "\" (" + groups + ");"
            + (liveIsDeclared
                ? " COMMENT ON INDEX \"" + liveIndex + "\" IS NULL;"
                : " DROP INDEX \"" + liveIndex + "\";")
            + " END IF;"
            + " END $lc$";
    }

    private sealed record Key(IReadOnlyList<string> Terms, IReadOnlyList<string> Columns);

    private static Key Check(
        string table, IReadOnlyList<string> keyTerms, string softColumn, string totalIndex, string liveIndex, string version)
    {
        RequireIdentifier(table, nameof(table));
        RequireIdentifier(softColumn, nameof(softColumn));
        RequireIdentifier(totalIndex, nameof(totalIndex));
        RequireIdentifier(liveIndex, nameof(liveIndex));
        if (!VersionName.IsMatch(version))
            throw new ArgumentException("not a migration version: " + version, nameof(version));
        if (keyTerms is null || keyTerms.Count == 0)
            throw new ArgumentException("a conversion needs at least one key term", nameof(keyTerms));

        var terms = new List<string>();
        var columns = new List<string>();
        foreach (var term in keyTerms)
        {
            var match = term is null ? Match.Empty : KeyTerm.Match(term);
            if (!match.Success)
                throw new ArgumentException("not a key term: " + term, nameof(keyTerms));
            if (match.Groups["plain"].Success)
            {
                terms.Add("\"" + match.Groups["plain"].Value + "\"");
                columns.Add(match.Groups["plain"].Value);
            }
            else
            {
                terms.Add("lower(\"" + match.Groups["lowered"].Value + "\")");
                columns.Add(match.Groups["lowered"].Value);
            }
        }
        return new Key(terms, columns.Distinct(StringComparer.Ordinal).ToList());
    }

    private static void RequireIdentifier(string value, string parameter)
    {
        if (!Identifier.IsMatch(value))
            throw new ArgumentException("not an identifier: " + value, parameter);
    }
}
