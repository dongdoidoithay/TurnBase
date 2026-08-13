# Task: TeamSelectScreen hero list overflow fix

## §0. Findings

Bug flagged during task-codex.md (spawn_task `task_26720454`): `TeamSelectScreen`'s
`HeroListContainer` (`Assets/_Project/Scripts/Meta/TeamSelectScreen.cs`,
`Assets/_Project/Resources/Prefabs/UI/Screens/UI_TeamSelect.prefab`) has no `Mask`/`ScrollRect`.
`RefreshHeroList` lays out 1 `UI_HeroCard` row per hero at `anchoredPosition.y = -i * 70`, inside a
container whose designed viewport is only 380px tall (~5.4 rows). This was invisible with the
original 6-hero roster (6*70=420, already slightly over but close enough nobody noticed) and became
a real, confirmed bug once task-hero-roster.md grew the roster to 24 heroes (24*70=1680px) — heroes
past row ~5 render off the visible panel and are completely unreachable (can't toggle IN TEAM/BENCH,
can't view gear).

Confirmed via `manage_prefabs get_hierarchy`: `HeroListContainer` was a bare `RectTransform` with no
scroll/clip infrastructure, direct child of `Content` alongside `GearPanelContainer`/`Footer`.

## §1. Scope

Small, targeted bugfix — not a new feature, no task-file-before-code requirement
([[feedback_write_task_file_before_code]] only applies to substantial new systems). Scope: make the
hero list scrollable so all 24 (and any future count) heroes are reachable. Did NOT touch
`GearPanelContainer` (fixed 6 slots forever, `SLOTS.Length` never grows — no analogous risk) or
`QuestScreen`'s narrow-label truncation issue (unrelated, separately noted in
[[feedback_unity_mcp_ui_gotchas]]).

## §2. Implementation

- `UI_TeamSelect.prefab`: inserted new `HeroListViewport` GameObject between `Content` and the
  existing `HeroListContainer`, taking over `HeroListContainer`'s old rect (anchorMin/Max=(0,1),
  pivot=(0,1), anchoredPosition=(0,0), sizeDelta=(360,380)). `HeroListViewport` carries
  `RectMask2D` (clips overflow) + `ScrollRect` (`content`=`HeroListContainer`, `horizontal=false`,
  `vertical=true`, `movementType=Clamped`, `viewport` left unset — defaults to its own
  RectTransform, which already has the mask). `HeroListContainer` reparented under it, same
  anchor/pivot/anchoredPosition=(0,0) so the reparent was a no-op visually (verified: world position
  unchanged after the move).
- `TeamSelectScreen.cs`: `BuildShell()` now finds `HeroListViewport/HeroListContainer` instead of
  the old direct path. `RefreshHeroList()` sets `_heroListContainer.sizeDelta.y` to
  `Max(viewport.rect.height, heroes.Count * rowH)` each rebuild — real scroll range for the actual
  roster size, clamped to never shrink below the 380px viewport (so a short roster doesn't make
  `ScrollRect` think there's nothing to scroll... though a short roster naturally has nothing to
  scroll, this just keeps `sizeDelta` sane rather than smaller than its own viewport).

## §3. Verification

- Compile: `refresh_unity` clean, no console errors.
- EditMode suite: 402/402 still green (`run_tests`, job `87ccb2f6...`) — this is a UI/prefab-only
  change, no combat/meta logic touched, so this was a regression check, not new coverage.
- Structural Play-mode check (via reflection, real live profile with the actual 24-hero roster):
  opened the real `TeamSelectScreen.Open()` through `MetaSceneInstaller`'s live `_profile`/
  `_teamSelectScreen` fields. Confirmed: `_heroListContainer.sizeDelta = (360, 1680)` (=24*70),
  `viewport.sizeDelta` stayed fixed at (360,380), `ScrollRect.content == _heroListContainer`,
  `RectMask2D` present, `movementType == Clamped`, exactly 24 child cards (not 48 — first attempt in
  the same session hit the documented MCP frame-stall bug, see below, and confirmed reflection was
  double-invoking `Open()` across two separate calls with `Destroy()` not yet applied between them;
  a clean stop/play cycle + single `Open()` call gave the correct 24).
- **Limitation, documented rather than hidden**: could not get a real rendered screenshot or drive
  an actual drag/click through the scrolled-off rows. `Time.frameCount` stayed at `1` for the entire
  session regardless of real elapsed time (confirmed via `Time.realtimeSinceStartup` advancing while
  `frameCount` didn't) — this is the pre-existing documented MCP Play-mode frame-stall
  ([[feedback_unity_mcp_ui_gotchas]]), not something caused by or fixable from this change. Per that
  memory's own guidance, didn't burn further time fighting it; relied on direct structural
  inspection (`execute_code` reading the real live `RectTransform`/`ScrollRect`/`RectMask2D` state)
  instead, which is sufficient to confirm the wiring is correct — `ScrollRect` with a correctly-sized
  content and a mask is a well-understood, low-risk uGUI pattern already used correctly elsewhere in
  Unity (first use in this project, but not a novel mechanism).
