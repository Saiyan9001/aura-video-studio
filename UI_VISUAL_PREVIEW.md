# Visual Provider Implementation - UI Preview

## CreateView - Step 4: Visuals Configuration

```
╔═══════════════════════════════════════════════════════════════════════╗
║ Step 4: Visuals                                              [Expanded] ║
╠═══════════════════════════════════════════════════════════════════════╣
║                                                                       ║
║ Visual Mode                                                           ║
║ ┌─────────────────────────────────────────────────────────────────┐ ║
║ │ Stock                                                       ▼   │ ║
║ └─────────────────────────────────────────────────────────────────┘ ║
║ Options: Stock / StockOrLocal / Pro                                  ║
║                                                                       ║
║ ─────────────────────────────────────────────────────────────────── ║
║ Stock Provider Weights (must sum to 100%)                            ║
║                                                                       ║
║ Pexels: 33%          Pixabay: 33%          Unsplash: 34%            ║
║ ├──────●─────┤      ├──────●─────┤       ├──────●──────┤           ║
║ 0          100       0          100       0           100            ║
║                                                                       ║
║ ─────────────────────────────────────────────────────────────────── ║
║ Stable Diffusion Parameters (StockOrLocal mode only)                 ║
║                                                                       ║
║ ┌─────────────────────────────────────────────────────────────────┐ ║
║ │ ℹ️ NVIDIA GPU Required                                           │ ║
║ │ Local Stable Diffusion requires an NVIDIA GPU with at least     │ ║
║ │ 6GB VRAM. SDXL model requires 12GB+.                            │ ║
║ └─────────────────────────────────────────────────────────────────┘ ║
║                                                                       ║
║ Model                        Size                                    ║
║ ┌─────────────────────────┐ ┌─────────────────────────┐            ║
║ │ Auto               ▼   │ │ Auto               ▼   │            ║
║ └─────────────────────────┘ └─────────────────────────┘            ║
║                                                                       ║
║ Steps: 20                    CFG Scale: 7.0                          ║
║ ├────────●────────┤         ├────────●────────┤                    ║
║ 5               50          1                20                     ║
║                                                                       ║
║ Seed                         Style                                   ║
║ ┌─────────────────────────┐ ┌─────────────────────────┐            ║
║ │ -1                      │ │ cinematic          ▼   │            ║
║ └─────────────────────────┘ └─────────────────────────┘            ║
║                                                                       ║
╚═══════════════════════════════════════════════════════════════════════╝
```

## StoryboardView - Scene Inspector Panel

```
╔═══════════════════════════╦═══════════════════════════════════════════╗
║                           ║                                           ║
║                           ║ Scene Inspector                           ║
║                           ║ ───────────────────────────────────────── ║
║                           ║                                           ║
║                           ║ Per-Scene Visual Overrides                ║
║                           ║                                           ║
║   Timeline Area           ║ Visual Provider                           ║
║   (Placeholder)           ║ ┌───────────────────────────────────────┐ ║
║                           ║ │ Use Global Setting            ▼      │ ║
║                           ║ └───────────────────────────────────────┘ ║
║   ┌─────────────────┐     ║                                           ║
║   │                 │     ║ Custom Keywords                           ║
║   │                 │     ║ ┌───────────────────────────────────────┐ ║
║   │   🎬 Timeline   │     ║ │ e.g., sunset, mountains, nature       │ ║
║   │     Editor      │     ║ └───────────────────────────────────────┘ ║
║   │                 │     ║                                           ║
║   │  Coming Soon    │     ║ Style Override                            ║
║   │                 │     ║ ┌───────────────────────────────────────┐ ║
║   └─────────────────┘     ║ │ Use Global Setting            ▼      │ ║
║                           ║ └───────────────────────────────────────┘ ║
║                           ║                                           ║
║                           ║ Stable Diffusion Overrides                ║
║                           ║                                           ║
║                           ║ Steps: 20                                 ║
║                           ║ ├────────●────────┤                      ║
║                           ║ 5               50                       ║
║                           ║                                           ║
║                           ║ CFG Scale: 7.0                            ║
║                           ║ ├────────●────────┤                      ║
║                           ║ 1                20                      ║
║                           ║                                           ║
║                           ║ Seed Override                             ║
║                           ║ ┌───────────────────────────────────────┐ ║
║                           ║ │ -1 for random                         │ ║
║                           ║ └───────────────────────────────────────┘ ║
║                           ║                                           ║
║                           ║ ┌───────────────────────────────────────┐ ║
║                           ║ │      Apply Overrides      ✓           │ ║
║                           ║ └───────────────────────────────────────┘ ║
║                           ║ ┌───────────────────────────────────────┐ ║
║                           ║ │      Clear Overrides                  │ ║
║                           ║ └───────────────────────────────────────┘ ║
║                           ║                                           ║
║                           ║ ┌───────────────────────────────────────┐ ║
║                           ║ │ ℹ️ Scene Overrides                    │ ║
║                           ║ │ Scene-specific settings override      │ ║
║                           ║ │ global visual configuration. Leave    │ ║
║                           ║ │ as 'Use Global Setting' to inherit.   │ ║
║                           ║ └───────────────────────────────────────┘ ║
║                           ║                                           ║
╠═══════════════════════════╩═══════════════════════════════════════════╣
║ Toolbar:                                                              ║
║ ▶ Play  ⏹ Stop  │  ✂ Split  ✄ Trim  │  + Add  ⚡ Transition │ 🔍± Zoom ║
╚═══════════════════════════════════════════════════════════════════════╝
```

## Key Features Demonstrated

### 1. Visual Mode Selection
- **Stock**: Default free mode using stock image APIs
- **StockOrLocal**: Hybrid mode with NVIDIA GPU gate for SD
- **Pro**: Professional cloud providers (requires API keys)

### 2. Stock Provider Weights
- Three independent sliders (0-100% each)
- Balances distribution across Pexels, Pixabay, Unsplash
- Real-time percentage display
- Tooltips explain each provider

### 3. NVIDIA GPU Gate
- Prominent InfoBar warning in CreateView
- Clear requirements: NVIDIA GPU, 6GB+ VRAM
- SDXL-specific requirement: 12GB+ VRAM
- Visible before user invests time configuring SD

### 4. Stable Diffusion Parameters
- **Model**: Auto-selects based on VRAM (SDXL/SD1.5)
- **Steps**: 5-50 range, 20-30 recommended (tooltip)
- **CFG Scale**: 1-20 range, 7-9 recommended (tooltip)
- **Seed**: Integer for reproducibility, -1 for random
- **Size**: Auto matches aspect ratio, manual override available
- **Style**: Preset styles (cinematic, photographic, artistic, abstract)

### 5. Per-Scene Inspector
- **Provider Override**: Force specific provider for scene
- **Custom Keywords**: Scene-specific search terms
- **Style Override**: Per-scene visual style
- **SD Overrides**: Full SD parameter control per scene
- **Actions**: Apply/Clear with clear visual feedback
- **Guidance**: InfoBar explains inheritance model

## Tooltips Present

Every control has a tooltip explaining:
- **Visual Mode**: "Choose visual generation strategy"
- **Provider Weights**: "Weight for [Provider] stock images"
- **Model**: "Auto selects based on VRAM (SDXL for 12GB+, SD1.5 for 6-12GB)"
- **Steps**: "Quality vs speed (20-30 recommended)"
- **CFG Scale**: "Prompt adherence (7-9 recommended)"
- **Seed**: "Set for reproducible results, -1 for random"
- **Size**: "Image dimensions (Auto matches aspect ratio)"
- **Style**: "Visual style for generated images"

## Gating Explanations

### CreateView InfoBar
```
┌─────────────────────────────────────────────────────────────────┐
│ ℹ️ NVIDIA GPU Required                                          │
│ Local Stable Diffusion requires an NVIDIA GPU with at least    │
│ 6GB VRAM. SDXL model requires 12GB+.                           │
└─────────────────────────────────────────────────────────────────┘
```

### StoryboardView InfoBar
```
┌─────────────────────────────────────────────────────────────────┐
│ ℹ️ Scene Overrides                                              │
│ Scene-specific settings override global visual configuration.  │
│ Leave as 'Use Global Setting' to inherit.                      │
└─────────────────────────────────────────────────────────────────┘
```

## Accessibility Features

1. **Semantic Labels**: All controls have descriptive Header attributes
2. **Tooltips**: Comprehensive guidance on every interactive element
3. **InfoBars**: Important warnings and information prominently displayed
4. **Contrast**: Proper theme-aware styling using ThemeResource
5. **Keyboard Navigation**: Full support via WinUI 3 controls
6. **Screen Reader**: Proper XAML structure with labels and descriptions

## Responsive Design

- **CreateView**: Single column, scrollable, expandable sections
- **StoryboardView**: 2:1 split (timeline:inspector), minimum 300px inspector
- **Independent Scrolling**: Each panel scrolls separately
- **Adaptive Width**: Controls stretch to available space
- **Touch Support**: Sliders and buttons sized for touch input
