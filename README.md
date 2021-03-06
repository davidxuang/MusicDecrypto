# MusicDecrypto

[![GitHub release](https://img.shields.io/github/release/davidxuang/musicdecrypto.svg)](https://GitHub.com/davidxuang/musicdecrypto/releases/)
[![Build status](https://ci.appveyor.com/api/projects/status/github/davidxuang/musicdecrypto?svg=true)](https://ci.appveyor.com/project/davidxuang/musicdecrypto)
[![GitHub license](https://img.shields.io/github/license/davidxuang/musicdecrypto.svg)](https://github.com/davidxuang/musicdecrypto/blob/master/LICENSE)

This project aims to implement music de-DRM for NetEase Cloud Music and QQ Music on .NET Core, and generate native executable through experimental [Native AOT](https://github.com/dotnet/runtimelab/tree/feature/NativeAOT) feature.

## Supported formats

<table><tbody align="center">
<tr>
<th>Format</th>
<th>Vorbis</th>
<th>MP3</th>
<th>AAC</th>
<th>PCM</th>
<th>FLAC</th>
</tr>
<tr>
<td>NetEase</td>
<td></td>
<td>✔️<br/><sub>.ncm</sub></td>
<td></td>
<td></td>
<td>✔️<br/><sub>.ncm</sub></td>
</tr>
<tr>
<td>Moo</td>
<td></td>
<td>✔️<br/><sub>.bkcmp3</sub></td>
<td></td>
<td></td>
<td>✔️<br/><sub>.bkcflac</sub></td>
</tr>
<tr>
<td rowspan="3">QQ<sup>*</sup></td>
<td></td>
<td></td>
<td>✔️<br/><sub>.tm2/tm6</sub></td>
<td></td>
<td></td>
</tr>
<tr>
<td>✔️<br/><sub>.qmcogg</sub></td>
<td>✔️<br/><sub>.qmc0/qmc3</sub></td>
<td>✔️<br/><sub>.tkm</sub></td>
<td></td>
<td>✔️<br/><sub>.qmcflac</sub></td>
</tr>
<tr>
<td>❌<br/><sub>.mgg</sub></td>
<td></td>
<td></td>
<td></td>
<td>⭕<br/><sub>.mflac</sub></td>
</tr>
<tr>
<td>Xiami</td>
<td></td>
<td>✔️<br/><sub>.xm**</sub></td>
<td>✔️<br/><sub>.xm**</sub></td>
<td>✔️<br/><sub>.xm**</sub></td>
<td>✔️<br/><sub>.xm**</sub></td>
</tr>
<tr>
<td colspan="6" align="left">
<sup>*</sup> .tm0/tm3 are just custom MP3 extensions.<br/>
<sup>**</sup> use <code>-x</code> to include files with a “normal” extension.</td>
</tr>
</tbody></table>

## Build

`dotnet build -c Release`

### Dependencies

-   [.NET](https://dotnet.microsoft.com) 5
-   [Json.NET](https://www.newtonsoft.com/json)
-   [Mono.Options](https://github.com/xamarin/XamarinComponents/tree/master/XPlat/Mono.Options)
-   [TagLib#](https://github.com/mono/taglib-sharp)

## Run

Drag and drop files and/or directories into the executable or run:

`MusicDecrypto [options] [<input>...]`

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
