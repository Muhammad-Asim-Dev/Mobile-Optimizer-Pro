# Mobile Performance Optimizer

Version **1.0.0**

Mobile Performance Optimizer is a focused Unity 6 Editor tool for reviewing and fixing common Android/iOS project settings that frequently affect mobile memory, rendering cost, loading and build configuration.

It is intentionally **not** a replacement for Unity Profiler. Use it to clean up project/import settings, then validate representative gameplay on real target devices.

## Supported environment

- Unity 6.x
- Universal Render Pipeline (URP)
- Android and iOS targets
- Editor-only package; no runtime dependency is added to builds

## Core categories

- **Textures** — mobile max size, Read/Write, mipmaps and compatible custom values
- **Materials** — GPU Instancing on supported material/shader workflows
- **Meshes / Models** — Read/Write and compatible Mesh Compression values
- **Audio** — streaming/preload and compatible importer values
- **URP** — mobile-relevant pipeline settings detected by the active profile
- **Quality** — MSAA, shadow distance, LOD bias and other detected quality settings
- **Build Settings** — Development Build/debug/profiler flags

## Main workflow

**Scan → Choose Category → Recommended or Custom Values → Preview → Apply → Verify → Re-scan**

There is no global cross-category Fix All. Batch changes are category-specific so developers can understand and control what is changing.

## Scan scopes

- Full Project
- Build Scenes
- Current Scene
- Selected Folder
- Selected Assets

## Reports

The Reports page exports the current scan to **HTML**, **JSON** or **CSV**.

## Safety

Supported fixes capture restorable values, apply through Unity APIs, save/reimport when required, verify the value after application, and refresh the original scan scope. Destructive content changes and shader conversion remain manual.

Start with **QUICK_START.md**.
