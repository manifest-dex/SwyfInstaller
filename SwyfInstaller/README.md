# SwyfInstaller

See the [root README](../README.md) — it is the complete manual for the
installer app, the backend, and the mod.

- App: basic WPF window calling `SwyfInstaller/` backend directly (no
  commands or flags). Game folder auto-detect on startup (registry roots +
  `libraryfolders.vdf`, Element-style), Install / Update / Uninstall,
  silent self-updates, copyable details log.
- Console: parameter-free interactive menu fallback
  (`install`, `update`, `verify`, `uninstall`, game-folder setup) plus a
  `selftest` build check.
- Update scans this repo's releases for the latest verified
  `SWYF-Custom-AI-*-win-x64.zip`, keeping settings, session and backups.
