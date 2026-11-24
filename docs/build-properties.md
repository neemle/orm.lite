# Build properties

[Documentation](README.md)

- `OrmLiteActorSetting`: the PostgreSQL custom setting that carries the acting user, `prefix.name` in lower case
  (default `ormlite.actor`); every assembly that references the runtime registers it at start-up. A value that breaks
  the rule fails the build (`ORMLITE032`). The initializer and the provider registration are written in C#, so a Visual
  Basic or F# project gets neither (a set `OrmLiteActorSetting` there gives the warning `ORMLITE033`)
- `OrmLiteDefaultTenant`: the tenant property a soft-delete entity uses when it does not declare one (default
  `TenantId`)
- `OrmLiteProviders`: the provider ids the project targets, separated by `;` (for example `postgres;sqlite`); it
  switches on the capability diagnostics `ORMLITE015` and `ORMLITE016`. It defaults to the provider packages the
  project references, whose capability manifests the build passes to the generator; set it to override
- `OrmLiteRegisterProviders`: `false` stops the build from emitting the provider registration (register the
  provider yourself with its factory's `Register()`)

`ExcludeAssets="analyzers"` on the package reference gives a runtime-only consumer: it still receives the build
assets and so the actor setting, and the generators do not run in it.
