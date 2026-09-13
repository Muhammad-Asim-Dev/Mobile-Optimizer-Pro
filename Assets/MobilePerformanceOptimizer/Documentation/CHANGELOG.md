# Changelog

## 1.0.1

- Fixed Problems-page controls overlapping results when category-wide custom values are expanded
- Added height-aware responsive layout for short/docked Unity Editor windows
- Category batch values now use a bounded internal scroll region instead of growing without limit
- Problems page now preserves normal document flow with a scrollable outer page and bounded virtualized result panes
- No analyzer, fix, report, or scan behavior changed in this UI-only hotfix

## 1.0.0

- Finalized focused Asset Store workflow
- Added responsive Deep Ocean production UI
- Fixed dynamic category-value layout/anchoring by using retained UI Toolkit controls
- Category tabs now appear before the category fix panel for a clearer workflow
- Category-specific Fix All with preview and incremental Apply
- Category-wide Recommended/Custom values for compatible settings
- Texture Android/iOS importer override handling and post-Apply verification
- Material GPU Instancing verification on supported workflows
- Mesh/model, audio, URP, quality and build-setting fix workflows
- Automatic refresh of the original scan scope after Apply
- Revert support for supported last-session changes
- Scan scopes: Full Project, Build Scenes, Current Scene, Selected Folder, Selected Assets
- HTML, JSON and CSV reports
- Resilient scanning that skips recoverable broken assets instead of stopping the complete scan
- Incremental scanning/batching and virtualized lists for large projects
- Added final user documentation and troubleshooting guide
