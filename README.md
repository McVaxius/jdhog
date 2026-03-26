# Jabberdhoggy

Dalamud plugin workspace for `Jabberdhoggy`.

## Current Status

Bootstrap scaffold created on 2026-03-25. This repo now has a buildable `Debug x64` shell with command routing, Ko-fi placement, DTR support, icon assets, and repo-ready documentation.

- Solution: `Z:\jdhog\jdhog.sln`
- Project: `Z:\jdhog\jdhog\jdhog.csproj`
- Command: `/jdhog`
- Repository target: `Public`

## Plugin Concept

- Separate account and character policy.
- Keep inference offline-first and bounded.
- Require explicit permission for emotes and commands.

## Planned Services

- ConfigManager
- OfflineModelHost
- ConversationStateService
- OutboundActionPolicy
- IChatEngine

## Documents

- Project plan: `Z:\xa-xiv-docs\Dhog\jdhog\JDHOG_PROJECT_PLAN.md`
- Knowledge base: `Z:\xa-xiv-docs\Dhog\jdhog\JDHOG_KNOWLEDGE_BASE.md`
- Import guide: `how to import plugins.md`
- Changelog: `CHANGELOG.md`

## Notes

- Icon assets live in `images\iconHQ.png` and `images\icon.png`.
- SamplePlugin references used for the initial shell: https://github.com/goatcorp/SamplePlugin and https://github.com/goatcorp/SamplePlugin/blob/master/README.md
