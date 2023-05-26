# MusicDecrypto

[![Build](https://github.com/davidxuang/MusicDecrypto/actions/workflows/build.yaml/badge.svg)](https://github.com/davidxuang/MusicDecrypto/actions/workflows/build.yaml)
[![NuGet](https://badgen.net/nuget/v/MusicDecrypto.Library)](https://www.nuget.org/packages/MusicDecrypto.Library/)
[![GitHub release](https://img.shields.io/github/release/davidxuang/musicdecrypto.svg)](https://GitHub.com/davidxuang/musicdecrypto/releases/)
[![GitHub license](https://img.shields.io/github/license/davidxuang/musicdecrypto.svg)](https://github.com/davidxuang/musicdecrypto/blob/master/LICENSE)

This project implements music de-obfuscation on [.NET](https://dotnet.microsoft.com/), and accelerates the process with [SIMD](https://docs.microsoft.com/en-us/dotnet/standard/simd). The CLI program also uses [Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) feature.

## Licensing

The core library is distributed under [GNU LGPL v3](./MusicDecrypto.Library/LICENSE), while the CLI and GUI applications are both distributed under [GNU AGPL v3](./LICENSE).

## Supported formats

You may refer to [the full list](./MusicDecrypto.Library/DecryptoFactory.cs#L23). Please notice that support for `.mgg` and `.mflac` series are partial.

## CLI usage

Drag and drop files and/or directories on the CLI program or run:

`musicdecrypto [options] [<input>...]`

### Options

```
-f, --force-overwrite    Overwrite existing files.
-r, --recursive          Search files recursively.
-x, --extensive          Extend range of extensions to be detected.
-o, --output <output>    Output directory.
```

## References

- [ncmdump](https://github.com/anonymous5l/ncmdump)
- [unlock-music](https://git.unlock-music.dev/um/web)
- [unlock-mflac](https://github.com/zeroclear/unlock-mflac-20220931)
- [parakeet-rs](https://github.com/parakeet-rs/parakeet-crypto-rs)
