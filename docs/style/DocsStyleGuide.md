# Documentation Style Guide

**Version**: 1.0  
**Last Updated**: 2025-11-03  
**Status**: Active

This guide establishes standards for all documentation in the Aura Video Studio repository to ensure consistency, clarity, and maintainability.

## Table of Contents

- [General Principles](#general-principles)
- [File Organization](#file-organization)
- [Naming Conventions](#naming-conventions)
- [Document Structure](#document-structure)
- [Writing Style](#writing-style)
- [Terminology](#terminology)
- [Formatting](#formatting)
- [Code Examples](#code-examples)
- [Links and References](#links-and-references)
- [Images and Media](#images-and-media)
- [Markdown Guidelines](#markdown-guidelines)
- [Review Process](#review-process)

## General Principles

### Audience-First

- **Know your reader**: Write for beginners in getting-started guides, experts in developer docs
- **Be clear**: Use simple language; explain jargon on first use
- **Be concise**: Respect reader's time; get to the point
- **Be accurate**: Test all instructions; verify all facts

### Production-Ready

- **No placeholders**: Never commit TODO, FIXME, HACK, or WIP comments in documentation
- **Complete content**: Every document should be fully written and current
- **Create issues**: For future work, create GitHub issues and reference them in docs if needed

### Maintainability

- **Single source of truth**: Avoid duplication; link to canonical docs
- **Clear ownership**: Each major doc should have a maintainer
- **Living documents**: Update docs when features change; archive when outdated

## File Organization

### Directory Structure

```
docs/
├── getting-started/     # Installation, first run, quick start
├── features/            # Feature documentation
├── workflows/           # Common workflows and how-tos
├── user-guide/          # End-user guides
├── developer/           # Developer documentation
├── api/                 # API reference
├── architecture/        # Architecture and design docs
├── troubleshooting/     # Problem-solving guides
├── security/            # Security documentation
├── best-practices/      # Best practices and patterns
├── style/               # Documentation style guide (this file)
├── archive/             # Historical documents
└── assets/              # Images, diagrams, media
```

### Project-Specific Docs

- **Aura.Api/README.md**: API-specific setup and endpoints
- **Aura.Web/README.md**: Frontend setup and architecture
- **Aura.Core/**: Domain-specific docs (AI adapters, ML models)
- **Aura.Cli/README.md**: CLI usage and commands

## Naming Conventions

### File Names

- **Use UPPER_SNAKE_CASE or Title_Case** for standalone docs in root
- **Use kebab-case** for docs in subdirectories
- **Be descriptive**: `USER_CUSTOMIZATION_GUIDE.md` not `GUIDE.md`
- **Use conventional suffixes**:
  - `_GUIDE.md` for user guides
  - `_API.md` for API documentation
  - `README.md` for directory indexes
  - `_CHECKLIST.md` for checklists

### Examples

✅ **Good**:
- `FIRST_RUN_GUIDE.md`
- `docs/getting-started/quick-start.md`
- `docs/api/jobs.md`
- `PROVIDER_INTEGRATION_GUIDE.md`

❌ **Avoid**:
- `guide.md` (too generic)
- `docs/GettingStarted.md` (inconsistent with directory convention)
- `PR13_IMPLEMENTATION_SUMMARY.md` in root (belongs in archive)

## Document Structure

### Standard Sections

Every standalone guide should include:

```markdown
# Document Title

Brief one-paragraph overview (what is this document about?)

## Table of Contents (optional for long docs)

## Prerequisites / Before You Start

## Main Content Sections

## Related Resources

## Troubleshooting (if applicable)

## Next Steps / Further Reading
```

### README Files

README files in directories should:
- List and briefly describe each document in the directory
- Provide navigation to key docs
- Explain the purpose of the directory

## Writing Style

### Voice and Tone

- **Active voice**: "Click the button" not "The button should be clicked"
- **Present tense**: "The system validates" not "The system will validate"
- **Second person for instructions**: "You can configure" not "One can configure"
- **Professional but approachable**: Clear and friendly without being casual

### Sentence Structure

- **Keep sentences short**: Aim for 15-20 words
- **One idea per sentence**: Break complex ideas into multiple sentences
- **Parallel structure**: In lists, use consistent grammatical structure

### Examples

✅ **Good**:
```markdown
To enable Advanced Mode:
1. Open Settings
2. Click the Advanced tab
3. Toggle "Advanced Mode" on
4. Restart the application
```

❌ **Avoid**:
```markdown
You can enable Advanced Mode by opening Settings, and then you should click on
the Advanced tab, after which you'll need to toggle the "Advanced Mode" switch 
to the on position, and finally restart the application for changes to take effect.
```

## Terminology

### Product Names

- **Aura Video Studio**: Full product name (use on first mention in doc)
- **Aura**: Short form (acceptable after full name)
- **The application**: Generic reference

### Feature Names (Capitalize)

- **Advanced Mode**: Not "advanced mode"
- **First Run Wizard**: Not "first run wizard"
- **ML Lab**: Not "ML lab" or "ml lab"
- **Path Selector**: Not "path selector"
- **Download Center**: Not "download center"
- **Quick Demo**: Not "quick demo"

### Provider Profiles

- **Free-Only**: Uses only free providers
- **Balanced Mix**: Mix of free and premium providers
- **Pro-Max**: Premium providers end-to-end

### Components and Terms

- **API key**: Not "apiKey" or "API-key"
- **Text-to-speech** or **TTS**: Both acceptable; define TTS on first use
- **Large Language Model** or **LLM**: Define LLM on first use
- **Frontend**: Not "front-end" or "front end"
- **Backend**: Not "back-end" or "back end"
- **Markdown**: Not "markdown" or "MARKDOWN"
- **GitHub**: Not "Github"
- **FFmpeg**: Not "ffmpeg" or "FFMPEG"
- **Server-Sent Events** or **SSE**: Define SSE on first use

### Technical Terms

- **Endpoint**: REST API endpoint (e.g., `/api/jobs`)
- **Route**: Frontend route (e.g., `/settings`)
- **Provider**: External service integration (OpenAI, ElevenLabs, etc.)
- **Engine**: Video generation engine
- **Pipeline**: Video generation pipeline
- **Orchestrator**: Pipeline orchestration component

## Formatting

### Headings

- **Use ATX-style headings**: `# Heading` not `Heading\n=======`
- **One H1 per document**: The title
- **Hierarchical structure**: Don't skip levels (H1 → H2 → H3)
- **Descriptive headings**: "Install Dependencies" not "Installation"

### Lists

- **Unordered lists**: Use `-` not `*` or `+`
- **Ordered lists**: Use `1.` for all items (auto-numbering)
- **Consistent punctuation**: End items with periods if they're complete sentences
- **Indentation**: Use 2 spaces for nested lists

### Emphasis

- **Bold (`**text**`)**: For UI elements, important terms, warnings
- **Italic (`*text*`)**: For emphasis, book titles, first use of terms
- **Code (`` `text` ``)**: For code, commands, file names, values

### Examples

```markdown
## Configuration

To configure the application:

1. Open **Settings** → **Advanced**
2. Set `apiKey` to your OpenAI key
3. Choose a *provider profile* (Free-Only, Balanced Mix, or Pro-Max)
4. Click **Save**

**Note**: Changes require a restart.
```

## Code Examples

### Code Blocks

Always specify the language for syntax highlighting:

````markdown
```typescript
const config: AppConfig = {
  mode: 'advanced',
  provider: 'openai'
};
```
````

### Supported Languages

- `typescript`, `javascript`, `tsx`, `jsx`
- `csharp`, `cs`
- `json`, `yaml`, `xml`
- `bash`, `sh`, `powershell`
- `sql`, `dockerfile`

### Inline Code

Use backticks for:
- File names: `appsettings.json`
- Variable names: `apiKey`
- Command names: `npm install`
- Code values: `true`, `false`, `null`

### Command Examples

Show the command and expected output:

```bash
# Check Node version
$ node --version
v18.18.0

# Install dependencies
$ npm ci
```

Use `$` for user commands, no prefix for output.

### Configuration Examples

Show complete, valid examples:

```json
{
  "providers": {
    "llm": {
      "primary": "openai",
      "fallback": "gemini"
    }
  }
}
```

## Links and References

### Internal Links

Use relative paths:

```markdown
See the [Getting Started Guide](docs/getting-started/QUICK_START.md) for details.

See [Provider Integration](PROVIDER_INTEGRATION_GUIDE.md) (same directory).

See [API Reference](../api/jobs.md) (parent directory).
```

### External Links

Use descriptive link text with URLs:

```markdown
✅ Good: See the [OpenAI API documentation](https://platform.openai.com/docs) for details.

❌ Avoid: See https://platform.openai.com/docs for details.
❌ Avoid: See the documentation [here](https://platform.openai.com/docs).
```

### Link Format

- **Use Markdown links**: `[text](url)` not raw URLs
- **Descriptive text**: Link text should describe the destination
- **Valid targets**: Ensure all links resolve correctly

### Cross-References

When referencing other docs:

```markdown
For more information, see:
- [Build Guide](BUILD_GUIDE.md) - Setup and build instructions
- [API Contract](docs/api/API_CONTRACT_V1.md) - API reference
- [Troubleshooting](docs/troubleshooting/Troubleshooting.md) - Common issues
```

## Images and Media

### Image Files

- **Location**: Store in `docs/assets/` or subdirectory
- **Naming**: Use kebab-case: `quick-demo-button.png`
- **Format**: Use PNG for screenshots, SVG for diagrams
- **Size**: Optimize images; keep under 500KB when possible

### Image References

```markdown
![Quick Demo button location](assets/quick-demo-button.png)

*Figure 1: Quick Demo button in the main interface*
```

Always provide:
- Alt text describing the image
- Optional caption below the image

### Diagrams

- Use Mermaid for diagrams when possible
- Store source files (`.mmd`) alongside rendered images
- Version diagrams with the code they represent

## Markdown Guidelines

### CommonMark Compliance

Follow [CommonMark specification](https://commonmark.org/):
- Use blank lines between blocks
- Close all markup properly
- Escape special characters when needed

### markdownlint Rules

The repository uses markdownlint with these exceptions:
- **MD013** (line length): Disabled - no hard limit
- **MD033** (HTML): Allowed for advanced formatting
- **MD041** (first line heading): Disabled for archived docs with banners

### Accessibility

- **Alt text**: Always provide for images
- **Heading hierarchy**: Don't skip levels
- **Descriptive links**: Avoid "click here"
- **Tables**: Include headers

### Tables

```markdown
| Feature | Free-Only | Balanced | Pro-Max |
|---------|-----------|----------|---------|
| LLM | Rule-based | Gemini | GPT-4 |
| TTS | SAPI | Piper | ElevenLabs |
| Cost | $0 | Low | High |
```

Align columns for readability in source.

## Review Process

### Before Committing

- [ ] Run `npm run lint` (includes markdownlint)
- [ ] Check all links work
- [ ] Verify code examples are valid
- [ ] Spell-check the document
- [ ] Test any instructions provided

### Pull Request Checklist

When updating docs:
- [ ] Update related docs if terminology/features changed
- [ ] Fix any broken links
- [ ] Update table of contents if structure changed
- [ ] Add entry to DocsIndex.md if new canonical doc
- [ ] Request review from doc owner or maintainer

### Archived Documents

When archiving:
- Add banner at top with link to current docs
- Move to `docs/archive/`
- Update links in other docs
- Note in commit message what replaces it

## Enforcement

### CI Checks

Documentation changes must pass:
1. **markdownlint**: Markdown style and structure
2. **Link checker**: Internal and external links
3. **DocFX build**: Documentation site builds successfully
4. **Spell check**: No obvious typos (warnings only)

### Quality Gates

- No placeholders (TODO, FIXME, etc.) allowed in committed docs
- All internal links must resolve
- All code examples must be syntactically valid
- PR template requires "Docs updated" checkbox

## Maintenance

This style guide is a living document. To suggest changes:

1. Open an issue describing the proposed change
2. Discuss with maintainers
3. Update this guide via PR
4. Update existing docs to match (if feasible)

### Version History

- **1.0** (2025-11-03): Initial version

## Questions?

For questions about documentation:
- Open an issue with the `documentation` label
- Tag `@docs-team` in discussions
- See [Contributing Guide](../../CONTRIBUTING.md)

---

*This style guide follows its own recommendations. If you find inconsistencies, please report them.*
