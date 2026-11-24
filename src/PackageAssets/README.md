# orm.lite brand assets

The split O represents an object and its relational mapping. The two complementary shapes form one compact mark.
The wordmark uses the short project name, `orm.lite`.

## Files

| File | Use |
| --- | --- |
| [logo.svg](logo.svg) | Horizontal logo with graphite lettering for light backgrounds. |
| [logo-dark.svg](logo-dark.svg) | The same logo with light lettering for dark backgrounds. |
| [mark.svg](mark.svg) | Standalone cobalt symbol with a transparent background. |
| [icon.svg](icon.svg) | White symbol on a cobalt rounded square, with an optical adjustment for small sizes. |
| [icon.png](icon.png) | 128 x 128 PNG exported from `icon.svg`, shared by all three NuGet packages. |

The SVGs are the editable originals. Every visible element, including the lettering, is a vector path or geometric
shape. They contain no embedded bitmap, external image, font dependency, script or filter. Their `viewBox` controls
the aspect ratio, so they can be scaled without changing the artwork. Keep both wordmark variants in sync when
editing their outlines.

## Colors

| Color | Value | Use |
| --- | --- | --- |
| Cobalt | `#5260E8` | Symbol, wordmark period and icon background. |
| Graphite | `#1B2430` | Lettering on light backgrounds. |
| Light | `#F4F6FA` | Lettering on dark backgrounds. |
| White | `#FFFFFF` | Symbol inside the square icon. |

Use the horizontal logo where the name needs to be read, and the square icon for compact package or avatar slots.
The icon has a slightly wider horizontal cut to keep its two parts distinct at small sizes. Preserve its internal
spacing and the aspect ratio of every asset.

## Regenerate the NuGet icon

NuGet's package icon format accepts PNG or JPEG, so `icon.png` is a generated compatibility asset. Edit `icon.svg`
first, then run this command from the repository root with Inkscape installed:

```sh
inkscape src/PackageAssets/icon.svg --export-width=128 --export-height=128 --export-filename=src/PackageAssets/icon.png
```

Commit the SVG and its refreshed PNG together. `src/Packaging.props` already includes `icon.png` in the core,
PostgreSQL and SQLite packages. No raster copy of the horizontal logo is needed: the repository README selects the
appropriate SVG for the reader's color scheme.
