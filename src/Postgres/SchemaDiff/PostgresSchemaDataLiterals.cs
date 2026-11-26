namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaDataLiterals
{
    internal const string TimestampStorage =
        "timestamp with time zone";

    internal const string Columns =
        "SELECT a.attname, pg_catalog.format_type(a.atttypid,a.atttypmod), NOT a.attnot"
        + "null,\n  COALESCE((SELECT k.ordinality::integer FROM pg_catalog.pg_constraint "
        + "c\n    CROSS JOIN LATERAL unnest(c.conkey) WITH ORDINALITY k(attnum,ordinality"
        + ")\n    WHERE c.conrelid=a.attrelid AND c.contype='p' AND k.attnum=a.attnum),0)"
        + ",\n  pg_catalog.pg_get_expr(d.adbin,d.adrelid), a.attgenerated<>'', a.attident"
        + "ity<>'',\n  CASE WHEN a.attcollation=t.typcollation THEN NULL ELSE cn.nspname|"
        + "|'.'||co.collname END\nFROM pg_catalog.pg_attribute a JOIN pg_catalog.pg_type "
        + "t ON t.oid=a.atttypid\nLEFT JOIN pg_catalog.pg_attrdef d ON d.adrelid=a.attrel"
        + "id AND d.adnum=a.attnum\nLEFT JOIN pg_catalog.pg_collation co ON co.oid=a.attc"
        + "ollation\nLEFT JOIN pg_catalog.pg_namespace cn ON cn.oid=co.collnamespace\nWHE"
        + "RE a.attrelid=CAST(@relation AS oid) AND a.attnum>0 AND NOT a.attisdropped ORD"
        + "ER BY a.attnum";

    internal const string HistoryRelationHead =
        "SELECT c.relkind::text,n.nspname,c.relrowsecurity,c.oid,c.relhasrules,";

    internal const string HistoryUserTriggers =
        "EXISTS(SELECT 1 FROM pg_catalog.pg_trigger t WHERE t.tgrelid=c.oid AND NOT t.t"
        + "gisinternal),";

    internal const string HistoryIncomingConstraints =
        "EXISTS(SELECT 1 FROM pg_catalog.pg_constraint x WHERE x.confrelid=c.oid ";

    internal const string HistoryConstraintTail =
        "OR (x.conrelid=c.oid AND x.contype NOT IN ('p','n'))) FROM pg_catalog.pg_class"
        + " c ";

    internal const string JoinNamespace =
        "JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace ";

    internal const string ResolveHistory =
        "WHERE c.oid=pg_catalog.to_regclass(pg_catalog.quote_ident('schema_migrations')"
        + ")";

    internal const string IdentifierQuote =
        "\"";

    internal const string EscapedIdentifierQuote =
        "\"\"";

    internal const string ReadHistoryPrefix =
        "SELECT version FROM \"";

    internal const string ReadHistorySuffix =
        "\".\"schema_migrations\"";

    internal const string ReadOnlyTransaction =
        "SET TRANSACTION READ ONLY";

    internal const string DatabaseIdentityHead =
        "SELECT current_database(),current_schema(),";

    internal const string DatabaseVersion =
        "current_setting('server_version_num'),";

    internal const string DatabaseOid =
        "(SELECT oid FROM pg_catalog.pg_database WHERE datname=current_database())";

    internal const string Inventory =
        "SELECT 'index',i.relname::text,pg_catalog.pg_get_indexdef(i.oid)\n  FROM pg_ca"
        + "talog.pg_index x JOIN pg_catalog.pg_class i ON i.oid=x.indexrelid\n  WHERE x.i"
        + "ndrelid=CAST(@relation AS oid)\nUNION ALL SELECT 'constraint:'||c.contype::tex"
        + "t,c.conname::text,pg_catalog.pg_get_constraintdef(c.oid,true)\n  FROM pg_catal"
        + "og.pg_constraint c WHERE c.conrelid=CAST(@relation AS oid)\nUNION ALL SELECT '"
        + "trigger',t.tgname::text,pg_catalog.pg_get_triggerdef(t.oid,true)\n  FROM pg_ca"
        + "talog.pg_trigger t WHERE t.tgrelid=CAST(@relation AS oid)\nUNION ALL SELECT CA"
        + "SE WHEN\n  (d.classid='pg_catalog.pg_type'::regclass AND EXISTS\n    (SELECT 1"
        + " FROM pg_catalog.pg_type t WHERE t.oid=d.objid AND t.typrelid=CAST(@relation A"
        + "S oid)))\n  OR (d.classid='pg_catalog.pg_constraint'::regclass AND EXISTS\n   "
        + " (SELECT 1 FROM pg_catalog.pg_constraint c WHERE c.oid=d.objid AND c.conrelid="
        + "CAST(@relation AS oid)))\n  OR (d.classid='pg_catalog.pg_class'::regclass AND "
        + "EXISTS\n    (SELECT 1 FROM pg_catalog.pg_index i WHERE i.indexrelid=d.objid AN"
        + "D i.indrelid=CAST(@relation AS oid)))\n  OR (d.classid='pg_catalog.pg_class'::"
        + "regclass AND EXISTS\n    (SELECT 1 FROM pg_catalog.pg_class t WHERE t.oid=CAST"
        + "(@relation AS oid) AND t.reltoastrelid=d.objid))\n  OR (d.classid='pg_catalog."
        + "pg_attrdef'::regclass AND EXISTS\n    (SELECT 1 FROM pg_catalog.pg_attrdef a W"
        + "HERE a.oid=d.objid AND a.adrelid=CAST(@relation AS oid)))\n  THEN 'dependent-l"
        + "ocal:'||d.deptype::text ELSE 'dependent-external:'||d.deptype::text END,\n  o."
        + "type||':'||o.identity,o.identity||':'||d.refobjsubid::text\n  FROM pg_catalog."
        + "pg_depend d CROSS JOIN LATERAL\n    pg_catalog.pg_identify_object(d.classid,d."
        + "objid,d.objsubid) o\n  WHERE d.refclassid='pg_catalog.pg_class'::regclass AND "
        + "d.refobjid=CAST(@relation AS oid)";

    internal const string IntrinsicRoles =
        "SELECT 'constraint:'||c.contype::text,c.conname::text,c.contype::text,a.attnam"
        + "e::text\n  FROM pg_catalog.pg_constraint c JOIN pg_catalog.pg_attribute a\n   "
        + " ON a.attrelid=c.conrelid AND a.attnum=c.conkey[1]\n  WHERE c.conrelid=CAST(@r"
        + "elation AS oid) AND c.contype IN ('p','n')\n    AND cardinality(c.conkey)=1 AN"
        + "D NOT c.condeferrable AND NOT c.condeferred\n    AND c.convalidated AND c.cone"
        + "nforced AND c.conparentid=0 AND c.coninhcount=0 AND c.conislocal\n    AND pg_c"
        + "atalog.pg_get_constraintdef(c.oid,true)=CASE c.contype\n      WHEN 'p' THEN 'P"
        + "RIMARY KEY ('||pg_catalog.quote_ident(a.attname)||')'\n      WHEN 'n' THEN 'NO"
        + "T NULL '||pg_catalog.quote_ident(a.attname) END\nUNION ALL SELECT 'index',i.re"
        + "lname::text,'index',a.attname::text\n  FROM pg_catalog.pg_index x JOIN pg_cata"
        + "log.pg_class i ON i.oid=x.indexrelid\n  JOIN pg_catalog.pg_am am ON am.oid=i.r"
        + "elam\n  JOIN pg_catalog.pg_opclass op ON op.oid=x.indclass[0]\n  JOIN pg_catal"
        + "og.pg_attribute a ON a.attrelid=x.indrelid AND a.attnum=x.indkey[0]\n  JOIN pg"
        + "_catalog.pg_constraint c ON c.conindid=i.oid AND c.contype='p'\n  WHERE x.indr"
        + "elid=CAST(@relation AS oid) AND x.indnatts=1 AND x.indnkeyatts=1\n    AND x.in"
        + "disprimary AND x.indisunique AND x.indisvalid AND x.indisready\n    AND x.indp"
        + "red IS NULL AND x.indexprs IS NULL AND i.reloptions IS NULL\n    AND am.amname"
        + "='btree' AND x.indoption[0]=0 AND x.indcollation[0]=a.attcollation\n    AND op"
        + ".opcdefault AND i.reltablespace=0 AND NOT x.indisclustered AND NOT x.indisrepl"
        + "ident\nUNION ALL SELECT 'dependent-local:'||d.deptype::text,o.type||':'||o.ide"
        + "ntity,\n  CASE WHEN t.typrelid=CAST(@relation AS oid) THEN 'row-type'\n    WHE"
        + "N own.reltoastrelid=d.objid AND d.classid='pg_catalog.pg_class'::regclass\n   "
        + "   THEN CASE WHEN toast.reloptions IS NULL AND toast.reltablespace=0\n        "
        + "THEN 'toast' ELSE 'toast-unsupported' END\n    WHEN ad.adrelid=CAST(@relation "
        + "AS oid) THEN 'default'\n    WHEN c.conrelid=CAST(@relation AS oid) THEN 'const"
        + "raint' ELSE '' END,\n  COALESCE(c.conname::text,a.attname::text,'')\n  FROM pg"
        + "_catalog.pg_depend d CROSS JOIN LATERAL\n    pg_catalog.pg_identify_object(d.c"
        + "lassid,d.objid,d.objsubid) o\n  LEFT JOIN pg_catalog.pg_type t ON d.classid='p"
        + "g_catalog.pg_type'::regclass AND t.oid=d.objid\n  LEFT JOIN pg_catalog.pg_clas"
        + "s own ON own.oid=CAST(@relation AS oid)\n  LEFT JOIN pg_catalog.pg_class toast"
        + " ON toast.oid=own.reltoastrelid\n  LEFT JOIN pg_catalog.pg_attrdef ad ON d.cla"
        + "ssid='pg_catalog.pg_attrdef'::regclass AND ad.oid=d.objid\n  LEFT JOIN pg_cata"
        + "log.pg_attribute a ON a.attrelid=ad.adrelid AND a.attnum=ad.adnum\n  LEFT JOIN"
        + " pg_catalog.pg_constraint c ON d.classid='pg_catalog.pg_constraint'::regclass "
        + "AND c.oid=d.objid\n  WHERE d.refclassid='pg_catalog.pg_class'::regclass AND d."
        + "refobjid=CAST(@relation AS oid)";

    internal const string RelationKind =
        "SELECT c.relkind::text FROM pg_catalog.pg_class c ";

    internal const string ResolveCurrentRelation =
        "WHERE n.nspname=current_schema() AND c.relname=@table";

    internal const string LockTablePrefix =
        "LOCK TABLE ";

    internal const string ExclusiveLockSuffix =
        " IN ACCESS EXCLUSIVE MODE";

    internal const string TableFacts =
        "SELECT c.oid,c.relkind::text,c.relpersistence::text,c.relispartition,c.relrows"
        + "ecurity,c.reloptions\nFROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace "
        + "n ON n.oid=c.relnamespace\nWHERE n.nspname=current_schema() AND c.relname=@tab"
        + "le";

    internal const string ResolveTable =
        "SELECT n.nspname FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON "
        + "n.oid=c.relnamespace\nWHERE c.oid=pg_catalog.to_regclass(pg_catalog.quote_iden"
        + "t(@table))";

    internal const string DoublePrecisionStorage =
        "double precision";

    internal const string NumericTypePrefix =
        "numeric(";

    internal const string ZeroDoublePrecision =
        "0::double precision";

    internal const string EmptyTextDefault =
        "''::text";

    internal const string CurrentTimeDefault =
        "now()";

}
