# Apply patch

How a person who does not use git applies a patch a developer sent them. It is a launcher
feature and not a tool: Apply patch is the one item in the git menu, which is the caret on
the status bar's Update button.

## Status

**Built.** `IGitPatchApplier` in `Kitbash.Core/Git`, `ApplyPatchDialog` and
`ApplyPatchViewModel` in the launcher, and `StatusBarSplitButton` in `Kitbash.Ui`. The git
sequence was run by hand against git 2.56 on Linux before it was built on, and
`GitPatchApplierTests` replays every case against real git. **Windows and macOS have not run
it.**

## Decided

- **A launcher feature, in the git menu.** It was first designed as a script tool. Putting it
  in the launcher means nothing to install or publish, and none of the three gaps the script
  modal had: the path field filters, the result is a sentence, and nothing can stop it part way.
- **Only `git format-patch` files.** `git am` reads the mail form and nothing else, so a plain
  diff is refused on the field before anything runs.
- **The commit is the developer's.** `git am` records the patch's author and the person as
  committer, with the patch's own subject and body. Nothing is added to the message.
- **It applies around the person's own changes**, staged and unstaged, and the commit holds
  the patch alone.
- **Ordinary git commands only.** No environment variable, no temporary index, no plumbing and
  nothing per OS. A temporary index under `GIT_INDEX_FILE` was designed first and refused.
- **It never pushes.** The toast says it is a commit on the branch, and pushing stays a
  separate act.
- **Godot being open is not checked.**
- **There is no undo.** A developer reverts the commit when one is needed.

## The promise

**Every outcome but two changed nothing.** Applied is the patch as a commit with the
person's work back where it was. Stranded is git refusing to put something back, which
leaves the person's work in a stash and says which. Every other answer leaves the repository
as it was, and the message says Nothing was changed.

**In progress work is never lost.** It is either where the person left it or in a stash git
keeps, and a stash is only ever dropped by a pop that succeeded.

## The sequence

Checks first, which write nothing:

1. Git is here, the workspace is a repository, the head is a branch with a commit.
2. Nothing is under way: `IGitConflictReader.ReadStateAsync`, plus the `rebase-apply` and
   `rebase-merge` folders under `--absolute-git-dir`, since a stopped `git am` and some rebase
   stops write no head, plus `ls-files --unmerged`.
3. No stash an earlier run left. See Set aside work.
4. `user.name` and `user.email` are set, since `git am` commits.
5. The file is a mail with at least one file in it, and every binary change carries data.
6. **Already applied** is `apply --check` failing and `apply --check --reverse` passing.

Then the three commands:

7. `git stash push --message "Kitbash: before applying <file>"`. Untracked files stay out,
   and a patch adding a file that is already there is refused by `git am` before it writes.
   **Git makes no stash when there is nothing to save**, so `refs/stash` is read before and
   after and only a new tip counts as ours.
8. `git am <file>`.
9. `git stash pop --index stash@{n}`, where `n` is found by the stash's object name so an
   entry pushed meanwhile is never the one taken. `--index` puts staged changes back staged.

Any failure after step 7 rolls back, in order, and stops at the first refusal as Stranded:

- `git am --abort` when `rebase-apply` is there.
- `git reset --merge <the starting commit>` when the head moved. **`--merge` and not
  `--hard`**: measured, a file another program wrote mid run survived `--merge` and was lost
  to `--hard`, with the stash pop conflict in place either way.
- The same pop, onto the commit the stash was made on, which git can always do.

**Measured outcomes**, all identical to the byte afterwards unless they applied: an edit
elsewhere in a patched file applies around it, an edit on a patched line and one on the line
next to it are both in the way, since git treats neighbouring changes as overlapping, an
`applypatch-msg` hook refusing, an untracked file where the patch adds one, a plain diff, a
patch already applied. An older stash entry of the person's own survives a run.

## Set aside work

A run stopped between steps 7 and 9, which takes a crash or a power cut, leaves the
person's work in a stash whose message starts with `Kitbash: before applying`. **The dialog
looks for one as it opens**, refuses to apply another patch until it is back, and offers to
put it back.

Putting it back is `git am --abort` when one is under way, then the pop, **and only onto a
tree with no tracked changes**, so anything a failed pop leaves came from the stash and
`git reset --merge HEAD` can clear it without touching anything the person did since. A tree
with changes is left alone and the person is told to ask a developer. Measured: back exactly
after a stop before `git am`, back onto the applied patch after a stop after it, and kept in
the stash where it overlaps the patch.

**Nothing outside the dialog notices it.** A person who never opens Apply patch again is not
told their work is in a stash.

## What the person reads

Each failure is an alert in the dialog, since it describes what is on screen and the person
has to act on it. Copy details puts git's own words on the clipboard for a developer.
Success closes the dialog and raises a toast, `<subject> is now a commit on <branch>.`

| Outcome | Title | Detail |
|---|---|---|
| AlreadyApplied | This patch is already in your copy of the project. | Nothing was changed. |
| DoesNotFit | This patch does not fit your copy of the project. | Nothing was changed. Send the details to your developer. |
| InTheWay | Your own changes to door.tscn are in the way of this patch. | Nothing was changed. Ask your developer to help with those changes first. |
| NotAPatch | Kitbash can only apply a patch made with git format-patch. | Nothing was changed. |
| MissingBinary | This patch is missing the contents of door.png. | Nothing was changed. Ask your developer to make it again with --binary. |
| Busy | Git is in the middle of something else. | Nothing was changed. Ask your developer to finish it first. |
| NoIdentity | Git does not know your name yet. | Nothing was changed. Ask your developer to set it up. |
| Refused | Git refused this patch. | Nothing was changed. Send the details to your developer. |
| Stranded | Git could not put everything back. | Your changes are safe in git as `<stash>`. Ask your developer to restore them. |

**Cancel and the close glyph do nothing while git runs.** Stopping part way is the one thing
that could leave work set aside, so `OnClosing` refuses while the view model is working, and
the applier takes no cancellation token.
