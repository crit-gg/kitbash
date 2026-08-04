# GitHub login

Signing a person in to GitHub, keeping them signed in, and holding the token somewhere
safe. Step 7 of the order of work in `tool-distribution.md`, built ahead of the rest
because it has the longest tail of setup outside this repository.

## Status

**Nothing here is built.** `ISecretStore` landed in commit f3824d6 and is what this stands
on. No GitHub App exists yet, so nothing here has been run against the real thing.

## Settled

- **A GitHub App, not an OAuth App.** Only a GitHub App issues refresh tokens.
- **Device flow, not the authorization code flow.** It is the only flow that refreshes
  without a client secret in the binary.
- **One permission, Contents: Read.** Releases, release assets and any file in the tree
  all fall under it.
- **The refresh token lives in the keyring. The access token lives in memory.**
- **Signing in is not the same as having access.** A person can authorise Kitbash and
  still reach nothing, because installing the App is an owner's action.
- **Signing out is local.** Kitbash cannot revoke its own authorization.

## Why a GitHub App and why device flow

Refreshing a user token is `POST https://github.com/login/oauth/access_token` with
`grant_type=refresh_token`, and **`client_secret` is required unless the token came from
device flow**. That exception is the entire reason for this shape.

Kitbash is a public client. Anything it ships can be read out of the binary, and GitHub
says so: a native app "will have to ship the client secret in the application's code".
PKCE, added in July 2025, does not change that. GitHub states it "does not distinguish
between public and confidential clients", and the same changelog says the device code flow
does not use PKCE at all. So PKCE is hardening for a flow we are not using.

| | Refresh | Secret in the binary | Reaches |
|---|---|---|---|
| GitHub App, device flow | 8 hours, 6 month refresh | none | repos the App is installed on |
| GitHub App, auth code and PKCE | same | required | same |
| OAuth App | none, token never expires | required unless device flow | everything the user can see |

Device flow also needs no redirect URI, so there is no loopback listener, no port to bind
and no firewall or sandbox failure to handle. On a desktop that must work on any Linux
distribution that is worth a great deal.

**The risk being accepted.** With no redirect URI, anyone holding the client id can start a
device flow and try to talk a person into approving it. The blast radius is bounded by the
App's permissions, which is read access to contents on repositories where it is already
installed. That is the trade against shipping an extractable secret, and it is a decision
rather than an oversight.

## What has to exist outside this repository

None of this is code and all of it blocks testing.

1. A GitHub App registered, owned by whoever owns the tool repositories.
2. **Device flow enabled** in the App's settings. It is off by default.
3. **Expire user authorization tokens enabled.** Off means no refresh token is ever issued
   and the whole design collapses to an OAuth App.
4. Repository permission **Contents: Read**, and nothing else.
5. The App installed on the organisation or the repositories holding tools, by somebody
   with owner rights.

**The client id is not a secret** and is compiled in, the way `UpdateSettingsSchema` holds
the feed address. Consider a `github.clientId` descriptor with that constant as its
default, on no page, so a different App can be pointed at without a rebuild. That mirrors
`updates.feed` exactly and costs one descriptor.

## The flow

Every response is form encoded unless the request sends `Accept: application/json`, so
every request here sends it.

**Ask for a code.** `POST https://github.com/login/device/code` with `client_id`. No
`scope`, because a GitHub App user token takes the App's permissions rather than scopes.
Back comes `device_code`, `user_code`, `verification_uri`, `expires_in` of 900 and
`interval`.

**Show the code and open the browser.** The person types `user_code` at
`verification_uri`. Opening it goes through `IPlatformServices.OpenInBrowser`, which
already exists and already handles a machine with no launcher installed.

**Poll.** `POST https://github.com/login/oauth/access_token` with `client_id`,
`device_code` and `grant_type=urn:ietf:params:oauth:grant-type:device_code`. No
`client_secret`.

| Error | What it means |
|---|---|
| `authorization_pending` | nobody has entered the code yet, keep polling |
| `slow_down` | poll too fast, add five seconds and use the interval in the response |
| `expired_token` | the fifteen minutes ran out, start again |
| `access_denied` | the person pressed cancel, do not retry |

**Never poll faster than `interval`**, and take the new interval from a `slow_down` rather
than assuming five. A client that ignores this gets the App rate limited.

Success carries `access_token`, `expires_in` of 28800, `refresh_token`,
`refresh_token_expires_in` of 15897600 and `token_type` of bearer.

Then `GET /user` for the login, which is what names the keyring entry and what the app
shows.

## Refreshing

Same endpoint, `grant_type=refresh_token`, `client_id` and `refresh_token`, and no secret
because the token came from device flow.

**A refresh token is single use and it rotates.** GitHub is explicit: once you use a
refresh token, that token and the old access token stop working. Three things follow, and
all three are correctness rather than polish.

**The new refresh token reaches the keyring before anything else happens.** The old one is
dead the moment GitHub answers, so a write that lands after the session is already in use
leaves a person signed out at the next launch with no way to tell why. Write first, then
publish the access token to callers.

**A failed write is not a failed sign in.** The access token is good for eight hours. If
the keyring refuses, the session keeps working and the app says it will not persist. That
is the same answer `ISecretStore` gives back as `Unavailable` and it should read the same
way to a person.

**Refresh is single flight.** Two callers finding an expired token at the same moment must
not both refresh, because one of the two rotations wins and the loser is signed out. One
lock, one refresh, both callers wait on it.

**Refresh early rather than on failure, and also on failure.** Compute an absolute expiry
from `expires_in` when the token arrives, through the `TimeProvider` already registered in
Core, and refresh with a margin of a few minutes. Also treat a 401 as a reason to refresh
once and retry, because a clock that is wrong is not a thing this app can rule out.

**Six months of not opening Kitbash means signing in again.** The refresh token expires
and there is nothing to be done about it. Say so plainly rather than looking broken.

## Where the token lives

The refresh token goes in the keyring under `SecretKey.Parse("github", login)`, which is
already the example on `ISecretStore`. The label a person sees becomes
`Kitbash: github (octocat)`, which is legible in Seahorse or Credential Manager.

**The login itself goes in application state**, not the keyring, because the account has to
be known before the token can be read at startup. It is not a secret. Signing out clears
both.

**The access token is never written anywhere.** It is good for eight hours, a refresh is
one request, and writing it would double the exposure for nothing.

## The three states

The reason this matters is that two of them look identical if nobody writes the code for
them.

| State | How it is detected | What it says |
|---|---|---|
| Signed out | no login in state, or no token in the keyring | Sign in to GitHub |
| Signed in, no installation | `GET /user/installations` returns an empty list | signed in as someone, the App is installed nowhere they can see, with a button to install it |
| Signed in and working | installations present | the account, and a way to sign out |

The middle one is the one that gets skipped. Without it an empty tools list looks exactly
like a bug, which is the same failure `tool-distribution.md` already lists as an open
question about a repository that cannot be reached.

**Signing out removes the token from this machine and nothing else.** Revoking an
authorization needs the client secret, which this app does not have, so the app should say
where to do it rather than implying it has.

## The HTTP seam

`IWebContent` cannot serve this. It is GET only with no headers, no request body and no
authentication.

Two seams rather than one, because otherwise they are circular. The token endpoints are
unauthenticated form posts and belong below the session. Everything else needs a token and
belongs above it.

- **`IGitHubOAuth`** the three token calls. Depends on nothing but HTTP.
- **`IGitHubSession`** holds the account, the access token and its expiry. Owns the
  refresh, the single flight lock and the keyring. Depends on `IGitHubOAuth` and
  `ISecretStore`.
- **`IGitHubSignIn`** starts a device flow and completes it. Two phases, because a person
  has to read the code between them.
- **`IGitHubHttp`** authenticated requests. Depends on `IGitHubSession`.

Every request carries `Accept: application/vnd.github+json`, `X-GitHub-Api-Version` and a
`User-Agent`, which GitHub rejects requests without.

**The asset download trap.** A private release asset is
`GET /repos/{owner}/{repo}/releases/assets/{asset_id}` with
`Accept: application/octet-stream`, which answers a redirect to a signed URL. **The
`Authorization` header must not follow that redirect.** The signed URL carries its own
credentials and rejects a request that also carries a bearer token. So automatic redirect
following is turned off and the 302 is handled by hand. This is not needed until the
installer exists, but the seam should be shaped for it now rather than reshaped later.

**Rate limits are why this exists at all.** 60 requests an hour unauthenticated against an
address, 5000 authenticated. `tool-distribution.md` notes that a conditional request
answered 304 is documented not to count and that this should be confirmed rather than
assumed. That confirmation belongs with the repository work, not here.

## What this changes in the code

New, in `Kitbash.Core/GitHub/`, registered by `AddKitbashGitHub`, which takes
`AddKitbashSecrets` and `AddKitbashPlatform`.

Nothing existing changes except `KitbashCoreServices` and, if the client id becomes a
setting, one schema. No UI. Nothing in `Kitbash` or `Kitbash.Ui`, so this drops in
underneath step 7 when the rest arrives.

## Order of work

1. **The value types and the token responses.** Records, parsing, absolute expiry from
   `expires_in`. No network.
2. **`IGitHubOAuth`**, the three calls, against a fake HTTP handler. The polling state
   machine including `slow_down` is the part worth exercising hardest, and it can be
   exercised entirely without GitHub.
3. **`IGitHubSession`.** Restore, refresh, rotate, single flight, sign out, and every
   `ISecretStore` outcome including `Unavailable`.
4. **`IGitHubSignIn`**, joining the two phases.
5. **`IGitHubHttp`** and the three states, once an App exists to answer.

Steps 1 to 3 are the bulk of the work and none of it needs the App to exist, so registering
the App is not on the critical path until step 5.

## Open questions

- **Whether the client id becomes a setting** or stays a constant. `updates.feed` is the
  precedent for either answer.
- **What happens to a signed in session when the workspace changes.** Nothing, probably,
  since the account is per machine rather than per workspace, but it has not been decided.
- **Whether a second account is ever wanted.** The keyring can hold several under different
  accounts and application state currently holds one login. Left unsolved deliberately.

## What could not be tested here

**No GitHub App exists**, so none of the network half has been run. Every endpoint,
parameter and error code here is from GitHub's documentation rather than from a response
anybody has seen.

This machine is Linux, so the Windows half of `ISecretStore` underneath all of this is
still unexecuted, as recorded in the `kitbash-platform` skill.
