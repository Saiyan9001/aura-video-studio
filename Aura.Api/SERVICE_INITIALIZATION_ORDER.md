# Service Initialization Order and Dependencies

This document describes the dependency relationships between services and the required initialization order in `Program.cs`.

## Initialization Phases

### Phase 1: Foundational Services (Lines 54-91)
**Order: CRITICAL - Must be first**

1. **Serilog Logger** - No dependencies
   - Required by: All services
   - Purpose: Logging infrastructure

2. **Controllers with JSON** - Depends on: Serilog
   - Required by: API endpoints
   - Purpose: Request handling

3. **FluentValidation** - No dependencies
   - Required by: Controllers
   - Purpose: Input validation

4. **Configuration Validator** - Depends on: Serilog, ProviderSettings
   - Required by: Startup validation
   - Purpose: Validates configuration on startup

### Phase 2: Infrastructure Services (Lines 76-106)
**Order: HIGH PRIORITY**

1. **Database Context** - Depends on: Configuration
   - Required by: Data access services
   - Purpose: SQLite database

2. **CORS** - No dependencies
   - Required by: Web UI
   - Purpose: Cross-origin requests

3. **HardwareDetector** - Depends on: Serilog
   - Required by: Video generation, provider selection
   - Purpose: System capability detection

4. **DiagnosticsHelper** - Depends on: Serilog
   - Required by: Health checks
   - Purpose: System diagnostics

5. **ProviderSettings** - Depends on: Configuration
   - Required by: All provider services
   - Purpose: Configuration management

6. **FFmpegLocator** - Depends on: Serilog, ProviderSettings
   - Required by: Video rendering, FFmpeg services
   - Purpose: FFmpeg path resolution

### Phase 3: HTTP and Provider Services (Lines 108-265)
**Order: MEDIUM PRIORITY**

1. **HttpClientFactory** - No dependencies
   - Required by: All HTTP-based providers
   - Purpose: HTTP client management

2. **LlmProviderFactory** - Depends on: HttpClient, Serilog
   - Required by: Script generation
   - Purpose: Creates LLM providers

3. **ProviderMixingConfig & ProviderMixer** - Depends on: LlmProviderFactory
   - Required by: Script orchestration
   - Purpose: Provider fallback logic

4. **ScriptOrchestrator** - Depends on: LlmProviderFactory, ProviderMixer
   - Required by: Script generation
   - Purpose: Script generation coordination

5. **TTS Providers** - Depends on: HttpClient, ProviderSettings
   - Required by: Audio generation
   - Purpose: Text-to-speech services
   - **Platform-dependent**: WindowsTtsProvider only on Windows

6. **Image Provider Factory** - Depends on: HttpClient
   - Required by: Visual generation
   - Purpose: Creates image providers

7. **Video Composer** - Depends on: FFmpegLocator
   - Required by: Video rendering
   - Purpose: FFmpeg video composition

### Phase 4: Domain Services (Lines 266-353)
**Order: NORMAL PRIORITY**

1. **Validators** - Depends on: Serilog
   - Required by: Generation pipeline
   - Purpose: Pre-generation validation

2. **Pipeline Reliability Services** - Depends on: Serilog
   - Required by: Generation orchestration
   - Purpose: Health monitoring, retries, cleanup

3. **Resource Management** - Depends on: Serilog, ProviderSettings
   - Required by: Video generation
   - Purpose: Disk space, temp file cleanup

4. **Smart Orchestration** - Depends on: HardwareDetector, Resource services
   - Required by: Video generation
   - Purpose: Resource-aware generation

5. **Timeline Services** - Depends on: ML models
   - Required by: Video orchestration
   - Purpose: Timeline building, pacing

6. **ML Models & Pacing Services** - Depends on: Serilog
   - Required by: Timeline optimization
   - Purpose: AI-powered pacing

7. **VideoOrchestrator** - Depends on: Timeline, Pacing, Image providers
   - Required by: Job execution
   - Purpose: Coordinates video generation

### Phase 5: Feature Services (Lines 354-714)
**Order: LOW PRIORITY - Can be lazy**

1. **Conversation Services** - Depends on: Serilog, ProviderSettings
   - Required by: Conversational features (optional)
   - Purpose: Context management

2. **Profile Services** - Depends on: Serilog, ProviderSettings
   - Required by: Profile management (optional)
   - Purpose: User preferences

3. **Learning Services** - Depends on: Serilog, ProviderSettings
   - Required by: AI learning features (optional)
   - Purpose: Pattern recognition, suggestions

4. **Content Services** - Depends on: LLM providers
   - Required by: Content analysis (optional)
   - Purpose: Content enhancement, analysis

5. **Analytics Services** - Depends on: Serilog
   - Required by: Performance tracking (optional)
   - Purpose: Video analytics, optimization

6. **Platform Services** - Depends on: Serilog
   - Required by: Platform optimization (optional)
   - Purpose: YouTube/TikTok optimization

7. **Quality Services** - Depends on: Serilog
   - Required by: Quality validation (optional)
   - Purpose: Quality checks, dashboards

8. **AI Editing Services** - Depends on: Serilog, FFmpeg
   - Required by: Advanced editing (optional)
   - Purpose: Scene detection, auto-framing

9. **Export Services** - Depends on: FFmpeg services
   - Required by: Export features (optional)
   - Purpose: Format conversion, optimization

### Phase 6: Job and Artifact Services (Lines 715-719)
**Order: CRITICAL - Near end**

1. **ArtifactManager** - Depends on: Serilog
   - Required by: JobRunner
   - Purpose: Artifact tracking

2. **JobRunner** - Depends on: ArtifactManager, VideoOrchestrator
   - Required by: Job endpoints
   - Purpose: Job execution coordination

3. **QuickService** - Depends on: JobRunner
   - Required by: Quick demo endpoint
   - Purpose: Quick video generation

### Phase 7: Dependency Management (Lines 487-656)
**Order: NORMAL - Can be parallel to features**

1. **DependencyManager** - Depends on: HttpClient, ProviderSettings
   - Required by: Dependency installation
   - Purpose: Component download/install

2. **Engine Services** - Depends on: HttpClient, ProviderSettings
   - Required by: External engine management
   - Purpose: Engine lifecycle, detection

3. **FFmpeg Installer** - Depends on: HttpClient, GitHubReleaseResolver
   - Required by: FFmpeg installation
   - Purpose: FFmpeg download/install

4. **ComponentDownloader** - Depends on: GitHubReleaseResolver, HttpDownloader
   - Required by: Component management
   - Purpose: Component downloads

5. **DependencyRescanService** - Depends on: FFmpegLocator, ComponentDownloader
   - Required by: Dependency detection
   - Purpose: Scans installed dependencies

### Phase 8: Health and Startup Services (Lines 476-485)
**Order: CRITICAL - Before hosted services**

1. **HealthCheckService** - Depends on: All services
   - Required by: Health endpoints
   - Purpose: Service health monitoring

2. **StartupValidator** - Depends on: FFmpegLocator, ProviderSettings
   - Required by: Startup validation
   - Purpose: Validates startup requirements

3. **FirstRunDiagnostics** - Depends on: Multiple services
   - Required by: First-run experience
   - Purpose: Comprehensive diagnostics

### Phase 9: Background Services (Lines 481-484)
**Order: MUST BE LAST in service registration**

1. **ProviderWarmupService** (IHostedService) - Depends on: Provider factories
   - Purpose: Warm up providers in background
   - **CRITICAL**: Uses IHostedService, runs after app starts

2. **HealthCheckBackgroundService** (IHostedService) - Depends on: ProviderHealthMonitor
   - Purpose: Periodic health checks
   - **CRITICAL**: Uses IHostedService, runs after app starts

### Post-Build Services (Lines 2968-3027)
**Order: AFTER app.Build()**

1. **EngineLifecycleManager** - Started via ApplicationStarted event
   - Purpose: Manages external engines
   - **Timing**: Starts when app is ready

2. **ProviderHealthMonitor** - Started via ApplicationStarted event
   - Purpose: Monitors provider health
   - **Timing**: Starts 2 seconds after EngineLifecycleManager

## Dependency Graph

```
Logger (Serilog)
├── Configuration
├── Database
├── HardwareDetector
│   ├── ResourceMonitor
│   └── StrategySelector
├── ProviderSettings
│   ├── FFmpegLocator
│   │   ├── VideoComposer
│   │   ├── FFmpegService
│   │   └── TimelineRenderer
│   ├── DependencyManager
│   ├── EngineServices
│   └── Feature Services (Profiles, Learning, etc.)
├── HttpClientFactory
│   ├── LlmProviderFactory
│   │   ├── ProviderMixer
│   │   └── ScriptOrchestrator
│   ├── TtsProviders
│   ├── ImageProviders
│   └── Dependency Downloaders
├── ML Models
│   └── Pacing Services
│       └── Timeline Services
│           └── VideoOrchestrator
│               └── JobRunner
│                   └── QuickService
└── Health Services
    ├── HealthCheckService
    ├── StartupValidator
    └── FirstRunDiagnostics
```

## Critical Rules

1. **Never** resolve services during registration - use Func<T> or lazy initialization
2. **Always** register IHostedService implementations last
3. **Test** service resolution in DiCoverageTests
4. **Document** when changing initialization order
5. **Validate** with StartupValidator before accepting requests

## Graceful Degradation

### Optional Services (Can fail without breaking app)
- Conversation services
- Profile services  
- Learning services
- Analytics services
- Platform optimization
- Quality validation
- AI Editing
- Content verification

### Critical Services (Must succeed for app to function)
- Logger
- Database
- HardwareDetector
- ProviderSettings
- FFmpegLocator
- VideoComposer
- JobRunner

## Testing Initialization Order

Run `DiCoverageTests` to verify all services can be resolved:
```bash
dotnet test --filter "FullyQualifiedName~DiCoverageTests"
```

## Troubleshooting

### Circular Dependencies
If you encounter circular dependencies:
1. Use `Func<T>` or `Lazy<T>` for deferred resolution
2. Consider extracting common dependencies to a new service
3. Review if both services truly need each other

### Startup Failures
If services fail to initialize:
1. Check logs for "Initialization Phase" messages
2. Look for exception stack traces showing which service failed
3. Verify configuration in appsettings.json
4. Ensure all required directories exist and are writable

### Provider Initialization Errors
Providers should **never** be resolved during startup:
1. Use provider factories that return `Func<T>`
2. Defer provider creation until first use
3. Implement fallback logic for unavailable providers
