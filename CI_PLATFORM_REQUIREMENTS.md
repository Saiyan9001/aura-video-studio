# CI Platform Requirements and Build Configuration

## Overview

This document explains the platform-specific build requirements for Aura Video Studio and how CI workflows handle cross-platform compatibility.

## Project Platform Compatibility

### Cross-Platform Projects (Linux + Windows)

The following projects can be built on both Linux and Windows:

- **Aura.Core** - Core business logic and services
- **Aura.Providers** - LLM, TTS, and image provider implementations
- **Aura.Api** - ASP.NET Core REST API
- **Aura.Cli** - Command-line interface
- **Aura.Analyzers** - Roslyn code analyzers
- **Aura.Tests** - Unit and integration tests
- **Aura.E2E** - End-to-end test suite
- **Aura.Web** - React/Vite frontend (Node.js-based)

### Windows-Only Projects

- **Aura.App** - WinUI 3 desktop application
  - Target framework: `net8.0-windows10.0.19041.0`
  - Uses Windows App SDK and WinUI 3
  - Requires Windows-specific XAML compiler
  - **Cannot be built on Linux runners**

## CI Workflow Configuration

### Linux-Based Workflows

The following workflows run on `ubuntu-latest` and explicitly exclude `Aura.App`:

#### `.github/workflows/ci.yml`
- **Runner**: `ubuntu-latest`
- **Build Strategy**: Individual project builds (excludes Aura.App)
- **Projects Built**:
  ```bash
  dotnet build Aura.Core/Aura.Core.csproj
  dotnet build Aura.Providers/Aura.Providers.csproj
  dotnet build Aura.Api/Aura.Api.csproj
  dotnet build Aura.Cli/Aura.Cli.csproj
  dotnet build Aura.Analyzers/Aura.Analyzers.csproj
  dotnet build Aura.Tests/Aura.Tests.csproj
  dotnet build Aura.E2E/Aura.E2E.csproj
  ```

#### `.github/workflows/ci-linux.yml`
- **Runner**: `ubuntu-latest`
- **Build Strategy**: Same as ci.yml (excludes Aura.App)
- **Additional Steps**: Includes coverage collection and Playwright E2E tests

### Windows-Based Workflows

The following workflows run on `windows-latest` and can build all projects including `Aura.App`:

#### `.github/workflows/build-validation.yml`
- **Runner**: `windows-latest`
- **Build Strategy**: Full solution build (`dotnet build Aura.sln`)
- **Projects Built**: All projects including Aura.App

#### `.github/workflows/ci-windows.yml`
- **Runner**: `windows-latest`
- **Build Strategy**: Full solution build
- **Projects Built**: All projects including Aura.App

## Why This Matters

### The Problem (Before Fix)

Prior to this fix, Linux-based CI workflows attempted to build the entire solution using:
```bash
dotnet build --configuration Release --no-restore
```

This would fail with:
```
error MSB3073: The command "XamlCompiler.exe" exited with code 126
```

This happened because:
1. The solution (`Aura.sln`) includes `Aura.App`
2. `Aura.App` requires Windows App SDK XAML compiler
3. XAML compiler is a Windows-only .exe file
4. Linux cannot execute Windows .exe files

### The Solution

Linux workflows now explicitly build each compatible project individually, skipping `Aura.App`. This:
- ✅ Allows CI to complete successfully on Linux runners
- ✅ Provides platform coverage for cross-platform code
- ✅ Reduces CI costs (Linux runners are cheaper than Windows)
- ✅ Validates that core functionality works on Linux servers
- ✅ Still builds Aura.App on Windows runners for validation

## Build Commands

### For Linux Development/CI

```bash
# Build all Linux-compatible projects
dotnet build Aura.Core/Aura.Core.csproj --configuration Release
dotnet build Aura.Providers/Aura.Providers.csproj --configuration Release
dotnet build Aura.Api/Aura.Api.csproj --configuration Release
dotnet build Aura.Cli/Aura.Cli.csproj --configuration Release
dotnet build Aura.Analyzers/Aura.Analyzers.csproj --configuration Release
dotnet build Aura.Tests/Aura.Tests.csproj --configuration Release
dotnet build Aura.E2E/Aura.E2E.csproj --configuration Release
```

### For Windows Development/CI

```bash
# Build entire solution (includes Aura.App)
dotnet build Aura.sln --configuration Release
```

## Testing Strategy

### Linux CI
- Runs unit tests from `Aura.Tests`
- Runs E2E tests that don't require the WinUI app
- Validates API and CLI functionality

### Windows CI
- Runs full test suite
- Validates WinUI app builds correctly
- Tests Windows-specific features

## Troubleshooting

### Error: "XamlCompiler.exe exited with code 126" on Linux
**Cause**: Attempting to build Aura.App on a Linux system

**Solution**: Exclude Aura.App from the build:
```bash
# Don't do this on Linux:
dotnet build Aura.sln

# Do this instead:
dotnet build Aura.Core/Aura.Core.csproj
dotnet build Aura.Api/Aura.Api.csproj
# ... (other compatible projects)
```

### Error: "The specified framework 'net8.0-windows10.0.19041.0' could not be found" on Linux
**Cause**: Same as above - trying to build Windows-specific project on Linux

**Solution**: Use the project-specific build commands shown above

## Related Documentation

- [BUILD_GUIDE.md](BUILD_GUIDE.md) - General build instructions
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contribution guidelines
- [.github/workflows/](/.github/workflows/) - CI workflow definitions

## CI Workflow Decision Matrix

| Workflow | Runner | Builds Aura.App? | Primary Purpose |
|----------|--------|------------------|-----------------|
| ci.yml | ubuntu-latest | ❌ No | Fast cross-platform validation |
| ci-linux.yml | ubuntu-latest | ❌ No | Comprehensive Linux testing |
| ci-windows.yml | windows-latest | ✅ Yes | Windows-specific validation |
| build-validation.yml | windows-latest | ✅ Yes | Strict build quality checks |
| comprehensive-ci.yml | ubuntu-latest | ❌ No | Full pipeline testing |
| e2e-pipeline.yml | Mixed | Varies | E2E scenarios |

## Future Considerations

If additional Windows-only projects are added to the solution:
1. Add them to the exclusion list in Linux CI workflows
2. Update this document
3. Ensure Windows CI workflows continue to build them
4. Consider creating a `Aura.CrossPlatform.sln` for Linux builds
