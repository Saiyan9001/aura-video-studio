# Visual Provider Implementation - UI Components

## Step 4: Visuals Configuration (CreateView)

### Visual Mode Selection
- **Stock**: Uses only free stock image providers (Pexels, Pixabay, Unsplash)
- **StockOrLocal**: Stock with local Stable Diffusion fallback (NVIDIA GPU required)
- **Pro**: Professional cloud providers (Stability AI, Runway)

### Stock Provider Weights
Three sliders to control distribution across providers:
- Pexels Weight: 0-100%
- Pixabay Weight: 0-100%
- Unsplash Weight: 0-100%
- Must sum to 100% for balanced selection

### Stable Diffusion Parameters
Only active in StockOrLocal mode:

**Model Selection:**
- Auto: Selects based on VRAM (SDXL for 12GB+, SD1.5 for 6-12GB)
- SDXL: High-quality, requires 12GB+ VRAM
- SD15: Faster, requires 6GB+ VRAM

**Generation Parameters:**
- Steps: 5-50 (20-30 recommended, quality vs speed)
- CFG Scale: 1-20 (7-9 recommended, prompt adherence)
- Seed: Integer or -1 for random (reproducibility)
- Size: Auto or manual (1024x576, 576x1024, 1024x1024)
- Style: cinematic, photographic, artistic, abstract

**NVIDIA Gate:**
InfoBar displays warning about hardware requirements:
- NVIDIA GPU required for local generation
- Minimum 6GB VRAM for SD1.5
- Minimum 12GB VRAM for SDXL

## Scene Inspector (StoryboardView)

### Per-Scene Visual Overrides
Located in right panel of timeline view:

**Visual Provider Override:**
- Use Global Setting (default)
- Stock Images (force stock for this scene)
- Local SD (force SD generation for this scene)
- Solid Color (use solid background)

**Custom Keywords:**
- Text input for scene-specific search/generation terms
- Overrides global visual style
- Example: "sunset, mountains, nature"

**Style Override:**
- Use Global Setting (default)
- Cinematic
- Photographic
- Artistic
- Abstract

**Stable Diffusion Overrides:**
- Steps slider: 5-50
- CFG Scale slider: 1-20
- Seed override: -1 or specific integer

**Actions:**
- Apply Overrides button (AccentButtonStyle)
- Clear Overrides button (removes all scene-specific settings)

**InfoBar Explanation:**
Displays guidance that scene-specific settings override global configuration,
and that "Use Global Setting" inherits from main visual config.

## Key UI Features

### Tooltips and Gates
- All controls include descriptive tooltips
- NVIDIA gate prominently displayed in InfoBar
- Clear explanations of VRAM requirements
- Style guidance for recommended values

### Responsive Layout
- CreateView: Single column layout with expandable sections
- StoryboardView: Split view with 2:1 ratio (timeline:inspector)
- Inspector panel has minimum width of 300px
- All panels scroll independently

### Validation and Feedback
- Stock weights should sum to 100% (UI guidance provided)
- SD parameters have recommended ranges in tooltips
- InfoBar messages explain hardware requirements
- Per-scene overrides clearly indicate inheritance

## Implementation Notes

### CreateViewModel Properties
Added to existing CreateViewModel:
- VisualMode: "Stock" | "StockOrLocal" | "Pro"
- Stock provider weights (3 integers, 0-100)
- SD parameters (model, steps, cfg, seed, size, style)
- All properties use ObservableProperty for binding

### StoryboardView Layout
- Converted from placeholder to functional inspector
- Left side: Timeline editor (placeholder remains)
- Right side: Scene inspector with override controls
- Uses Grid with 2:1 column ratio
- Inspector panel fully functional for visual overrides

### Accessibility
- All controls properly labeled with Header attributes
- ToolTip guidance on every interactive element
- InfoBar messages for important warnings
- Clear visual hierarchy with proper contrast
