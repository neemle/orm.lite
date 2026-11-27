using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresLowering
{
    private const string Tag = "rh";

    private static readonly string[] SourceRowColumns = ["source_table", "row_id", "id"];

    private static readonly string[] TenantXactColumns = ["tenant_id", "xact_id"];

    private static readonly string[] ChangedAtColumns = ["changed_at"];

    internal static Node Lower(Node tree) => tree switch
    {
        ConvertForeignKeyAction n => ConvertForeignKey(n),
        ConvertToLiveOnlyIndex { Revert: false } n => LiveOnlyUp(n),
        ConvertToLiveOnlyIndex n => LiveOnlyDown(n),
        SoftDeleteColumn { Remove: false } n => SoftColumnUp(n),
        SoftDeleteColumn n => SoftColumnDown(n),
        RowHistoryFunction n => new PgDataLiteral(CaptureFunction(n.ActorSetting)),
        RowHistoryInstall n => Install(n),
        ReconcileIndexes n => Reconcile(n),
        CreateView n => MarkedView(n),
        _ => tree,
    };

    internal static string ForeignKeyCode(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => "c",
        ReferentialAction.SetNull => "n",
        ReferentialAction.NoAction => "a",
        ReferentialAction.Restrict => "r",
        _ => throw new ArgumentException($"no catalog code for the referential action {action}.", nameof(action)),
    };

    private static StringLit S(string value) => new(value);

    private static PgName Id(string text) => new(text);

    private static NodeList L(params Node[] nodes) => NodeList.Of(nodes);

    private static PgCall Call(string function, params Node[] arguments) => new(function, NodeList.Of(arguments));

    private static Compare Eq(Node left, Node right) => new(CompareOp.Equal, left, right);

    private static PgCall Regclass(string name) => Call("to_regclass", PostgresNames.Regclass(name));

    private static PgPerform LockTimeout() =>
        new(Call("set_config", S(PostgresDdlDataLiterals.LockTimeoutSetting), S(PostgresDdlDataLiterals.LockTimeoutValue), new BoolLit(true)));

    private static Node ConvertForeignKey(ConvertForeignKeyAction n)
    {
        var lookup = new PgQuery(
            L(new PgCast(Id("c.confdeltype"), "text")),
            new PgRelation("pg_constraint", "c"),
            new Where(L(Eq(Id("c.contype"), S("f")), Eq(Id("c.conname"), S(n.Constraint)), Eq(Id("c.conrelid"), Regclass(n.Table)))),
            Into: "current_action");
        var replace = new PgReplaceConstraint(
            n.Table, n.Constraint,
            new AddForeignKey(n.Constraint, n.Table, n.Columns, n.ReferencedTable, n.ReferencedColumns, n.Target, n.OnUpdate));
        var differs = new PgAll(L(
            new IsNotNull(Id("current_action")),
            new Compare(CompareOp.NotEqual, Id("current_action"), S(ForeignKeyCode(n.Target)))));
        return new PgBlock("fk", L(new PgDeclare("current_action", "text")), L(LockTimeout(), lookup, new PgIf(differs, L(replace), NodeList.Empty)));
    }

    private static Node MarkedView(CreateView n)
    {
        var create = new PgDataLiteral(new PostgresSerializer().Serialize(n).Text);
        var marker = new PgComment("VIEW", new TableRef(n.View), null, S(ViewDefinitions.Marker(PostgresProvider.Instance, n)));
        return new Script(L(create, marker));
    }

    private static string ColumnOf(Node term) => term switch
    {
        ColumnRef c => c.Column,
        Call { Function: Fn.Lower, Arguments: [ColumnRef c] } => c.Column,
        _ => throw new ArgumentException($"a key term is a column or a lowered column, not {term.Name}.", nameof(term)),
    };

    private static PostgresNames.Escaped TermText(Node term) => term switch
    {
        ColumnRef c => PostgresNames.Percent(c.Column),
        _ => PostgresNames.Splice(PostgresDdlDataLiterals.LoweredKeyTerm, PostgresNames.Percent(ColumnOf(term))),
    };

    private static string DropOwnerTemplate(string table) => PostgresNames.Splice(
        PostgresDdlDataLiterals.DropOwnerConstraintTemplate,
        PostgresNames.FormatIdent(table)).Text;

    private static Node LiveOnlyUp(ConvertToLiveOnlyIndex n)
    {
        var create = new CreateIndex(n.LiveIndex, n.Table, n.KeyTerms, true, new Where(L(new IsNull(new ColumnRef(n.SoftColumn)))));
        var total = Id(PostgresDdlDataLiterals.TotalIndexVariable);
        var owner = new PgQuery(
            L(Id("c.conname")),
            new PgRelation("pg_constraint", "c"),
            new Where(L(
                Eq(Id("c.conrelid"), Regclass(n.Table)),
                Eq(Id("c.conindid"), total),
                new InList(Id("c.contype"), [S("u"), S("p")]))),
            Into: "owner_constraint");
        var dropOwner = new PgIf(
            new IsNull(Id("owner_constraint")),
            L(new PgDataLiteral(PostgresDdlDataLiterals.DropTotalByOid)),
            L(new PgExecuteFormat(S(DropOwnerTemplate(n.Table)), Id("owner_constraint"))));
        var present = new PgIf(
            new IsNotNull(total),
            L(create, new PgComment("INDEX", new TableRef(n.LiveIndex), null, S(n.Version)), owner, dropOwner),
            L(create));
        var declarations = L(new PgDeclare("owner_constraint", "name"), IndexVariable(true), IndexVariable(false));
        return new PgBlock("lc", declarations, L(
            LockTimeout(),
            IndexInto(n.Table, n.TotalIndex, PostgresDdlDataLiterals.TotalIndexVariable),
            IndexInto(n.Table, n.LiveIndex, PostgresDdlDataLiterals.LiveIndexVariable),
            NameTaken(n.Table, n.LiveIndex, Id(PostgresDdlDataLiterals.LiveIndexVariable)),
            present));
    }

    private static PgDeclare IndexVariable(bool total) =>
        new(total ? PostgresDdlDataLiterals.TotalIndexVariable : PostgresDdlDataLiterals.LiveIndexVariable, "oid");

    private static PostgresNames.Escaped RecreateMessage(ConvertToLiveOnlyIndex n, IEnumerable<Node> terms) =>
        PostgresNames.Splice(
            PostgresDdlDataLiterals.RecreateIndexMessage,
            PostgresNames.Percent(n.TotalIndex),
            PostgresNames.Percent(n.Table),
            PostgresNames.List(terms.Select(TermText)));

    private static PgQuery DuplicateKey(ConvertToLiveOnlyIndex n, IReadOnlyList<Node> terms)
    {
        var columns = terms.Select(ColumnOf).Distinct(StringComparer.Ordinal).ToList();
        var texts = terms.Select(t => (Node)new PgCast(t, "text"));
        return new PgQuery(
            L(Call("concat_ws", [S(PostgresDdlDataLiterals.ListSeparator), .. texts])),
            new TableRef(n.Table),
            new Where(NodeList.Of(columns.Select(c => (Node)new IsNotNull(new ColumnRef(c))))),
            Into: "duplicate_key",
            GroupBy: n.KeyTerms,
            Having: new Compare(CompareOp.Greater, Call("count", new Star()), new IntLit(1)),
            Limit: 1);
    }

    private static Node ReleaseLive(ConvertToLiveOnlyIndex n) => n.LiveIsDeclared
        ? new PgExecuteFormat(
            S(PostgresDdlDataLiterals.CommentLiveNullTemplate),
            new PgCast(Id(PostgresDdlDataLiterals.LiveIndexVariable), "regclass"))
        : new PgDataLiteral(PostgresDdlDataLiterals.DropLiveByOid);

    private static Node LiveOnlyDown(ConvertToLiveOnlyIndex n)
    {
        var terms = n.KeyTerms.ToList();
        var live = Id(PostgresDdlDataLiterals.LiveIndexVariable);
        var refuse = new PgIf(
            new IsNotNull(Id("duplicate_key")),
            L(new PgRaise(S(RecreateMessage(n, terms).Text), L(Call("quote_literal", Id("duplicate_key"))))),
            NodeList.Empty);
        var marked = Eq(Call("coalesce", Call("obj_description", live, S("pg_class")), S("")), S(n.Version));
        var restore = new PgIf(
            marked,
            L(
                DuplicateKey(n, terms),
                refuse,
                NameTaken(n.Table, n.TotalIndex, Id(PostgresDdlDataLiterals.TotalIndexVariable)),
                new CreateIndex(n.TotalIndex, n.Table, n.KeyTerms, true),
                ReleaseLive(n)),
            NodeList.Empty);
        var declarations = L(new PgDeclare("duplicate_key", "text"), IndexVariable(true), IndexVariable(false));
        return new PgBlock("lc", declarations, L(
            LockTimeout(),
            IndexInto(n.Table, n.LiveIndex, PostgresDdlDataLiterals.LiveIndexVariable),
            IndexInto(n.Table, n.TotalIndex, PostgresDdlDataLiterals.TotalIndexVariable),
            restore));
    }

    private static Where SoftColumnFilter(string table) => new(L(
        Eq(Id("a.attrelid"), Regclass(table)),
        Eq(Id("a.attname"), S(SoftDeleteColumnDdl.Column)),
        new PgNegate(Id("a.attisdropped"))));

    private static Node SoftColumnUp(SoftDeleteColumn n)
    {
        var missing = new PgExists(true, new PgQuery(L(new IntLit(1)), new PgRelation("pg_attribute", "a"), SoftColumnFilter(n.Table)));
        var column = new ColumnDef(SoftDeleteColumnDdl.Column, new PgNullable(new TypeRef(ColumnType.Timestamp)));
        var add = new PgIf(
            missing,
            L(
                new AddColumn(n.Table, column, false),
                new PgComment("COLUMN", new ColumnRef(SoftDeleteColumnDdl.Column, new TableRef(n.Table)), null, S(n.Version))),
            NodeList.Empty);
        return new PgBlock("sd", NodeList.Empty, L(LockTimeout(), add));
    }

    private static string DropSoftMessage(string table) => PostgresNames.Splice(
        PostgresDdlDataLiterals.DropSoftColumnMessage,
        PostgresNames.Percent(SoftDeleteColumnDdl.Column),
        PostgresNames.Percent(table)).Text;

    private static Node SoftColumnDown(SoftDeleteColumn n)
    {
        var marker = new PgScalar(new PgQuery(
            L(Call("col_description", Id("a.attrelid"), Id("a.attnum"))),
            new PgRelation("pg_attribute", "a"),
            SoftColumnFilter(n.Table)));
        var count = new PgQuery(
            L(Call("count", new Star())),
            new TableRef(n.Table),
            new Where(L(new IsNotNull(new ColumnRef(SoftDeleteColumnDdl.Column)))),
            Into: "soft_rows");
        var refuse = new PgIf(
            new Compare(CompareOp.Greater, Id("soft_rows"), new IntLit(0)),
            L(new PgRaise(S(DropSoftMessage(n.Table)), L(Id("soft_rows")))),
            NodeList.Empty);
        var drop = new PgIf(
            Eq(Call("coalesce", marker, S("")), S(n.Version)),
            L(count, refuse, new DropColumn(n.Table, SoftDeleteColumnDdl.Column, false)),
            NodeList.Empty);
        return new PgBlock("sd", L(new PgDeclare("soft_rows", "bigint")), L(LockTimeout(), drop));
    }

    internal static string CaptureFunction(string actorSetting)
    {
        foreach (var ch in actorSetting)
            if (!(ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.'))
                throw new ArgumentException("an actor setting is lowercase letters, digits, underscores and one dot.", nameof(actorSetting));
        return PostgresDdlDataLiterals.CaptureFunctionHead + actorSetting + PostgresDdlDataLiterals.CaptureFunctionTail;
    }

    private static PgBlock GuardedIndex(string name, string[] columns) =>
        new(Tag, NodeList.Empty, L(new PgIf(
            new IsNull(Call("to_regclass", S(name))),
            L(new PgPlainIndex(name, PostgresDdlDataLiterals.RowHistoryTableName, NodeList.Of(columns.Select(c => (Node)Id(c))))),
            NodeList.Empty)));

    private static PgExists MissingTrigger(string table, string trigger, string hash) =>
        new(true, new PgQuery(
            L(new IntLit(1)),
            new PgRelation("pg_trigger", "g"),
            new Where(L(
                Eq(Id("g.tgrelid"), Regclass(table)),
                Eq(Id("g.tgname"), S(trigger)),
                Eq(Call("obj_description", Id("g.oid"), S("pg_trigger")), S(hash))))));

    private static PgBlock Attach(HistoryTableNode t)
    {
        var update = PostgresDdlDataLiterals.RowHistoryUpdateTrigger;
        var delete = PostgresDdlDataLiterals.RowHistoryDeleteTrigger;
        var arguments = NodeList.Of(
            new[] { t.KeyColumn, t.SoftColumn, t.TenantColumn }
                .Concat(t.Unversioned.Select(ColumnOf))
                .Select(a => (Node)S(a)));
        var function = PostgresDdlDataLiterals.RowHistoryFunctionName;
        var condition = new PgAll(L(
            new IsNotNull(Regclass(t.Table)),
            new Or(MissingTrigger(t.Table, update, t.ArgumentsHash), MissingTrigger(t.Table, delete, t.ArgumentsHash))));
        return new PgBlock(Tag, NodeList.Empty, L(new PgIf(
            condition,
            L(
                new PgCreateTrigger(update, "UPDATE", t.Table, true, function, arguments),
                new PgCreateTrigger(delete, "DELETE", t.Table, false, function, arguments),
                new PgComment("TRIGGER", Id(update), new TableRef(t.Table), S(t.ArgumentsHash)),
                new PgComment("TRIGGER", Id(delete), new TableRef(t.Table), S(t.ArgumentsHash))),
            NodeList.Empty)));
    }

    private static Node Reconcile(ReconcileIndexes n)
    {
        var statements = new List<Node> { new PgSetLocal(PostgresDdlDataLiterals.LockTimeoutSetting, S(PostgresDdlDataLiterals.LockTimeoutValue)) };
        foreach (var item in n.Indexes.Cast<ReconcileIndex>())
        {
            var columns = item.GuardColumns.Select(ColumnOf).ToList();
            statements.Add(Drift(item, columns));
            statements.Add(new PgBlock(
                "reconcile",
                NodeList.Empty,
                L(new PgIf(ColumnsPresent(item.Create.Table, columns), L(new PgExecuteStatement(item.Create)), NodeList.Empty))));
            if (item.CollisionKeys is not null)
                statements.Add(CollisionCheck(item, columns));
        }
        return new Script(NodeList.Of(statements));
    }

    private static PgBlock Drift(ReconcileIndex item, IReadOnlyList<string> columns)
    {
        var create = item.Create;
        var current = Id(PostgresDdlDataLiterals.DriftCurrent);
        return new PgBlock(
            "drift",
            L(new PgDeclare(PostgresDdlDataLiterals.DriftCurrent, "oid")),
            L(new PgIf(
                ColumnsPresent(create.Table, columns),
                L(
                    IndexInto(create.Table, create.Index, PostgresDdlDataLiterals.DriftCurrent),
                    NameTaken(create.Table, create.Index, current),
                    Probe(create, current)),
                NodeList.Empty)));
    }

    private static PgQuery IndexInto(string table, string index, string variable) => new(
        L(Id("ic.oid")),
        new PgRelation(PostgresDdlDataLiterals.IndexOfTable),
        new Where(L(Eq(Id("i.indrelid"), Regclass(table)), Eq(Id("ic.relname"), S(index)))),
        Into: variable);

    private static PgIf NameTaken(string table, string index, PgName current)
    {
        var schema = new PgScalar(new PgQuery(
            L(Id("t.relnamespace")),
            new PgRelation("pg_class", "t"),
            new Where(L(Eq(Id("t.oid"), Regclass(table))))));
        var held = new PgExists(false, new PgQuery(
            L(new IntLit(1)),
            new PgRelation("pg_class", "x"),
            new Where(L(Eq(Id("x.relname"), S(index)), Eq(Id("x.relnamespace"), schema)))));
        var refuse = new PgRaise(S(PostgresDdlDataLiterals.IndexNameTakenMessage), L(S(index), S(table)));
        return new PgIf(new PgAll(L(new IsNull(current), held)), L(refuse), NodeList.Empty);
    }

    private static PgIf Probe(CreateIndex create, PgName current)
    {
        var probeIndex = Call(
            "to_regclass",
            S(PostgresDdlDataLiterals.TempSchema + "." + PostgresNames.Ident(create.Index).Text));
        var differs = new Compare(CompareOp.NotEqual, ExactDefinition(create.Table, current), DefinitionOf(probeIndex));
        var dropProbe = new PgDataLiteral(PostgresDdlDataLiterals.DriftDropProbe);
        var compare = new PgIf(
            differs,
            L(dropProbe, new PgDataLiteral(PostgresDdlDataLiterals.DriftDropCurrent)),
            L(dropProbe));
        var table = PostgresNames.Splice(
            PostgresDdlDataLiterals.DriftProbeTable,
            PostgresNames.Ident(PostgresDdlDataLiterals.DriftProbeName),
            PostgresNames.Ident(create.Table)).Text;
        return new PgIf(
            new IsNotNull(current),
            L(
                new PgDataLiteral(table),
                new PgExecuteStatement(create with { Table = PostgresDdlDataLiterals.DriftProbeName }),
                compare),
            NodeList.Empty);
    }

    private static PgCall ExactDefinition(string table, Node index)
    {
        var qualified = new PgScalar(new PgQuery(
            L(Call("concat", Call("quote_ident", Id("n.nspname")), S("."), Call("quote_ident", Id("c.relname")))),
            new PgRelation(PostgresDdlDataLiterals.ClassWithNamespace),
            new Where(L(Eq(Id("c.oid"), Regclass(table))))));
        var onTable = Call(
            "concat",
            S(PostgresDdlDataLiterals.DriftOn),
            qualified,
            S(PostgresDdlDataLiterals.DriftTableReplacement));
        return Call(
            "replace",
            Call("pg_get_indexdef", index),
            onTable,
            S(PostgresDdlDataLiterals.DriftTableReplacement));
    }

    private static PgCall DefinitionOf(Node index) =>
        Call("regexp_replace", Call("pg_get_indexdef", index), S(PostgresDdlDataLiterals.DriftTablePattern), S(PostgresDdlDataLiterals.DriftTableReplacement));

    private static PgAll ColumnsPresent(string table, IReadOnlyList<string> columns)
    {
        var catalog = new PgQuery(
            L(Call("count", new Star())),
            new PgRelation("pg_attribute"),
            new Where(L(
                Eq(Id("attrelid"), Regclass(table)),
                new Compare(CompareOp.Greater, Id("attnum"), new IntLit(0)),
                new PgNegate(Id("attisdropped")),
                new PgAnyArray(Id("attname"), NodeList.Of(columns.Select(c => (Node)S(c)))))));
        return new PgAll(L(new IsNotNull(Regclass(table)), Eq(new PgScalar(catalog), new IntLit(columns.Count))));
    }

    private static PgBlock CollisionCheck(ReconcileIndex item, IReadOnlyList<string> guarded)
    {
        var keys = item.CollisionKeys!.Select(ColumnOf).ToList();
        var loop = PostgresNames.Splice(
            PostgresDdlDataLiterals.CollisionLoopTemplate,
            PostgresNames.Text(PostgresNames.Ident(item.Create.Table).Text),
            PostgresNames.Count(keys.Count),
            PostgresNames.List(keys.Select(PostgresNames.Text)),
            PostgresNames.Text(item.Create.Index),
            PostgresNames.Text(item.Create.Table),
            PostgresNames.Text(string.Join(PostgresDdlDataLiterals.ListSeparator, keys))).Text;
        return new PgBlock(
            "collision",
            L(new PgDeclare("r", "record")),
            L(new PgIf(ColumnsPresent(item.Create.Table, guarded), L(new PgDataLiteral(loop)), NodeList.Empty)));
    }

    private static Node Install(RowHistoryInstall n)
    {
        var statements = new List<Node>
        {
            new PgSetLocal(PostgresDdlDataLiterals.LockTimeoutSetting, S(PostgresDdlDataLiterals.LockTimeoutValue)),
            new PgDataLiteral(PostgresDdlDataLiterals.RowHistoryTableDdl),
            GuardedIndex("ix_row_history_source_row", SourceRowColumns),
            GuardedIndex("ix_row_history_tenant_xact", TenantXactColumns),
            GuardedIndex("ix_row_history_changed_at", ChangedAtColumns),
            new PgDataLiteral(CaptureFunction(n.ActorSetting)),
        };
        statements.AddRange(n.Tables.Cast<HistoryTableNode>().Select(Attach));
        return new Script(NodeList.Of(statements));
    }
}
