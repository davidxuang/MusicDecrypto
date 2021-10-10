# MusicDecrypto

[![Build](https://github.com/davidxuang/MusicDecrypto/actions/workflows/build.yaml/badge.svg)](https://github.com/davidxuang/MusicDecrypto/actions/workflows/build.yaml)
[![GitHub release](https://img.shields.io/github/release/davidxuang/musicdecrypto.svg)](https://GitHub.com/davidxuang/musicdecrypto/releases/)
[![GitHub license](https://img.shields.io/github/license/davidxuang/musicdecrypto.svg)](https://github.com/davidxuang/musicdecrypto/blob/master/LICENSE)

This project aims to implement music deobfuscation on .NET, and generate native binary through experimental [Native AOT](https://github.com/dotnet/runtimelab/tree/feature/NativeAOT) feature.

## Supported formats

<table>
<tbody>
<tr>
  <th align=left>✔️&nbsp;Supported</th>
  <td><code>.kgm</code> <code>.kgma</code> <code>.kwm</code> <code>.tm2</code> <code>.tm6</code> <code>.qmcogg</code> <code>.qmc0</code> <code>.qmc3</code> <code>.bkcmp3</code> <code>.tkm</code> <code>.qmcflac</code> <code>.ncm</code> <code>.xm</code></td>
</tr>
<tr>
  <th align=left>⭕&nbsp;Partially</th>
  <td><code>.vpr</code> <code>.mflac</code></td>
</tr>
<tr>
  <th align=left>❌&nbsp;Unsupported</th>
  <td><code>.mgg</code></td>
</tr>
</tbody>
</table>

## Build

`dotnet build MusicDecrypto.Commandline/MusicDecrypto.Commandline.csproj -c Release`

### Dependencies

-   [.NET](https://dotnet.microsoft.com) 5
-   [Mono.Options](https://github.com/xamarin/XamarinComponents/tree/master/XPlat/Mono.Options)
-   [TagLib#](https://github.com/mono/taglib-sharp)

## Run

Drag and drop files and/or directories into the executable or run:

`musicdecrypto [options] [<input>...]`

### Options

```
-f, --force-overwrite    Overwrite existing files.
-n, --renew-name         Renew Hash-like names basing on metadata.
-r, --recursive          Search files recursively.
-x, --extensive          Extend range of extensions to be detected.
-o, --output <output>    Output directory.
```

## References

-   [ncmdump](https://github.com/anonymous5l/ncmdump)
-   [unlock-music](https://github.com/ix64/unlock-music)
