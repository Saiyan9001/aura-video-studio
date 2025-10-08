# Troubleshooting Guide

## Enum Compatibility

### Overview
The Aura Video Studio API supports both canonical enum values and legacy aliases for backward compatibility. This ensures that older client code continues to work while new code uses standardized naming.

### Supported Enum Aliases

#### Density Enum
The `Density` enum controls how much content is packed per minute in the generated video.

| Canonical Value | Legacy Alias | Description |
|----------------|--------------|-------------|
| `Sparse`       | -            | Light content, more breathing room |
| `Balanced`     | `Normal`     | Standard content density (recommended) |
| `Dense`        | -            | Maximum content per minute |

**Example:**
```json
// Both of these are valid:
{ "density": "Balanced" }
{ "density": "Normal" }  // Automatically converted to "Balanced"
```

#### Aspect Ratio Enum
The `Aspect` enum specifies the video dimensions.

| Canonical Value    | Legacy Alias | Description |
|-------------------|--------------|-------------|
| `Widescreen16x9`  | `16:9`       | Standard widescreen (1920x1080) |
| `Vertical9x16`    | `9:16`       | Vertical/mobile format (1080x1920) |
| `Square1x1`       | `1:1`        | Square format (1080x1080) |

**Example:**
```json
// Both of these are valid:
{ "aspect": "Widescreen16x9" }
{ "aspect": "16:9" }  // Automatically converted to "Widescreen16x9"
```

### Error Handling

#### E303: Invalid Enum Value
When an unrecognized enum value is provided, the API returns an RFC7807 ProblemDetails response:

```json
{
  "type": "https://docs.aura.studio/errors/E303",
  "title": "Invalid Enum Value",
  "status": 400,
  "detail": "Invalid Density value: 'Medium'. Valid values are: 'Sparse', 'Balanced', 'Dense' (or alias 'Normal' for Balanced)."
}
```

**Common causes:**
- Typo in enum value
- Using an unsupported legacy value
- Case mismatch (though the API is case-insensitive)

**Resolution:**
1. Check the error detail for the list of valid values
2. Update your request to use a canonical or supported alias value
3. If using the Web UI, values are automatically normalized

### Client-Side Normalization (Web UI)

The TypeScript client automatically normalizes enum values before sending requests:

```typescript
import { normalizeDensity, normalizeAspect } from './types';

// Automatically converts legacy values to canonical
const density = normalizeDensity('Normal'); // Returns 'Balanced'
const aspect = normalizeAspect('16:9');     // Returns 'Widescreen16x9'
```

**Console warnings:**
The normalization functions emit non-blocking warnings when legacy values are detected:
```
⚠️ Legacy density value "Normal" detected. Use "Balanced" instead. 
   Automatically normalizing to "Balanced".
```

These warnings help developers migrate to canonical values but don't block functionality.

### Best Practices

1. **Use canonical values in new code** - While aliases are supported, prefer canonical values for clarity
2. **Handle E303 errors gracefully** - Display the error detail to users to help them correct invalid inputs
3. **Monitor console warnings** - Use browser console warnings to identify legacy value usage during development
4. **Case-insensitive** - All enum values are case-insensitive, but prefer PascalCase for consistency

### Migration Guide

If you're updating old code that uses legacy values:

**Before:**
```typescript
const request = {
  density: 'Normal',  // Legacy
  aspect: '16:9'      // Legacy
};
```

**After:**
```typescript
const request = {
  density: 'Balanced',      // Canonical
  aspect: 'Widescreen16x9'  // Canonical
};
```

**Note:** Your old code will continue to work thanks to the tolerant converters, but updating to canonical values is recommended for long-term maintainability.

### Testing Enum Values

**Using curl:**
```bash
# Test with canonical values
curl -X POST http://localhost:5005/api/script \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "Test",
    "aspect": "Widescreen16x9",
    "density": "Balanced",
    "targetDurationMinutes": 3,
    "pacing": "Conversational",
    "tone": "Informative",
    "language": "en-US",
    "audience": "General",
    "goal": "Inform",
    "style": "Standard"
  }'

# Test with legacy aliases (also works)
curl -X POST http://localhost:5005/api/script \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "Test",
    "aspect": "16:9",
    "density": "Normal",
    "targetDurationMinutes": 3,
    "pacing": "Conversational",
    "tone": "Informative",
    "language": "en-US",
    "audience": "General",
    "goal": "Inform",
    "style": "Standard"
  }'
```

### Related Error Codes

- **E300**: Script provider failed
- **E301**: Request timeout
- **E302**: Empty script result
- **E303**: Invalid enum value (this section)
- **E304**: Invalid plan parameters

For more details on error codes, see the [Error Reference](ErrorReference.md).
