---
name: kitbash-git
description: "Kitbash git integration. The shared runner, reading porcelain v2, history, diffs, staging by hunk, commits, branches, push and pull, conflict stages, and how one repository is watched with directory watches plus a beat. Read before changing anything that runs git."
---

## Git

`Kitbash.Core/Git` reads a repository by running git, and there is no library. Running
git works with whatever git the person has, honors their config, their credential helper
and their hooks, and cannot disagree with what they see in a terminal.

**`IGitRunner` is how git is run and there is no second way.** Every service here goes
through it, so the flags every run needs are set once. It resolves git through
`IExternalTools`, runs in one repository, and answers a `GitResult` rather than throwing:
`Ran` with an exit code, or `Unavailable`, `TimedOut`, `DidNotStart`. A folder that is not
a repository is `Ran` with a non zero code, which is a different thing from git not being
here, and callers that conflate the two say the wrong thing on screen.

Four settings go on every run, before the subcommand, which is where git reads its own
options:

- `--no-optional-locks`, for the reason below
- `--no-pager`, since a configured pager would take the output away and wait for a key
- `-c core.quotepath=false`, or a path outside ASCII comes back with its bytes escaped
- UTF 8 for input and output. Git speaks UTF 8 whatever the machine, and .NET otherwise
  decodes with the console encoding, which on Windows is the OEM code page

**Paths go in through git's input too, not as arguments.** `GitCommand.Over(paths)` adds
`--pathspec-from-file=-` and `--pathspec-file-nul` and writes them null separated, so a
selection of any size works and a name holding a space or a newline is one path rather than
two. **`git clean` is the one subcommand with no pathspec file**, so it still takes its
paths as arguments. Measured: add, reset, restore, checkout and stash all take them.

**A patch and a commit message go in through git's input, never in an argument.** Every
operating system caps a command line and a real patch passes it. `ProcessRequest` carries
`StandardInput` for this, and `ProcessRunner` writes it while the output pipes are being
read, since a program that answers as it goes fills its output part way through a large
input and a write that waited would wait on a reader that had not started.

The services, all from `AddKitbashGit`:

| | |
|---|---|
| `IGitStatusReader` | the counts the launcher's strip draws |
| `IGitFileStatusReader` | every path that differs, by name, which is what a file tree is |
| `IGitHistoryReader` | commits |
| `IGitDiffReader` | which paths differ, and the lines |
| `IGitStager` | into and out of the index, whole files or single hunks |
| `IGitCommitter` | commits, and who one would be by |
| `IGitBranches` | listing, creating, switching, deleting |
| `IGitSync` | fetch, push, pull |
| `IGitConflictReader` | the three versions a conflict leaves, and settling one |
| `IGitCloner`, `IGitUpdater`, `IGitStatusMonitor` | the launcher's own three |

Porcelain v2, `git status --porcelain=v2 --branch --untracked-files=no`, is the format git
promises not to change between versions, which is the only reason parsing output is
defensible. Untracked files are excluded so an unignored build directory does not swamp
the count.

**`--no-optional-locks` is not optional.** Plain `git status` writes the index back to
refresh its stat cache. Measured: every run produces `Created index.lock`,
`Changed index.lock` and `Renamed index`. Anything watching the git directory then sees
its own read as a change and reads again, forever. VS Code passes `GIT_OPTIONAL_LOCKS=0`
for the same reason.

**Where git keeps a repository is asked, never guessed.** `rev-parse --absolute-git-dir
--git-common-dir --show-toplevel` gives all three in one run, and `GitPlaces` holds them.
The top level is what every path git reports is relative to, and it is not the folder git
was run in. `.git` under the workspace is right only in the simplest case: a workspace can
sit below the repository root, a worktree and a submodule leave a file there instead of a
folder, and `GIT_DIR` can point elsewhere again.

**Updating runs unattended.** `GitUpdater` sets the variables that stop git waiting for a
person, since there is no terminal behind this to type into and it would wait until the
app closed. A credential helper already set up still works, because it answers without
asking. There is a 30 second limit as well, and cancelling kills the process tree, since
a network can accept a connection and then say nothing.

**Updating fetches, then takes the new commits only when they are free.** This is the one
thing in the app that writes to a person's repository, so the bar for doing it is high.
Every one of these has to hold, read **after** the fetch and never before it, since before
it the behind count is whatever it was last time anyone looked:

- the working tree and the index are clean
- the head is a branch and not a commit
- the branch tracks something
- the branch is behind, so there is a reason to
- the branch is not also ahead, so this is a straight line and not two histories

Then `--ff-only` on top, which is the guarantee rather than the check. Even with every count
stale, git will only move the branch pointer forward. It cannot merge, rebase, commit or
rewrite. It is a `merge --ff-only @{u}` rather than a second `git pull`, because the fetch
just above already brought everything down and a pull would go back to the network to learn
what it already knows.

Untracked files are the one thing the clean check does not cover, because the status read
excludes them. Git covers it instead: measured, an untracked file that an incoming commit
would overwrite makes the merge refuse, and the branch stays behind with the local file
untouched.

Measured across every case: clean and behind pulls, a modified file does not, a staged file
does not, diverged does not, a detached head does not, no upstream does not, already level
does not, and an untracked file in the way does not.

### The file list is not the status strip

`IGitStatusReader` counts, `IGitFileStatusReader` names. They are separate because the
strip deliberately excludes untracked files so an unignored build directory cannot swamp
the count, and a file tree cannot do that. The list reads
`status --porcelain=v2 -z --untracked-files=all`, so a new folder lists its files rather
than itself, since a folder cannot be staged.

**Every path form uses `-z`.** Git otherwise quotes any path holding a space or a newline,
and a client that reads those back wrong stages the wrong file. In the null separated form
a rename writes the path it came from as its own record, right after the entry.

A file carries two states, not one. Staged and unstaged are separate changes to the same
path and both are drawn, since editing a file after staging it is ordinary.

### History

`git log -z` with a format of twelve fields separated by the unit separator. `-z` ends each
commit with a null byte, which is the only separator a commit message cannot hold, and the
body is the last field so a message holding a stray separator cannot push a later field out
of place. A commit is still followed by a newline before the next, and that one belongs to
neither.

A repository with no commits fails this rather than answering nothing, since there is no
head to walk. Empty is the answer either way.

### Diffs

`GitPatchReader` reads git's unified form into files, hunks and lines, and runs git for
none of it, so a patch from anywhere can be read. `GitPatchWriter` writes it back.

**The gate is that git's own output, read and written back, is the same bytes.** Anything
the reader drops is a hunk that cannot be staged. That is what the tests assert.

- Split on the newline alone. A carriage return before it belongs to the file's content.
- Git writes an empty context line as a lone space, so a reader that trims lines loses it.
- The hunk header counts say how long a hunk is, and reading to them rather than to the
  next marker is what keeps an empty line apart from the end of the output.
- `\ No newline at end of file` has to be kept and written back, or a patch built from
  these hunks adds a newline nobody asked for. It can appear mid hunk as well as at the end.
- `-c diff.noprefix=false -c diff.mnemonicPrefix=false` on every diff, since the reader
  takes the `a/` and `b/` off by position and a repository can turn either off.
- `--no-ext-diff` and `--no-textconv`, or a configured tool answers instead of git and
  gives text that cannot be applied back.
- `--full-index`, so the blob names are whole rather than abbreviated. That is what lets
  `git apply` be sure which version a patch built from these hunks is patching.

**A merge commit needs `--diff-merges=first-parent`.** Measured on git 2.55: `diff-tree`
writes nothing at all for a merge by default, and the plain `--first-parent` does not
narrow it, it writes both parents and the same file arrives twice. `diff-tree --root` is
what lets the first commit be shown at all, since it has no parent to compare with.

An untracked file is read with `diff --no-index -- /dev/null <path>`, which reports a
difference through the exit code the way `diff` does, so one means it worked. Git matches
that name itself rather than opening it, on Windows as well. Nothing is staged to look at
a new file.

### Staging one hunk and not the rest

The patch is built here and handed to `git apply --cached`. There is no other way: git
offers this only through `add -p`, which wants a terminal.

**A hunk left out changes how long the file is, so every later hunk starts somewhere else
than the patch says.** The side that moves is the one describing the file git will apply
the patch to:

- going in, that is the old side, so skipped hunks accumulate `old - new` and each
  emitted hunk's **new** start moves by it
- coming back out, `apply --reverse` treats the patch's new side as the preimage, so
  skipped hunks accumulate `new - old` and each emitted hunk's **old** start moves by it

Getting that backwards still applies cleanly when the first hunk is taken, which is why
the tests take a later hunk and skip an earlier one that changes the file's length, then
read the index back with `git show :path` and compare it whole.

`--whitespace=nowarn`, since a patch built here is git's own output with hunks removed and
a warning would be about code the person did not write in this gesture.

**Unstaging is `reset`, not `restore --staged`.** The second needs a HEAD it can resolve, so
it fails outright on a repository with no commits yet, which is exactly where the first file
anybody stages gets unstaged. `reset` handles both, and an unborn head or a path the head
never had simply leaves nothing in the index.

**A new file has to be tracked before a hunk of it can be staged.** `add --intent-to-add`
records it with no content, which is what makes it show in a diff at all.

### Committing

`commit --file=-`, so the message goes in through git's input. Nothing needs quoting, a
message starting with a dash is not an option, and there is no length limit.

**`--cleanup=whitespace`.** Git otherwise strips lines starting with a hash out of the
message, and a line like that is ordinary text when it was typed into a box rather than
taken from an editor template.

Hooks run. They are the person's own, and a client that skipped them would be lying about
what the repository does.

`ReadIdentityAsync` reads `user.name` and `user.email` and answers null when either is
unset, since a person who never set them cannot commit and a client should say so before
offering the button rather than after git refuses.

### Branches

`for-each-ref` over `refs/heads` and `refs/remotes` with the fields separated by the unit
separator, one line per ref. A ref name cannot hold a newline, so line based is safe here
in a way it would not be for a path. `%(upstream:track)` is git's own note,
`[ahead 1, behind 2]` or `[gone]` or empty.

`switch` rather than `checkout`, so a branch and a path can never be confused for each
other, and `switch --detach` for a commit.

### Which branch is the base, and how far from it

`IGitRefReader` answers the questions a branch list cannot. **`refs/remotes/origin/HEAD` is
written by a clone and by nothing else**, so a repository built with `remote add` and
`fetch` has none and a caller has to fall back rather than treat the absence as an error.
`remote set-head origin --auto` is what fills it in after the fact.

`rev-list --left-right --count baseline...head` is the pair of numbers. **Left is what the
baseline has alone, which is what the head is behind by**, and getting the two the wrong
way round reads as up to date when it is not. Both counts on `GitDivergence` are about the
head.

**A revision that does not resolve answers null, not level.** A client that took the two as
the same would tell somebody they are up to date with a branch that is not there.

### Push and pull

**Git says why in prose rather than in an exit code**, so prose is what there is to read,
and `GitSyncOutcome` names the ones a person needs told apart. The distinctions that
matter: rejected, which means this branch is behind and only fetching first gets through,
against diverged, which means both sides moved and a merge is what settles it, against no
upstream, which means there is nowhere to send it yet.

Pull fetches and then merges what is already here, rather than running `git pull`, which
would go back to the network to learn what it just learned. `--ff-only` by default.

**Never a rebase.** A rebase settles the same conflict once per commit, which for a binary
file is once too many.

### Conflict stages

`ls-files --unmerged -z` gives one record per version, so a path with three arrives three
times: stage one is what both sides started from, two is ours, three is theirs. A side is
missing when that side had no file, which is what a delete against an edit looks like, and
there is no base at all when both sides added the same name. A merge tool that assumes
three of everything breaks on both.

The content is read with `cat-file blob` off the object name, as text. A binary file needs
another way and there is not one here yet.

**A stopped rebase writes `CHERRY_PICK_HEAD` as well as its own head**, so `REBASE_HEAD` is
asked about first or every stopped rebase reads as a cherry pick. What is under way is what
says which command undoes it.

Taking one side writes the working tree and leaves the index holding three versions, so the
path is still conflicted until it is added. `TakeAsync` does both.

### Cloning

`IGitCloner` is the launcher's Clone workspace from git. It runs `git clone` with the
same unattended variables the updater uses, which are `GitEnvironment.Unattended` now
that two callers need them.

**An address is parsed before it reaches a process.** `GitRemote` accepts http, https,
`ssh://` and the scp form such as `git@example.com:team/game.git`, and refuses everything
else, a local path included. It also carries the folder name git would pick, which is the
last path segment without `.git`, so the destination is on screen before anything is
written. A single character before the colon is a Windows drive letter, so an scp host
needs two or more.

**It refuses a destination that exists and clears up one it made.** Nothing was there a
moment before, so a folder there after a failure is git's half written one, and cancelling
or timing out leaves the same thing. The parent is created when it is missing. There is a
30 minute backstop and the dialog's Cancel stops it sooner.

The outcome is read out of git's words the way the updater reads its own, and anything
unrecognised is a plain failure.

Measured on this machine against a repository served over http by `python3 -m http.server`
from a temporary folder, which is dumb http, so the real client with its smart protocol
fallback ran: a clone landing with its files and its `.git`, a second one refused, a
repository that is not there reported as `NotFound` with no folder left behind, a parent
created two levels deep, and a cancelled clone clearing up after itself.

### Watching a repository

Modeled on what editors do and checked against two, the VS Code git extension and
SourceGit. Both watch the git directory rather than poll, both refuse to recurse into it,
both throw away lock files, and both debounce before running git.

**Watch a few named folders, never a tree.** `IDirectoryWatcher` has no recursive option
at all. A recursive watch costs one kernel handle per directory underneath, drawn on Linux
from a pool shared with every other application: measured on this repository, 242 against
1. A fetch or a repack then writes thousands of files under `objects` and says nothing
that `FETCH_HEAD` did not. VS Code recurses over the working tree instead, which it can
afford because the editor already runs one shared native watcher there. This app has no
such watcher, and the beat below does that job.

`GitPlaces.Watchable` is the list: the git directory, the common directory when this is a
worktree, and the reftable directory when there is one.

**A reftable repository writes nothing at the top level.** Git 2.45 added a second way to
store refs. A repository created with it leaves `.git/HEAD` a stub reading
`ref: refs/heads/.invalid` that never changes, and keeps every ref under `.git/reftable`.
Measured on git 2.55: a branch rename there changes nothing a watch on the git directory
alone would see. That folder is watched for exactly this.

**Lock files are ignored by name.** A `.lock` file is git reserving the right to write,
not git having written, and the write arrives under its own name a moment later.

**The beat is what the app relies on.** Every five seconds, and it catches the four things
no watch reports: a file edited in another editor, a push that moves only a remote ref, a
filesystem that reports nothing, and a watch that died because its folder was replaced. It
also puts dead watches back, which is why `IDirectoryWatcher.Watching` clears itself when
a watch dies. VS Code closes the push case with an extra watch on the upstream ref file,
which is more machinery than a status bar earns.

**Nothing runs while the app is not in front.** `IGitStatusMonitor.IsActive` parks the beat
and every watch driven read, and coming back reads at once. The launcher drives it from
`Activated` and `Deactivated`. VS Code parks its refresh the same way.

Reads are debounced 400ms, and a watch driven read cannot run more than once a second
whatever the debounce lets through.

**Switching workspaces replaces everything.** `Follow` stops the beat, drops the debounce,
disposes every watch, clears the status so it never describes a folder it is not
following, and reads the new folder at once. That read waits its turn rather than giving
up, since a read still running belongs to the folder nobody is looking at any more.
Following the folder already followed does nothing, which is why the launcher takes what
the monitor holds rather than blanking the strip itself.

Measured on this machine, both ref backends: a branch switch, a branch rename and a
staging all show up in about 400ms, a working tree edit within the beat, and an idle
repository produces no git processes at all. Counted from the kernel: a files repository
is 1 watch, a reftable one 2, a worktree 2, a folder that is not a repository 0, and
thirty switches back and forth leave the same 2 they started with.


### Tests

`tests/Kitbash.Core.Tests` is the repository's first test project, and it is here because
this is where the parsing is. Every test builds a real repository in a temporary folder and
runs the real git. Nothing is faked, because what is being tested is agreement with git
itself, and a fake that agreed with the parser would prove nothing.

`TestRepository` sets `user.name`, `user.email`, `commit.gpgsign`, `core.autocrlf`,
`merge.conflictStyle` and `diff.algorithm` in the repository it makes, so a person's own
settings cannot change what the tests measure. Push and pull run against a bare repository
on the same machine, so no network is involved and git says the same things it would over
one.

Every test skips rather than fails when there is no git on the machine.

**A found bug: the strip has never shown a detached head as a commit.** Git writes
`# branch.oid` before `# branch.head`, so the old reader had not yet learned the head was
detached when the oid went past, and the word `detached` was drawn instead of the short
commit its own comment promised. The oid is now kept whichever line comes first.
