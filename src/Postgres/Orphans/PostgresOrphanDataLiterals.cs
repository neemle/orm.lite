namespace Neemle.OrmLite.Postgres;

internal static class PostgresOrphanDataLiterals
{
    internal const string CatalogAcquire = "SELECT pg_catalog.current_schemas(true)";

    internal const string CatalogResolve =
        "SELECT c.oid::bigint,pg_catalog.format('%I.%I',n.nspname,c.relname), "
        + "c.relkind='r' AND c.relpersistence='p' AND NOT c.relrowsecurity AND NOT c.relhasrules "
        + "AND NOT EXISTS(SELECT 1 FROM pg_catalog.pg_inherits i "
        + "WHERE i.inhrelid=c.oid OR i.inhparent=c.oid) "
        + "AND pg_catalog.current_setting('session_replication_role')='origin' "
        + "FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace "
        + "WHERE c.oid=pg_catalog.to_regclass(@owner)";

    internal const string CatalogLock = "LOCK TABLE ONLY ";

    internal const string CatalogLockMode = " IN SHARE UPDATE EXCLUSIVE MODE";

    internal const string CatalogColumns =
        "SELECT a.attname,pg_catalog.format_type(a.atttypid,a.atttypmod),NOT a.attnotnull, "
        + "EXISTS(SELECT 1 FROM pg_catalog.pg_constraint k WHERE k.conrelid=a.attrelid "
        + "AND k.contype='p' AND k.conkey=ARRAY[a.attnum]), "
        + "a.attidentity<>'' OR a.attgenerated<>'',a.atthasdef,d.adbin::pg_catalog.text "
        + "FROM pg_catalog.pg_attribute a LEFT JOIN pg_catalog.pg_attrdef d "
        + "ON d.adrelid=a.attrelid AND d.adnum=a.attnum "
        + "WHERE a.attrelid=@owner::pg_catalog.oid AND a.attnum>0 "
        + "AND NOT a.attisdropped ORDER BY a.attnum LIMIT 257";

    internal const string CatalogOwner =
        "SELECT c.confrelid::bigint,p.attname FROM pg_catalog.pg_constraint c "
        + "JOIN pg_catalog.pg_attribute a ON a.attrelid=c.conrelid AND c.conkey=ARRAY[a.attnum] "
        + "JOIN pg_catalog.pg_attribute p ON p.attrelid=c.confrelid AND c.confkey=ARRAY[p.attnum] "
        + "WHERE c.conrelid=@owner::pg_catalog.oid AND c.contype='f' AND c.convalidated "
        + "AND a.attname=@column LIMIT 2";

    internal const string CatalogIncoming =
        "SELECT confdeltype::pg_catalog.text,confupdtype::pg_catalog.text "
        + "FROM pg_catalog.pg_constraint WHERE confrelid=@owner::pg_catalog.oid AND contype='f' LIMIT 257";

    internal const string CatalogPlainEffects =
        "SELECT 1 FROM pg_catalog.pg_constraint "
        + "WHERE conrelid=@owner::pg_catalog.oid AND contype='c' LIMIT 1";

    internal const string CatalogIndexEffects =
        "SELECT i.indexprs IS NOT NULL,i.indpred::pg_catalog.text,EXISTS(SELECT 1 FROM "
        + "pg_catalog.unnest(i.indclass::pg_catalog.oid[]) classes(opclass) "
        + "JOIN pg_catalog.pg_opclass o ON o.oid=classes.opclass "
        + "JOIN pg_catalog.pg_namespace n ON n.oid=o.opcnamespace "
        + "WHERE n.nspname<>'pg_catalog'),a.attnum,a.atttypid::pg_catalog.int8,a.atttypmod, "
        + "a.attcollation::pg_catalog.int8 FROM pg_catalog.pg_index i "
        + "LEFT JOIN pg_catalog.pg_attribute a ON a.attrelid=i.indrelid AND a.attname=@soft "
        + "AND NOT a.attisdropped WHERE i.indrelid=@owner::pg_catalog.oid "
        + "ORDER BY i.indexrelid LIMIT 257";

    internal const string HistoryTriggers =
        "SELECT t.tgname,CASE WHEN t.tgenabled='O' AND NOT t.tgdeferrable "
        + "AND NOT t.tginitdeferred AND t.tgconstraint=0 AND pg_catalog.cardinality(t.tgattr::smallint[])=0 "
        + "THEN t.tgtype::integer ELSE -1 END,t.tgargs,pg_catalog.pg_get_triggerdef(t.oid),t.tgfoid::bigint "
        + "FROM pg_catalog.pg_trigger t WHERE t.tgrelid=@owner::pg_catalog.oid AND NOT t.tgisinternal LIMIT 257";

    internal const string HistoryInternalTriggers =
        "SELECT 1 FROM pg_catalog.pg_trigger t JOIN pg_catalog.pg_proc p ON p.oid=t.tgfoid "
        + "JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace "
        + "LEFT JOIN pg_catalog.pg_constraint c ON c.oid=t.tgconstraint "
        + "WHERE t.tgrelid=@owner::pg_catalog.oid AND t.tgisinternal AND NOT "
        + "(t.tgenabled='O' AND c.contype='f' AND c.convalidated "
        + "AND n.nspname='pg_catalog' AND pg_catalog.starts_with(p.proname,'RI_FKey_')) LIMIT 1";

    internal const string HistoryFreezeFunctions =
        "'pg_catalog.pg_proc','pg_catalog.pg_language','pg_catalog.pg_namespace',"
        + "'pg_catalog.pg_class','pg_catalog.pg_type','pg_catalog.pg_operator','pg_catalog.pg_cast'";

    internal const string HistoryFreezeFunctions2 =
        "SELECT pg_catalog.bool_and(pg_catalog.has_table_privilege(c,'UPDATE,DELETE,TRUNCATE')) "
        + "AND (pg_catalog.current_setting('is_superuser')='on' "
        + "OR pg_catalog.pg_has_role(current_user,'pg_read_all_stats','USAGE')) "
        + "FROM pg_catalog.unnest(ARRAY[";

    internal const string HistoryFreezeFunctions3 = "]) c";

    internal const string HistoryFreezeFunctions4 =
        "LOCK TABLE pg_catalog.pg_proc,pg_catalog.pg_language,pg_catalog.pg_namespace,"
        + "pg_catalog.pg_class,pg_catalog.pg_type,pg_catalog.pg_operator,pg_catalog.pg_cast IN SHARE MODE";

    internal const string HistoryTriggerSeparator = ", ";

    internal const string HistoryTriggerAfterUpdate = " AFTER UPDATE ON ";

    internal const string HistoryTriggerUpdateTail =
        " FOR EACH ROW WHEN ((old.* IS DISTINCT FROM new.*)) EXECUTE FUNCTION ";

    internal const string HistoryTriggerAfterDelete = " AFTER DELETE ON ";

    internal const string HistoryTriggerDeleteTail = " FOR EACH ROW EXECUTE FUNCTION ";

    internal const string HistoryCreateTrigger = "CREATE TRIGGER ";

    internal const string HistoryOpenParen = "(";

    internal const string HistoryCloseParen = ")";

    internal const string HistoryFunction =
        "SELECT pg_catalog.format('%I.%I',n.nspname,p.proname),p.prosrc "
        + "FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace "
        + "JOIN pg_catalog.pg_language l ON l.oid=p.prolang WHERE p.oid=@owner::pg_catalog.oid "
        + "AND p.proname='row_history_capture' AND p.prorettype='pg_catalog.trigger'::pg_catalog.regtype "
        + "AND p.pronargs=0 AND NOT p.prosecdef AND p.proconfig IS NULL AND l.lanname='plpgsql' "
        + "FOR SHARE OF p";

    internal const string HistoryRefuseShadowFunctions =
        "SELECT 1 WHERE (pg_catalog.current_schemas(true))[1]<>'pg_catalog' OR EXISTS "
        + "(SELECT 1 FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace "
        + "WHERE n.nspname=ANY(pg_catalog.current_schemas(true)) AND n.nspname<>'pg_catalog' AND p.proname IN "
        + "('to_jsonb','jsonb_each','jsonb_object_agg','current_setting','pg_current_xact_id'))";

    internal const string HistoryTriggerDependencies =
        "SELECT 1 FROM pg_catalog.pg_depend d JOIN pg_catalog.pg_trigger t ON t.oid=d.objid "
        + "WHERE d.classid='pg_catalog.pg_trigger'::pg_catalog.regclass AND t.tgrelid=@owner::pg_catalog.oid "
        + "AND d.refclassid IN ('pg_catalog.pg_proc'::pg_catalog.regclass,"
        + "'pg_catalog.pg_operator'::pg_catalog.regclass) "
        + "AND d.refobjid>=16384 AND d.refobjid<>t.tgfoid LIMIT 1";

    internal const string HistoryRefuseInFlightCatalogWrites = "SELECT pg_catalog.pg_stat_clear_snapshot()";

    internal const string HistoryRefuseInFlightCatalogWrites2 =
        "SELECT 1 FROM pg_catalog.pg_stat_activity "
        + "WHERE pid<>pg_catalog.pg_backend_pid() AND backend_xid IS NOT NULL LIMIT 1";

    internal const string HistoryRefuseInFlightCatalogWrites3 =
        "SELECT 1 FROM pg_catalog.pg_prepared_xacts "
        + "WHERE database=pg_catalog.current_database() LIMIT 1";

    internal const string HistoryStoreVerify =
        "SELECT a.attname,pg_catalog.format_type(a.atttypid,a.atttypmod),a.attnotnull,"
        + "a.attidentity::pg_catalog.text||'|'||a.attgenerated::pg_catalog.text||'|'||"
        + "coalesce(pg_catalog.pg_get_expr(d.adbin,d.adrelid),'') "
        + "FROM pg_catalog.pg_attribute a LEFT JOIN pg_catalog.pg_attrdef d "
        + "ON d.adrelid=a.attrelid AND d.adnum=a.attnum WHERE a.attrelid=@owner::pg_catalog.oid "
        + "AND a.attnum>0 AND NOT a.attisdropped ORDER BY a.attnum LIMIT 12";

    internal const string HistoryStoreVerify2 =
        "SELECT 1 FROM pg_catalog.pg_trigger "
        + "WHERE tgrelid=@owner::pg_catalog.oid LIMIT 1";

    internal const string HistoryStoreChecks =
        "SELECT pg_catalog.pg_get_constraintdef(oid) FROM pg_catalog.pg_constraint "
        + "WHERE conrelid=@owner::pg_catalog.oid AND contype='c' ORDER BY conname LIMIT 4";

    internal const string HistoryStoreKey =
        "SELECT 1 FROM pg_catalog.pg_constraint c JOIN pg_catalog.pg_attribute a "
        + "ON a.attrelid=c.conrelid AND c.conkey=ARRAY[a.attnum] WHERE c.conrelid=@owner::pg_catalog.oid "
        + "AND c.contype='p' AND c.convalidated AND a.attname='id'";

    internal const string HistoryStoreTimestampType = "timestamp with time zone";

    internal const string HistoryStoreNowDefault = "||now()";

    internal const string HistoryStoreOpCheck =
        "CHECK ((op = ANY (ARRAY['update'::text, 'soft_delete'::text, 'restore'::text])))";

    internal const string HistoryStoreActorCheck =
        "CHECK ((actor_kind = ANY (ARRAY['user'::text, 'system'::text, 'unknown'::text])))";

    internal const string RowLockSuffix = " FOR UPDATE";

    internal const string EmptyTextDatum = "4 [ 16 0 0 0 ]";

    internal const string EmptyJsonDatum = "8 [ 32 0 0 0 0 0 0 32 ]";

    internal const string ZeroDatum = "{0} [ 0 0 0 0 0 0 0 0 ]";

    internal const string ConstNode =
        "{{CONST :consttype {0} :consttypmod -1 :constcollid {1} :constlen {2} :constbyval {3} "
        + ":constisnull false :location -1 :constvalue {4}}}";

    internal const string CoercionNode =
        "{{FUNCEXPR :funcid {0} :funcresulttype {1} :funcretset false :funcvariadic false "
        + ":funcformat 2 :funccollid 0 :inputcollid 0 :args ({2}) :location -1}}";

    internal const string LiveNullTest =
        "{{NULLTEST :arg {{VAR :varno 1 :varattno {0} :vartype {1} :vartypmod {2} "
        + ":varcollid {3} :varnullingrels (b) :varlevelsup 0 "
        + ":varreturningtype 0 :varnosyn 1 :varattnosyn {0} :location -1}} "
        + ":nulltesttype 0 :argisrow false :location -1}}";
}
