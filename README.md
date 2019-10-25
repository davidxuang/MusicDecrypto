# MusicDecrypto

[![GitHub release](https://img.shields.io/github/release/davidxuang/musicdecrypto.svg)](https://GitHub.com/davidxuang/musicdecrypto/releases/)
[![Build status](https://ci.appveyor.com/api/projects/status/github/davidxuang/musicdecrypto?svg=true)](https://ci.appveyor.com/project/davidxuang/musicdecrypto)
[![GitHub license](https://img.shields.io/github/license/davidxuang/musicdecrypto.svg)](https://github.com/davidxuang/musicdecrypto/blob/master/LICENSE)

A .NET Core implementation of music de-DRM for NetEase Cloud Music and QQ Music.

## Build

`dotnet build -c Release`

### Dependencies

-   [.NET Core](https://dotnet.microsoft.com) 3.0
-   [Json.NET](https://www.newtonsoft.com/json)
-   [TagLib#](https://github.com/mono/taglib-sharp)

## Run

Drag and drop files and/or directories into the executable or run:

`MusicDecrypto [-a|--avoid-overwrite] path...`

## References

-   [ncmdump](https://github.com/anonymous5l/ncmdump)
-   [qmcdump](https://github.com/MegrezZhu/qmcdump)
