---
name: kitbash-git
description: "Kitbash git integration. Reading porcelain v2, the guards on updating a repository, and how one repository is watched with directory watches plus a beat. Read before changing anything that runs git."
---

## Git

`Kitbash.Core/Git` reads a repository by running git, and there is no library. Running
git works with whatever git the person has, honors their config, their credential helper
and their hooks, and cannot disagree with what they see in a terminal.

`IGitStatusReader` runs one command and parses it. `IGitFetcher` fetches.
`IGitStatusMonitor` follows one repository so the launcher's status bar stays true when
git is used from outside the app, which is where it is mostly used. All three come from
`AddKitbashGit`.

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
--git-common-dir` gives both, and `GitPlaces` holds them. `.git` under the workspace is
right only in the simplest case: a workspace can sit below the repository root, a worktree
and a submodule leave a file there instead of a folder, and `GIT_DIR` can point elsewhere
again.

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

