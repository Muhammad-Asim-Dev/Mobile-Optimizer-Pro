# Milestone 4.5 — Production Test Checklist

## Clean import

- [ ] Delete the previous `Assets/MobilePerformanceOptimizer` folder before importing
- [ ] Import into a clean Unity 6 URP project
- [ ] 0 compile errors caused by MPO
- [ ] 0 obsolete API warnings caused by MPO
- [ ] Open `Tools > Mobile Performance Optimizer`
- [ ] Midnight Amber theme renders correctly at narrow and wide window sizes

## Scan scopes

### Full Project
- [ ] scans supported project assets
- [ ] scans currently loaded scene content
- [ ] scan remains responsive while clicking/scrolling

### Build Scenes
- [ ] only enabled Build Settings scenes contribute dependencies
- [ ] does not open/close/save scenes automatically
- [ ] loaded build scenes receive scene-component checks
- [ ] no-loaded-build-scene state is explained and does not create misleading empty scene-category scores

### Current Scene
- [ ] active saved scene scans scene content + dependencies
- [ ] unsaved active scene handles component analysis without throwing

### Selected Folder
- [ ] invalid folder blocks scan with a clear message
- [ ] selected Assets folder scans supported assets recursively
- [ ] unrelated scene/project-setting analyzers are not run

### Selected Assets
- [ ] no Project selection blocks scan with a clear message
- [ ] one selected asset scans correctly
- [ ] multiple selected assets scan correctly
- [ ] selected folder through Project selection resolves child assets

## Problems UX

- [ ] category chips switch instantly
- [ ] All category chip restores all categories
- [ ] More Filters opens advanced severity/sort controls
- [ ] grouped problems are the default view
- [ ] Open Group shows all underlying affected items
- [ ] individual list stays responsive with 1,000+ findings
- [ ] problem detail starts with Why it matters / What you should do
- [ ] Technical details are collapsed by default
- [ ] Ping Asset works where an asset path exists
- [ ] Ignore actions refresh result counts correctly

## Fixes

- [ ] Fix All Safe Issues is disabled/redirected when no safe fixes exist
- [ ] Fix All Safe Issues includes Safe actions only
- [ ] duplicate safe actions are not applied twice
- [ ] Review Required actions are excluded from Fix All Safe
- [ ] Manual findings are never modified automatically
- [ ] Review All opens the batch preview
- [ ] individual safe fix can be applied
- [ ] individual review fix opens preview before applying
- [ ] Revert Last Session restores captured values
- [ ] failed fixes do not prevent remaining safe fixes from being attempted

## Resilience

- [ ] broken shader does not terminate remaining analyzers
- [ ] unreadable texture/importer is skipped and recorded
- [ ] missing component/reference does not stop scan
- [ ] compiling scripts pauses/resumes safely
- [ ] asset import/update pauses/resumes safely
- [ ] Cancel Scan preserves partial result state without exception

## Reports / history

- [ ] HTML export succeeds
- [ ] CSV export succeeds
- [ ] JSON export succeeds
- [ ] report includes Scan Scope
- [ ] scan history includes Scan Scope
- [ ] before/after compares only matching Platform + Device Tier + Scan Scope
- [ ] stale results warning appears after target/scope changes until re-scan

## Large-project UX

- [ ] scrolling stays smooth with 5,000+ findings
- [ ] search/category changes do not repaint thousands of item cards
- [ ] no repeating Editor freeze during cooperative scanning
- [ ] Diagnostics list is usable with many skipped assets
