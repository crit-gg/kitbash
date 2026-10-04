# scoop-better-shimexe

`shim.exe` is the x86_64 build of release v3.2.2 of
<https://github.com/kiennq/scoop-better-shimexe>, taken unchanged from `shimexe-x86_64.zip`.

| | SHA256 |
|---|---|
| `shimexe-x86_64.zip` | `27ee2cffa9d6f5f72f39a4f60d23b6ac3390db843a3b2eefb52d64daa80a8440` |
| `shim.exe` | `caf80022ea9ea756a4bf18f37fd85615b65c8a5d45ec7f6cad1f91c844ada921` |

MIT or Unlicense, at the reader's choice. Both texts are beside it.

The launcher copies it to the bin folder as `godot.exe`, next to a `godot.shim` that names
the real engine. The release pack signs it with everything else in the package, so the
hash of the copy a person gets differs from the one above.

Read from `shim.cpp` at that tag, and relied on:

- It reads `<own name>.shim` beside itself, one `name = value` per line, UTF 8.
- `path` is the program. It quotes a path holding a space itself, and expands `%NAME%`.
- It passes its own arguments through untouched, ignores Ctrl C so the child gets it,
  waits, and returns the child's exit code.
- It kills the child when it is itself killed, through a job object.
