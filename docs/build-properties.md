# Build properties

[Documentation](README.md)

Set these MSBuild properties in a C# project’s `.csproj` file. For database connection settings, see
[Configuration](configuration.md).

| Property | Default | What it controls |
| --- | --- | --- |
| [`OrmLiteActorSetting`](#postgresql-actor-setting) | `ormlite.actor` | PostgreSQL setting used to identify the acting user. |
| [`OrmLiteDefaultTenant`](#default-tenant-property) | `TenantId` | Tenant property for soft-delete entities without an explicit tenant mapping. |
| [`OrmLiteProviders`](#provider-capability-checks) | Providers referenced by the project | Provider capabilities the analyzer checks at build time. |
| [`OrmLiteRegisterProviders`](#provider-registration) | Automatic in C# projects | Whether the build emits provider registration. |

## Example project settings

Add this property group inside the existing `<Project>` element. This example assumes the project references both
provider packages.

```xml
<PropertyGroup>
  <OrmLiteActorSetting>myapp.actor</OrmLiteActorSetting>
  <OrmLiteDefaultTenant>WorkspaceId</OrmLiteDefaultTenant>
  <OrmLiteProviders>postgres;sqlite</OrmLiteProviders>
</PropertyGroup>
```

## PostgreSQL actor setting

Use a lowercase `prefix.name`, such as `myapp.actor`. Each part must start with a letter and contain at most 31
letters, digits, or underscores. The generated initializer registers the setting when the assembly starts. An invalid value raises [`ORMLITE032`](generator-diagnostics.md#build-configuration).

The initializer and automatic provider registration are generated for C# projects. Setting
`OrmLiteActorSetting` in an F# or Visual Basic project raises [`ORMLITE033`](generator-diagnostics.md#build-configuration). Call
`OrmLiteSettings.UseActorSetting("myapp.actor")` during application startup in those languages.

## Default tenant property

Use a valid C# identifier for the mapped tenant property. The generator uses it when a soft-delete entity has no
explicit tenant mapping. A bad identifier raises [`ORMLITE014`](generator-diagnostics.md#build-configuration).

## Provider capability checks

Separate provider IDs with semicolons, such as `postgres;sqlite`. The build passes manifests from referenced
provider packages to the generator. The analyzer reports unsupported declarations and calls as
[`ORMLITE015` and `ORMLITE016`](generator-diagnostics.md#provider-capabilities).
See the [provider matrix](providers.md#capabilities-per-provider) for supported features.

## Provider registration

Set `OrmLiteRegisterProviders` to `false` to supply registration yourself. Call the provider factory’s
`Register()` before opening a connection.

For a runtime-only consumer, set `ExcludeAssets="analyzers"` on the package reference. The build assets and
actor setting remain available while the source generators are disabled.
