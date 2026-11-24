# Third-party notices

Neemle.OrmLite is licensed under the MIT license (see `LICENSE`). The components below are used by the packages;
their licenses are those declared in their own NuGet packages at the versions this repository restores.

## Declared dependency of the Neemle.OrmLite package

- Neemle.CSharpComposer 0.5.0, declared as a NuGet dependency of Neemle.OrmLite and resolved by NuGet; not redistributed inside the package.
  License MIT, `Copyright (c) 2026 Neemle`. Project: https://github.com/neemle/cs-composer. The full license text it ships in its package is:

```
MIT License

Copyright (c) 2026 Neemle

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## NuGet dependencies declared by the provider packages

These are referenced, not copied into the Neemle.OrmLite packages; a consumer restores them from NuGet.

| Declared by | Package | Version | License (nuspec) | Copyright (nuspec) |
| --- | --- | --- | --- | --- |
| Neemle.OrmLite.Postgres | Npgsql | 10.0.3 | PostgreSQL | Copyright 2025 © The Npgsql Development Team |
| Neemle.OrmLite.Sqlite | Microsoft.Data.Sqlite | 10.0.13 | MIT | © Microsoft Corporation. All rights reserved. |

Microsoft.Data.Sqlite brings, transitively, the following packages:

| Package | Version | License (nuspec) | Copyright (nuspec) |
| --- | --- | --- | --- |
| Microsoft.Data.Sqlite.Core | 10.0.13 | MIT | © Microsoft Corporation. All rights reserved. |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.13 | Apache-2.0 | Copyright 2014-2024 SourceGear, LLC |
| SQLitePCLRaw.core | 2.1.13 | Apache-2.0 | Copyright 2014-2024 SourceGear, LLC |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.13 | Apache-2.0 | Copyright 2014-2024 SourceGear, LLC |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.13 | Apache-2.0 | Copyright 2014-2024 SourceGear, LLC |

Npgsql declares Microsoft.Extensions.Logging.Abstractions (MIT, Microsoft) as its own dependency.
