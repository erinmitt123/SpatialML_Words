# Bundled SDK packages

These folders provide the PICO SDK, glTF exporter, and USD dependencies used by
the samples. Package files, Unity metadata, local fixes, native binaries, and
executable permissions are preserved. Separate SDK checkouts are not required.

| Package | Version | Folder |
| --- | --- | --- |
| `com.bytedance.pico.spatialadapter` | `6.2.0` | [com.bytedance.pico.spatialadapter](com.bytedance.pico.spatialadapter/package.json) |
| `com.bytedance.pico.xr` | `6.2.0` | [com.bytedance.pico.xr](com.bytedance.pico.xr/package.json) |
| `com.plattar.unitygltf` | `1.142.0` | [com.plattar.unitygltf](com.plattar.unitygltf/package.json) |
| `com.unity.formats.usd` | `3.0.0-exp.5` | [com.unity.formats.usd](com.unity.formats.usd/package.json) |

[Packages/manifest.json](../Packages/manifest.json) uses repository-relative
`file:../SDKPackages/<package>` references. Standard Unity dependencies continue
to resolve through Unity Package Manager's registry.

Spatial Adapter's bundle tools are unpacked into
`com.bytedance.pico.spatialadapter/Tools~/SpatialBundle`, which its exporter
supports directly. The oversized `SpatialBundle.zip` is omitted; macOS archive
metadata is excluded. Every individual SDK file is below GitHub's 100 MiB Git
file limit. Use Git to transfer these files; GitHub web uploads have a lower
per-file limit.

[packages.json](packages.json) records package versions, dependencies, file
counts, and unpacked sizes. [SHA256SUMS](SHA256SUMS) records the contents of the
package files. Verify from this directory with:

```sh
shasum -a 256 -c SHA256SUMS
```
