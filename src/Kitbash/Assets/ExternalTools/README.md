# External tool marks

The brand marks the Open in menu draws, one PNG per tool, 64 by 64 with an alpha channel.

## Where they came from

All of them were taken from SourceGit, which is MIT licensed, out of
`src/Resources/Images/ExternalToolIcons/` and `src/Resources/Images/ShellIcons/`. Two
names changed on the way in: `vs-preview.png` became `vs_preview.png` and the terminal
files lost their hyphens, so every name here matches the key the code asks for.

**Each mark belongs to whoever owns the product.** Microsoft, JetBrains, the VSCodium
project, Anysphere and the terminal projects each own theirs. They are here to name a
program a person already installed, which is nominative use, and nothing here claims any
of them.

## How a name is chosen

The key is the `IconKey` on `WorkspaceOpener` and it is a path under this folder without
the extension. A missing file draws no mark and still draws the row, so adding a tool
before its mark is safe.

```
vscode              an editor at the top level
jetbrains/RD        a JetBrains product code
terminal/konsole    a terminal
custom              a tool a person added
```

## Adding one

Drop the PNG in the right folder and name it after the key. The build glob is recursive,
so nothing in `Kitbash.csproj` changes. Keep it square and no smaller than 64 by 64, since
the menu draws at 16 and downscales.

**These are not the icon set.** The vector glyphs in `Kitbash.Ui` come from
`tools/icons/generate.py` and none of this goes near it. A brand mark is full colour and
belongs to somebody else, which is why it lives in the launcher and not in the design
library.
