# PR #9 Review — `TakoKylo:dev1`

PR: https://github.com/ckhawks/ToastersReskinLoader/pull/9
Title: QoL: server browser favorites/block/queue, main-menu shortcuts, scoreboard polish
Size: ~3700 LOC across 16 files.

Game source cross-referenced at `C:\PuckModdingTools\PuckDecompiled323Project`.

---

## Resume plan

Create a worktree off the PR head so it doesn't disturb the local `stellaric-2026-05-14` WIP:

```
gh pr checkout 9 --repo ckhawks/ToastersReskinLoader --detach
git worktree add ../ToasterReskinLoader-pr9 -b pr9-fixes FETCH_HEAD
```

Push a fix commit to `TakoKylo:dev1` (PR has maintainer-edits enabled by default) or to our own fork branch and open a follow-up.

---

## Merge blockers (fix before pushing our commit)

### 1. `ServerSlotQueue.PingTargetServer` ignores `CancellationToken`
`src/qol/serverbrowser/ServerSlotQueue.cs:346-396`

`tcp.Connect()` and `responseEvent.Wait(PingResponseTimeoutMs)` are synchronous and take no token. On cancel-then-rearm-with-different-target, the still-running ping's `finally` writes its outcome into the *new* queue's `_lastPingUnreachable` / `_targetEndPoint`, polluting state.

Fix: capture `ep` at top (already done), but also stage results into locals and only publish if `token` is still uncancelled AND `_targetEndPoint == ep`.

### 2. TCPClient event lambdas leak + post-`Disconnect` `Set()` on disposed MRES
Same file, `:355-377`. Verified against `TCPClient.cs:12,27` — `OnConnected` / `OnMessageReceived` are plain C# events; the PR subscribes lambdas every ping and never unsubscribes. If the socket flushes a buffered packet on `Disconnect()`, the callback fires after `responseEvent` is disposed.

Fix: hold delegate references, `-=` in finally; or wrap `responseEvent.Set()` in try/catch with `IsSet` guard.

### 3. `OnConnectionRejected` mutates queue state without sync
`src/qol/serverbrowser/ServerSlotQueue.cs:168-189`

Same-target re-rejection mutates `_targetPassword` / `_targetName` while `PollLoopAsync` is still iterating. If a `TryJoin` is marshaled to main while these mutate, `Client_StartClient` may use a password that doesn't match the prompting rejection.

Fix: copy `_targetEndPoint` / `_targetPassword` into locals before `Client_StartClient`, or guard mutations with a lock.

---

## Bugs (real, not blockers)

### Unlock 🔓 badge gated on wrong toggle
`src/qol/ServerBrowserSort.cs:695-737, 329-361`

`StyleServer_AddBadges_Postfix` short-circuits when favorites+blocks are both off — also hides the saved-password badge, which is on an independent toggle. And toggling `enableSavedServerPasswords` doesn't fire `RefreshForCurrentBrowser`.

Fix: separate postfix or independent enable gate for the badge.

### MouseMove handlers stack on rows after favorites toggle
`src/qol/ServerBrowserSort.cs:767-803`

`RefreshForCurrentBrowser:344` clears `TooltipMarkerCls` on disable, so on re-enable a second handler registers per row.

Fix: store delegates for `UnregisterCallback`, or don't clear the marker class.

### `UnicodeFontFallback.Disable → Apply` leaks `TMP_FontAsset` Unity assets
`src/qol/UnicodeFontFallback.cs:140-169`

`TMP_FontAsset.CreateFontAsset` allocates real Unity assets; orphaned ones are still findable via `Resources.FindObjectsOfTypeAll`. Toggling on/off grows the asset pool linearly.

Fix: `Object.DestroyImmediate(fb, allowDestroyingAssets: true)` before clearing the list.

### `ScoreboardPolish._timeLabel` stale across scene reloads
`src/qol/ScoreboardPolish.cs:38-95`

Managed ref outlives the destroyed VE; `Render()` writes to a detached element forever.

Fix: re-cache when `_timeLabel.panel == null`.

### `OnClickQuickJoin` fire-and-forget Task
`src/qol/MainMenuButtons.cs:277`

Exceptions past the inner `catch` go to `TaskScheduler.UnobservedTaskException`. Add a `ContinueWith(..., OnlyOnFaulted)` to log.

### `MarshalToMainSync` MRES dispose race
`src/qol/MainMenuButtons.cs:568-584`

`using var done = new ManualResetEventSlim(false)` — if `Wait` times out (e.g. loading screen), `done.Set()` may still run later on a disposed event.

Fix: non-disposed event, or guard `Set` in try.

### `PingTargetServer` log line spams on socket exceptions
`src/qol/serverbrowser/ServerSlotQueue.cs:389`

Unconditional `Plugin.Log` defeats the `LogEveryNPings` throttle. Move under the verbose gate.

---

## Concerns

- **O(N² log N) sort** — `ServerBrowserSort.GetEndPointFromRow:276-285` is an O(N) map scan per comparator call. Build a reverse `Dictionary<VisualElement, EndPoint>` once per sort.
- **`UiTextShadow.WalkAndApply`** runs full `Query<TextElement>` on every geometry change (`:157-164`); debounce via `schedule.Execute`.
- **`HideInactiveChat._touchedDescendants`** accumulates dead VE refs as chat rows recycle.
- **`enableTrustedModLists` defaults to `true`** — auto-clicking a mod-trust popup is a security decision. Default should be `false`.
- **`MissingModsPopupSuppression`** re-enabled after being commented-out; should re-validate.
- **`enableMainMenuQuickJoin` / `enableMainMenuServerBrowser`** placed in "Server Browser" config section but they are main-menu features.
- **`ScoreboardPolish.Tick`** runs every frame even in menus — gate on `_timeLabel.style.display != None`.

---

## Nits

- `PingEveryNTicks = 3` but comment says "≈1s" (it's 750ms at 250ms tick).
- `ShowQueuePanel(_targetName)` accepts the name but writes a hardcoded `"WAITING FOR SLOT TO OPEN"`.
- `ScoreboardPolish.cs:4` references `enableScoreboardTextShadow` — stale comment, that flag lives in `UiTextShadow`.
- `QuickJoinAsync` uses `OrderBy().First()` instead of `MaxBy`.

---

## False alarms (verified against decompile, do not "fix")

- ✅ `UIManager.Matchmaking` IS a public field (`UIManager.cs:540`) — `GetField` reflection works in both `ServerSlotQueue.GetMatchmakingPanel:695` and `MainMenuButtons.GetPanel:546`.
- ✅ `UIView.Show()` returns `bool` (`UIView.cs:87`) — `__result` postfix gate in `UiTextShadow.cs:81` attaches cleanly.
- ✅ `UIChat.IsFocused` is set in `StartInput`/`StopInput` (`UIChat.cs:94,106`) — `EscClosesMenus` chat-focus prefix reads the right field.

---

## Priority order for our fix commit

1. ServerSlotQueue cancellation gap (#1) + lambda/MRES leak (#2) + state race (#3) — one focused commit on `ServerSlotQueue.cs`.
2. Unlock badge gate (independent of favorites/blocks).
3. UnicodeFontFallback asset leak.
4. ScoreboardPolish stale label ref.
5. Everything else as follow-ups.
