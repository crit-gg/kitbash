# Script tools

A tool that runs to the end rather than opening a window, reports on itself while it does,
and asks for what it needs first.

This sits inside `.claude/plans/tool-distribution.md`, which is where a tool comes from and
how it is installed. Nothing here changes any of that. A script tool is installed, updated
and removed exactly like any other, and the only difference is what starting it means.

## Status

**Built.** A manifest can say it is a script, declare what to ask for, and the launcher
draws the form, runs the script, and reports what it writes over a modal.

- `Kitbash/Tools` grew `ToolKind`, `ToolInput`, `ToolInputKind`, `ToolInputChoice`,
  `ToolProgressStep`, `ToolProgressReader`, `ToolRunOutcome`, `IToolScriptRunner` with
  `ToolScriptRunner`, and `IToolInputMemory` with `ToolInputMemory`.
- `ToolManifest` grew `Kind` and `Inputs`, and the reader reads up to format 2.
- `Kitbash/Views` grew `ToolInputsDialog` and `ToolRunDialog`, and
  `Kitbash/ViewModels` grew `ToolInputsViewModel` and `ToolInputRowViewModel`.
- The card's lead button says Run rather than Launch for one.
- **A script has its own section on both sides, and a card half the height.** Running one
  and opening an app are different things to want, so they are separate sections rather
  than one list. The four are INSTALLED TOOLS, INSTALLED SCRIPTS, AVAILABLE TOOLS and
  AVAILABLE SCRIPTS, and like every section each is drawn only when it holds something. A
  script that is only offered is drawn small too, so no section ever mixes card sizes.
- The small card drops the promoted actions and keeps the name, the version, two lines of
  description and the buttons, against the full card's four lines. Both are three across,
  so a script row and a tool row line up down the page. `ToolCardTemplates` is what picks
  between the two, off `IsCompact` on the card, so the group a card lands in decides how
  it is drawn.
- Its description has a two line floor for the same reason the full card's has a four line
  one. Without it a one line description leaves that card's buttons above its neighbour's.
- **A card name and a small card description are trimmed, so both carry `ui:TextTip.Shows`**,
  which puts the whole text in a tooltip while it does not fit and takes the tooltip away
  again while it does.
- Checking for updates moved off the installed group and onto whichever heading is first,
  since two installed sections would otherwise each draw their own Check for updates.

## Settled

- **A script is a whole tool, not an action on one.** The manifest says `kind`, and that
  decides what the lead button does. A tool that wants to be both is two tools.
- **Both report forms are read.** A prefixed plain line and a JSON object per line. A shell
  script should not have to build JSON, and a program that already speaks JSON should not
  have to build a plain line.
- **Anything that is neither is a log line.** Output is never dropped.
- **Standard output is the report and standard error is never one.** A script cannot report
  progress down the error pipe, so a program printing warnings there cannot move the bar by
  accident.
- **The answers are the arguments.** The form has no other effect. Nothing is written to a
  file, and the script is told nothing it was not handed on its command line.
- **Remembering is per input.** The manifest says which answers come back next time.
- **Cancelling kills the script and everything it started.** There is no polite stop.

## What a tool author writes

### The manifest

`kind` and `inputs` both need `"manifest": 2`. A manifest carrying either at format 1 is
refused, because a launcher reading only format 1 would ignore both and run the script as
though it were a window that never opens.

```json
{
  "manifest": 2,
  "id": "sprites",
  "name": "Sprite import",
  "summary": "Slices sheets and writes the atlas Godot imports.",
  "category": "Content",
  "version": "1.0.0",
  "kind": "script",
  "inputs": [
    {
      "key": "source",
      "name": "Source folder",
      "description": "Where the sheets are.",
      "type": "folder",
      "argument": "--source",
      "required": true,
      "remember": true,
      "default": "{workspace}/art"
    },
    { "key": "scale", "type": "number", "argument": "--scale",
      "default": 1, "minimum": 0.1, "maximum": 8 },
    { "key": "dry", "name": "Dry run", "type": "boolean", "argument": "--dry-run" },
    { "key": "mode", "type": "choice", "argument": "--mode=",
      "choices": [ { "value": "fast", "label": "Fast" },
                   { "value": "careful", "label": "Careful" } ] }
  ],
  "payloads": [
    { "runtime": "any", "asset": "sprites-1.0.0.tar.gz", "size": 4096,
      "sha256": "...", "executable": "run.sh" }
  ]
}
```

`kind` is `app` or `script`, and absent means `app`.

### What an input may say

| Field | What it is |
|---|---|
| `key` | names the value in the form and in what is remembered. Letters, digits, dashes and underscores |
| `name` | the label. The key when absent |
| `description` | the line under the editor. Optional |
| `type` | `text`, `number`, `integer`, `boolean`, `choice`, `file` or `folder`. `text` when absent |
| `argument` | the flag the value goes behind. Absent means a plain argument |
| `required` | the form cannot be run while it is empty |
| `remember` | the answer comes back next time, for this person on this machine |
| `default` | what the field opens with. Text, a number, or true or false |
| `choices` | the options of a `choice`, each a `value` and an optional `label` |
| `minimum`, `maximum` | the range of a `number` or an `integer` |

**`{workspace}` in a default is the open workspace**, the same token a custom Open in tool
uses. With no workspace open it becomes nothing. It is the only token, and it is not
expanded anywhere but in a default.

### What the answers become

Arguments are built in the order the manifest declares the inputs, after the workspace.

```
<executable> --workspace <path> <every answer, in order>
```

| The input | The command line |
|---|---|
| a value behind `--source` | `--source` `/art`, two arguments |
| a value behind `--mode=` | `--mode=fast`, one argument |
| a value with no `argument` | `/art`, on its own |
| an empty optional value | nothing at all |
| a ticked box behind `--dry-run` | `--dry-run` |
| an unticked box behind `--dry-run` | nothing at all |
| a box with no `argument` | `true` or `false` |

**A flag ending in `=` is joined to its value.** That is the one accommodation made for a
script parsing its own arguments by hand, and it is why a flag holding a space is refused
when the manifest is read: it would arrive as two arguments and nobody wrote it meaning
that.

**Every answer is one argument whatever it holds.** Arguments are passed as a list, so a
path with a space in it needs no quoting anywhere and no quoting is honoured.

### Reporting

Write to standard output. Both of these say the same thing:

```sh
echo "@kitbash stage Reading files"
echo "@kitbash progress 40"
echo "@kitbash detail 12 / 30"
```

```python
print('{"kitbash":1,"stage":"Reading files","progress":40,"detail":"12 / 30"}')
```

| Verb | JSON field | What it does |
|---|---|---|
| `stage` | `stage` | the sentence a person reads |
| `detail` | `detail` | the mono line under it, such as a count |
| `progress` | `progress` | moves the bar. 0 to 100, and a trailing `%` is allowed |
| `log` | `log` | a line for the log under Details |
| `error` | `error` | the same, marked as the script complaining |

**`@kitbash progress` with no number puts the spinner back**, for a script that has stopped
knowing how far through it is. In JSON that is `"progress": null`.

`@kitbash:` is accepted for the prefix as well as `@kitbash `, and the verb is not case
sensitive. **The prefix has to end the word**, so a line about `@kitbashing` is a log line.

**A JSON line must carry a `kitbash` property.** An object without one is a script printing
JSON for its own reasons and stays a log line. The value is not read, so `1` is as good as
anything.

**Anything else is a log line**, including an unknown verb, which keeps its whole line.

**The exit code is the answer.** Anything but zero keeps the modal up, says which code it
was, and opens the log. Zero raises a toast, and keeps the modal up with its log open when
the script wrote anything at all, so what it said can be read. A script that wrote nothing
has nothing to read, so its modal closes.

## What the launcher does

1. The card's lead button says Run. Pressing it asks whether the manifest declares inputs.
2. With inputs, `ToolInputsDialog` opens over the launcher, filled from what was remembered
   and from the defaults behind that. Cancelling stops here and runs nothing.
3. What was accepted is remembered **before** the run rather than after, so a script that
   fails still finds its form as it was left.
4. `ToolRunDialog` opens and `ToolScriptRunner` starts the script in its own version
   folder, reading its output a line at a time.
5. Every line goes through `ToolProgressReader` and arrives at the dialog on the UI thread.
6. Standard error is read at the end and each line joins the log, marked as an error.
7. A run that wrote a line keeps the dialog up with the log open, whichever way it went.
   Only a run that worked and said nothing closes it. The toast for one that worked is
   raised once the dialog has gone.

**The log keeps the last 500 lines and is rebuilt when the thread is next idle**, never per
line. A script can write faster than a screen can follow, and the text is only built at all
while Details is open.

**A line is never folded onto the next one.** A script writes lines and a wrapped one reads
as two, so a long line runs off the side and the log scrolls both ways. Following the newest
line moves down alone, since taking somebody back to the left edge every time the script
writes would make reading sideways impossible. Measured: with the offset put at 200 across,
a new line leaves it there and still lands on the last line.

**Copy details is in the footer, hard left away from the decision**, the same shape
`ui:ErrorDialog` and `LaunchFailedDialog` use, and it says Copied for 1.4 seconds after a
press. It appears with the first line and stays whether or not Details is open, so the whole
log is one press away at any point in a run. What lands is what the log holds, so a script
that wrote past 500 lines is short of its oldest ones there too.

**Nothing about a script tool reaches `launcher.after.tool`.** Getting out of the way
follows a program that outlives the launcher, and a script does not, so the setting is not
consulted and the launcher stays where it is.

## Where the decisions differ from the app path

**A script is not detached.** `IToolStarter.StartDetached` is what an app tool goes through,
and the whole point of that is a program that outlives the launcher. A script is watched, so
it goes through `IProcessRunner.ReadLinesAsync` instead, which is the only way to read its
pipes at all.

**Output is decoded as UTF 8 rather than as the console's encoding.** A script writes what
its own language writes, which is UTF 8 on both platforms, and the default on Windows is the
OEM code page. Without this a stage line holding anything but ASCII would arrive wrong there.

**Remembered answers live in state, not in settings.** `<state>/tools/<id>/state.toml` under
`[inputs]`, beside `install.version`. They are the app's record of what somebody typed
rather than anything they chose to configure, and the settings window has no business
drawing them.

## What is not built

- **No secret input.** A password or a token typed into a form would reach the script as an
  argument, and an argument is readable by anything on the machine that can list processes.
  A tool wanting one should read it itself.
- **No progress from standard error**, deliberately. See Settled.
- **No cancel a script can hear.** Cancelling kills the process tree. A script that needs to
  clean up after itself cannot, and nothing tells it to.
- **No per input visibility rules.** Every input is always drawn. There is no way to say one
  field only matters when another is ticked.
- **No output a tool hands back.** A script says how it went with its exit code and nothing
  else. Anything it produced is on disk.
- **No progress on the card.** A run is a modal, so the tools page is not usable while one
  runs.

## What was measured

On this machine, Linux, 118 checks in `tests/Kitbash.Tests`.

**Real scripts, started by the real runner over the real process runner**: both report
forms arriving in order, a plain line becoming a log line, standard error arriving marked,
the exit code coming back, the workspace and the form's answers arriving as the script's
own arguments in order, an answer holding a space staying one argument, the script running
in its own folder, cancelling killing it, and a script that is not there being reported
rather than failing quietly.

**Both windows, drawn headlessly against the real theme**: one editor per input kind, Run
disabled while a required answer is missing and enabled once it is typed, a folder input
being a path field whose value reaches the arguments, the modal drawing every part of a
step and leaving alone what a step does not mention, the spinner coming back, the log
appearing with its first line and filling in when it is opened, a long line scrolling
sideways rather than wrapping and staying where it was scrolled to as more arrive, the whole
log landing on the clipboard in one press, a failed run keeping
its window with Cancel gone, the code said and the log open, and a run that worked and
wrote something keeping its window with nothing marked wrong about it.

**Remembering, over a real state file**: only a marked input kept, a cleared answer
forgetting its key, the file landing in the tool's own folder, and a form with nothing worth
keeping writing no file at all.

**The manifest reader**: every field read, every default filled in, and each of the nine
refusals, including both fields being refused at format 1.

## What could not be tested here

- **The path through the launcher window has never been executed.** `LaunchToolAsync`
  branching on the kind, and `LauncherWindow` handing the two dialogs over, are read rather
  than run. Both dialogs and everything under them are covered, and the branch itself is
  three lines.
- **Nothing about Windows.** This machine is Linux, so the UTF 8 decoding, a payload marked
  `any` holding a script that Windows cannot run, and killing a process tree there are all
  reasoning rather than something watched.
- **No script tool is published anywhere**, so nothing here has been through a real install
  from a real release.
