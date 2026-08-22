# Third party notices

`LICENSE` at the root is the MIT license and it covers the code in this repository.
It does not cover the content listed here, which arrived from somewhere else and
keeps whatever license its owner put on it. Nothing below is restated or summarised
into new terms. Each entry says what the content is, where it sits, and where its
real license lives.

## Box Icons Pro

The vector glyphs the app draws.

- `src/Kitbash.Ui/Themes/Icons.axaml`, the path data for each glyph
- `src/Kitbash.Ui/Controls/IconGlyph.cs`, the names
- `tools/icons/generate.py`, which reads the set and writes both

**The set is Box Icons Pro, Solid Rounded, which is bought rather than free.** The SVG
files are not in this repository and the build never reaches for them, as
`tools/icons/generate.py` says. What is committed is path data taken out of them, so
it is still the set's content and the MIT license above does not reach it.

The terms are Boxicons' own, at <https://boxicons.com/>, and whoever bought the set
holds them. Read them before publishing this repository or shipping a build, since a
paid icon set usually says something about redistributing the assets themselves.

The marks in `tools/icons/local/` are not from the set. That folder's README says
what each one is and where it came from.

## SourceGit

MIT. <https://github.com/sourcegit-scm/sourcegit>

Ported into `src/Kitbash.Core/Git`, `src/Kitbash.Core/Platform/Openers`,
the `TextDiff` controls in `src/Kitbash.Ui/Controls/` and the brand mark PNGs under
`src/Kitbash/Assets/ExternalTools/`. Its own license text follows, as MIT asks.

```
The MIT License (MIT)

Copyright (c) 2026 sourcegit

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

## Brand marks

`src/Kitbash/Assets/ExternalTools/` holds one PNG per external tool the Open in menu
can name. **Each mark belongs to whoever owns the product**, and no license here
covers any of them. They name a program a person already installed. That folder's
README has the whole of it.

## Fonts

Both are under the SIL Open Font License 1.1, and each license is committed beside
the font files it covers, which is what the OFL asks for.

- Archivo, `src/Kitbash.Ui/Assets/Fonts/Archivo-OFL.txt`
- JetBrains Mono, `src/Kitbash.Ui/Assets/Fonts/JetBrainsMono-OFL.txt`

## The Godot mark

`tools/icons/local/godot.svg` and `tools/icons/local/godot_colored.svg` are the Godot
engine's own mark, traced and normalised. It belongs to the Godot project and its
terms are at <https://godotengine.org/license/>.

## Packages

Every NuGet package this repository builds against is restored rather than committed,
so none of their files are here. Each carries its own license, which its package
records.
