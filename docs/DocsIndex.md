# Documentation Index

**Last Updated**: 2025-11-03  
**Purpose**: Central index of all canonical documentation in Aura Video Studio

This document provides a map of all current, authoritative documentation. For historical documents, see [docs/archive/](archive/).

## Quick Links

- [Getting Started](#getting-started)
- [User Guides](#user-guides)
- [Features](#features)
- [Workflows](#workflows)
- [Developer Documentation](#developer-documentation)
- [API Reference](#api-reference)
- [Architecture](#architecture)
- [Operations](#operations)
- [Contributing](#contributing)

## Getting Started

Start here if you're new to Aura Video Studio.

| Document | Description | Audience | Maintainer |
|----------|-------------|----------|------------|
| [README.md](../README.md) | Project overview and quick start | Everyone | Core Team |
| [FIRST_RUN_GUIDE.md](../FIRST_RUN_GUIDE.md) | First-time setup walkthrough | End Users | Core Team |
| [BUILD_GUIDE.md](../BUILD_GUIDE.md) | Build from source instructions | Developers | Core Team |
| [Quick Start](getting-started/QUICK_START.md) | 5-minute getting started guide | End Users | Docs Team |
| [Installation Guide](getting-started/INSTALLATION.md) | Detailed installation instructions | End Users | Docs Team |
| [First Run FAQ](getting-started/FIRST_RUN_FAQ.md) | Common first-run questions | End Users | Docs Team |

## User Guides

End-user documentation for using Aura Video Studio features.

### Core Workflows

| Document | Description | Related Features |
|----------|-------------|------------------|
| [USER_CUSTOMIZATION_GUIDE.md](../USER_CUSTOMIZATION_GUIDE.md) | Configure preferences and settings | Settings, Profiles |
| [TRANSLATION_USER_GUIDE.md](../TRANSLATION_USER_GUIDE.md) | Multi-language video creation | Translation, Localization |
| [PROMPT_CUSTOMIZATION_USER_GUIDE.md](../PROMPT_CUSTOMIZATION_USER_GUIDE.md) | Customize LLM prompts | Advanced Mode, Prompts |
| [SCRIPT_REFINEMENT_GUIDE.md](../SCRIPT_REFINEMENT_GUIDE.md) | Improve generated scripts | Script Generation |

### Content Creation

| Document | Description | Related Features |
|----------|-------------|------------------|
| [CONTENT_ADAPTATION_GUIDE.md](../CONTENT_ADAPTATION_GUIDE.md) | Adapt content for audiences | Audience Profiles |
| [DOCUMENT_IMPORT_GUIDE.md](../DOCUMENT_IMPORT_GUIDE.md) | Import existing documents | Document Import |
| [CONTENT_SAFETY_GUIDE.md](../CONTENT_SAFETY_GUIDE.md) | Content safety and moderation | Safety, Compliance |
| [TRANSLATION_SAMPLES.md](../TRANSLATION_SAMPLES.md) | Translation examples and samples | Translation |

### Advanced Features

| Document | Description | Requires |
|----------|-------------|----------|
| [user-guide/ASSET_LIBRARY_GUIDE.md](user-guide/ASSET_LIBRARY_GUIDE.md) | Manage media assets | Advanced Mode |
| [user-guide/TIMELINE_EDITOR_UI_GUIDE.md](user-guide/TIMELINE_EDITOR_UI_GUIDE.md) | Timeline editing features | Advanced Mode |
| [user-guide/PACING_OPTIMIZATION_GUIDE.md](user-guide/PACING_OPTIMIZATION_GUIDE.md) | Optimize video pacing | Advanced Mode |
| [user-guide/CONTENT_VERIFICATION_GUIDE.md](user-guide/CONTENT_VERIFICATION_GUIDE.md) | Verify generated content | Advanced Mode |
| [user-guide/AI_LEARNING_SYSTEM_GUIDE.md](user-guide/AI_LEARNING_SYSTEM_GUIDE.md) | ML Lab and model training | Advanced Mode |

### Visual Guides

| Document | Description | Audience |
|----------|-------------|----------|
| [LOADING_STATES_VISUAL_GUIDE.md](../LOADING_STATES_VISUAL_GUIDE.md) | Loading states and progress UI | End Users |
| [PATH_SELECTOR_VISUAL_GUIDE.md](../PATH_SELECTOR_VISUAL_GUIDE.md) | Path Selector component usage | End Users |
| [VISUAL_IMPACT_EXAMPLES.md](../VISUAL_IMPACT_EXAMPLES.md) | Visual design examples | Designers, Users |
| [user-guide/VISUAL_WAVEFORMS_THUMBNAILS_GUIDE.md](user-guide/VISUAL_WAVEFORMS_THUMBNAILS_GUIDE.md) | Waveforms and thumbnails | Advanced Mode |
| [user-guide/UI_IMPROVEMENTS_VISUAL_GUIDE.md](user-guide/UI_IMPROVEMENTS_VISUAL_GUIDE.md) | UI features overview | End Users |

## Features

Feature-specific documentation.

| Document | Description | Status |
|----------|-------------|--------|
| [features/ENGINES.md](features/ENGINES.md) | Video generation engines overview | Current |
| [features/ENGINES_SD.md](features/ENGINES_SD.md) | Stable Diffusion integration | Current |
| [features/TIMELINE.md](features/TIMELINE.md) | Timeline editor features | Current |
| [features/TTS-and-Captions.md](features/TTS-and-Captions.md) | Text-to-speech and captions | Current |
| [features/TTS_LOCAL.md](features/TTS_LOCAL.md) | Local TTS providers (Piper, etc.) | Current |
| [features/CLI.md](features/CLI.md) | Command-line interface | Current |

## Workflows

Common workflows and how-to guides.

| Document | Description | Complexity |
|----------|-------------|------------|
| [workflows/QUICK_DEMO.md](workflows/QUICK_DEMO.md) | Quick Demo workflow | Beginner |
| [workflows/PORTABLE_MODE_GUIDE.md](workflows/PORTABLE_MODE_GUIDE.md) | Portable/offline mode | Intermediate |
| [workflows/SETTINGS_SCHEMA.md](workflows/SETTINGS_SCHEMA.md) | Settings configuration reference | Advanced |
| [workflows/UX_GUIDE.md](workflows/UX_GUIDE.md) | UX patterns and guidelines | Developer |
| [user-guide/LOCAL_PROVIDERS_SETUP.md](user-guide/LOCAL_PROVIDERS_SETUP.md) | Setup local/offline providers | Intermediate |
| [user-guide/PORTABLE.md](user-guide/PORTABLE.md) | Portable distribution | End Users |

## Developer Documentation

Documentation for contributors and developers.

### Getting Started (Developers)

| Document | Description | Audience |
|----------|-------------|----------|
| [developer/README.md](developer/README.md) | Developer documentation index | Developers |
| [developer/BUILD_GUIDE.md](developer/BUILD_GUIDE.md) | Build system overview | Developers |
| [developer/BUILD_AND_RUN.md](developer/BUILD_AND_RUN.md) | Build and run locally | Developers |
| [developer/DEPLOYMENT.md](developer/DEPLOYMENT.md) | Deployment procedures | DevOps |
| [developer/INSTALL.md](developer/INSTALL.md) | Development environment setup | Developers |

### Architecture and Design

| Document | Description | Area |
|----------|-------------|------|
| [architecture/ARCHITECTURE.md](architecture/ARCHITECTURE.md) | System architecture overview | All |
| [architecture/PROVIDER_SELECTION_ARCHITECTURE.md](architecture/PROVIDER_SELECTION_ARCHITECTURE.md) | Provider selection system | Backend |
| [architecture/SSE_EVENT_FLOW.md](architecture/SSE_EVENT_FLOW.md) | Server-Sent Events flow | Backend |
| [architecture/SERVICE_INITIALIZATION_ORDER.md](architecture/SERVICE_INITIALIZATION_ORDER.md) | Service startup sequence | Backend |
| [architecture/WIZARD_STATE_MACHINE_DIAGRAM.md](architecture/WIZARD_STATE_MACHINE_DIAGRAM.md) | First Run Wizard state machine | Frontend |

### Implementation Guides

| Document | Description | Technology |
|----------|-------------|------------|
| [LLM_IMPLEMENTATION_GUIDE.md](../LLM_IMPLEMENTATION_GUIDE.md) | LLM integration patterns | LLMs, AI |
| [PROVIDER_INTEGRATION_GUIDE.md](../PROVIDER_INTEGRATION_GUIDE.md) | Add new providers | Providers |
| [PROMPT_ENGINEERING_API.md](../PROMPT_ENGINEERING_API.md) | Prompt engineering API | LLMs |
| [SSE_INTEGRATION_TESTING_GUIDE.md](../SSE_INTEGRATION_TESTING_GUIDE.md) | Test SSE endpoints | Backend |
| [LLM_LATENCY_MANAGEMENT.md](../LLM_LATENCY_MANAGEMENT.md) | Manage LLM latency | Performance |

### Frontend Development

| Document | Description | Area |
|----------|-------------|------|
| [Aura.Web/README.md](../Aura.Web/README.md) | Frontend architecture | React, TypeScript |
| [Aura.Web/TESTING.md](../Aura.Web/TESTING.md) | Frontend testing guide | Testing |
| [Aura.Web/ICON_SYSTEM_GUIDE.md](../Aura.Web/ICON_SYSTEM_GUIDE.md) | Icon system usage | UI |
| [Aura.Web/KEYBOARD_SHORTCUTS_GUIDE.md](../Aura.Web/KEYBOARD_SHORTCUTS_GUIDE.md) | Keyboard shortcuts | UX |
| [Aura.Web/UNDO_REDO_GUIDE.md](../Aura.Web/UNDO_REDO_GUIDE.md) | Undo/redo system | State Management |
| [Aura.Web/TIMELINE_FEATURES.md](../Aura.Web/TIMELINE_FEATURES.md) | Timeline implementation | Video Editing |
| [Aura.Web/CHROMA_KEY_COMPOSITING.md](../Aura.Web/CHROMA_KEY_COMPOSITING.md) | Chroma key features | Advanced Mode |
| [Aura.Web/UNDO_REDO_VISUAL_GUIDE.md](../Aura.Web/UNDO_REDO_VISUAL_GUIDE.md) | Undo/redo visual reference | UI |

### Backend Development

| Document | Description | Area |
|----------|-------------|------|
| [Aura.Api/README.md](../Aura.Api/README.md) | Backend API overview | .NET, ASP.NET Core |
| [api/README.md](api/README.md) | API documentation index | REST API |
| [api/API_CONTRACT_V1.md](api/API_CONTRACT_V1.md) | API contract v1 | REST API |
| [api/jobs.md](api/jobs.md) | Jobs API | REST API |
| [api/providers.md](api/providers.md) | Providers API | REST API |
| [api/health.md](api/health.md) | Health check endpoints | Monitoring |
| [api/errors.md](api/errors.md) | Error handling | REST API |

### Testing

| Document | Description | Type |
|----------|-------------|------|
| [TESTING.md](TESTING.md) | Testing strategy overview | All |
| [Aura.Web/TESTING.md](../Aura.Web/TESTING.md) | Frontend testing | Frontend |
| [Aura.E2E/README.md](../Aura.E2E/README.md) | End-to-end tests | E2E |
| [INTEGRATION_TESTING_GUIDE.md](INTEGRATION_TESTING_GUIDE.md) | Integration testing | Backend |

## API Reference

REST API documentation.

| Document | Description | Version |
|----------|-------------|---------|
| [api/API_CONTRACT_V1.md](api/API_CONTRACT_V1.md) | Complete API v1 reference | v1 |
| [api/jobs.md](api/jobs.md) | Job management endpoints | v1 |
| [api/providers.md](api/providers.md) | Provider endpoints | v1 |
| [api/health.md](api/health.md) | Health and diagnostics | v1 |
| [api/errors.md](api/errors.md) | Error responses | v1 |
| [api/rate-limits.md](api/rate-limits.md) | Rate limiting | v1 |

## Architecture

System design and architecture documents.

| Document | Description | Scope |
|----------|-------------|-------|
| [architecture/ARCHITECTURE.md](architecture/ARCHITECTURE.md) | High-level architecture | System |
| [architecture/PROVIDER_SELECTION_ARCHITECTURE.md](architecture/PROVIDER_SELECTION_ARCHITECTURE.md) | Provider selection design | Providers |
| [architecture/SSE_EVENT_FLOW.md](architecture/SSE_EVENT_FLOW.md) | Server-Sent Events design | Real-time |
| [architecture/SERVICE_INITIALIZATION_ORDER.md](architecture/SERVICE_INITIALIZATION_ORDER.md) | Service initialization | Backend |
| [architecture/WIZARD_STATE_MACHINE_DIAGRAM.md](architecture/WIZARD_STATE_MACHINE_DIAGRAM.md) | Wizard state machine | Frontend |
| [architecture/ERROR_FLOW_DIAGRAM.md](architecture/ERROR_FLOW_DIAGRAM.md) | Error handling flow | System |
| [architecture/FFMPEG_SINGLE_LOCATOR_FLOW.md](architecture/FFMPEG_SINGLE_LOCATOR_FLOW.md) | FFmpeg detection flow | Video |

## Operations

Operational documentation for running and maintaining Aura Video Studio.

### Runbooks and Playbooks

| Document | Description | Audience |
|----------|-------------|----------|
| [OncallRunbook.md](../OncallRunbook.md) | On-call procedures | DevOps, SRE |
| [ReleasePlaybook.md](../ReleasePlaybook.md) | Release procedures | Release Team |
| [ORCHESTRATION_RUNBOOK.md](ORCHESTRATION_RUNBOOK.md) | Pipeline orchestration ops | DevOps |

### Production and Deployment

| Document | Description | Audience |
|----------|-------------|----------|
| [PRODUCTION_READINESS_CHECKLIST.md](../PRODUCTION_READINESS_CHECKLIST.md) | Pre-production checklist | All Teams |
| [Aura.Web/PRODUCTION_DEPLOYMENT.md](../Aura.Web/PRODUCTION_DEPLOYMENT.md) | Frontend deployment | DevOps |
| [Aura.Web/WINDOWS_SETUP.md](../Aura.Web/WINDOWS_SETUP.md) | Windows-specific setup | Users, DevOps |
| [PORTABLE.md](../PORTABLE.md) | Portable distribution | Release Team |

### Monitoring and Diagnostics

| Document | Description | Area |
|----------|-------------|------|
| [user-guide/PIPELINE_VALIDATION_GUIDE.md](user-guide/PIPELINE_VALIDATION_GUIDE.md) | Pipeline validation | Monitoring |
| [user-guide/DEPENDENCY_RESCAN_UI_GUIDE.md](user-guide/DEPENDENCY_RESCAN_UI_GUIDE.md) | Dependency diagnostics | Troubleshooting |
| [user-guide/DOWNLOAD_CENTER.md](user-guide/DOWNLOAD_CENTER.md) | Download Center usage | User Support |

### Configuration

| Document | Description | Scope |
|----------|-------------|-------|
| [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md) | Configuration reference | System |
| [FFmpeg_Setup_Guide.md](FFmpeg_Setup_Guide.md) | FFmpeg installation | System |
| [OLLAMA_MODEL_SELECTION.md](../OLLAMA_MODEL_SELECTION.md) | Ollama model selection | LLMs |
| [SERVICE_INITIALIZATION.md](../SERVICE_INITIALIZATION.md) | Service configuration | Backend |

## Troubleshooting

Problem-solving guides and FAQs.

| Document | Description | Audience |
|----------|-------------|----------|
| [troubleshooting/Troubleshooting.md](troubleshooting/Troubleshooting.md) | Common issues and solutions | End Users |
| [troubleshooting/README.md](troubleshooting/README.md) | Troubleshooting index | End Users |
| [TROUBLESHOOTING_INTEGRATION_TESTS.md](TROUBLESHOOTING_INTEGRATION_TESTS.md) | Integration test issues | Developers |
| [getting-started/FIRST_RUN_FAQ.md](getting-started/FIRST_RUN_FAQ.md) | First run FAQ | End Users |

## Contributing

Documentation for contributors.

| Document | Description | Audience |
|----------|-------------|----------|
| [CONTRIBUTING.md](../CONTRIBUTING.md) | Contribution guidelines | Contributors |
| [SECURITY.md](../SECURITY.md) | Security policy | Contributors, Security |
| [style/DocsStyleGuide.md](style/DocsStyleGuide.md) | Documentation style guide | Doc Authors |
| [ZERO_PLACEHOLDER_POLICY.md](../ZERO_PLACEHOLDER_POLICY.md) | Zero placeholder policy | Developers |
| [SPACING_CONVENTIONS.md](../SPACING_CONVENTIONS.md) | Code spacing conventions | Developers |

## Specialized Topics

### Security

| Document | Description | Audience |
|----------|-------------|----------|
| [SECURITY.md](../SECURITY.md) | Security policy | All |
| [security/README.md](security/README.md) | Security documentation index | Security Team |
| [CONTENT_SAFETY_GUIDE.md](../CONTENT_SAFETY_GUIDE.md) | Content safety | Compliance |

### Performance

| Document | Description | Audience |
|----------|-------------|----------|
| [PERFORMANCE_BENCHMARKS.md](PERFORMANCE_BENCHMARKS.md) | Performance benchmarks | Developers |
| [LLM_LATENCY_MANAGEMENT.md](../LLM_LATENCY_MANAGEMENT.md) | LLM latency optimization | Developers |

### CI/CD

| Document | Description | Audience |
|----------|-------------|----------|
| [CI.md](CI.md) | CI/CD documentation | DevOps |
| [DEPENDENCIES.md](DEPENDENCIES.md) | Dependency management | Developers |
| [VERIFICATION.md](VERIFICATION.md) | Verification procedures | QA |

## Best Practices

| Document | Description | Area |
|----------|-------------|------|
| [best-practices/README.md](best-practices/README.md) | Best practices index | All |
| [TYPESCRIPT_GUIDELINES.md](TYPESCRIPT_GUIDELINES.md) | TypeScript guidelines | Frontend |

## How to Use This Index

### Finding Documentation

1. **By topic**: Use the categorized sections above
2. **By audience**: Look for "Audience" columns
3. **By status**: Check "Status" to ensure doc is current
4. **By search**: Use browser search (Ctrl+F / Cmd+F)

### Document Status

- **Current**: Up-to-date with latest code
- **Active**: Maintained and accurate
- **Deprecated**: Superseded; see replacement link

### Suggesting Changes

To suggest updates to this index:

1. Open an issue with tag `documentation`
2. Submit a PR updating this file
3. Ensure all new docs follow [DocsStyleGuide.md](style/DocsStyleGuide.md)

## Archived Documentation

Historical implementation notes, PR summaries, and deprecated guides are in [docs/archive/](archive/).

These documents are retained for historical context but should not be used as current guidance.

## Maintenance

This index is maintained by the Docs Team. Last review: 2025-11-03.

To report missing or incorrect entries, open an issue with the `documentation` label.

---

**Need help?** See [CONTRIBUTING.md](../CONTRIBUTING.md) or open an issue.
