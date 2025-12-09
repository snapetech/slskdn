# Memory Bank Template

A persistent context system for AI-assisted development. Copy this to any project to give AI models memory across sessions.

## Quick Start

```bash
# Copy to your project
cp -r memory-bank/ /path/to/your/project/
cp -r .cursor/ /path/to/your/project/
cp AGENTS.md /path/to/your/project/

# Then customize:
# 1. Edit memory-bank/projectbrief.md with your project details
# 2. Update .cursor/rules/*.mdc with your conventions
# 3. Start working - AI will read these files automatically
```

## What's Included

```
memory-bank/
├── projectbrief.md      # Project overview (customize first)
├── tasks.md             # Task tracking
├── activeContext.md     # Current session state
├── progress.md          # Work log
├── scratch.md           # Quick reference & notes
└── decisions/
    ├── adr-0000-template.md
    ├── adr-0001-known-gotchas.md
    └── ...

.cursor/rules/
├── memory-bank.mdc      # How AI should use memory bank
└── conventions.mdc      # Your coding conventions

AGENTS.md                # Top-level AI instructions
```

## Core Principles

1. **Save results often** - Commit after every meaningful change
2. **Document bugs immediately** - If you fix it, write it down
3. **Grep before you write** - Search existing code first
4. **Keep it simple** - No unnecessary abstractions

## Customization

After copying, update these files for your project:

| File | What to Change |
|------|----------------|
| `projectbrief.md` | Project name, tech stack, architecture |
| `tasks.md` | Your actual tasks |
| `conventions.mdc` | Your language/framework conventions |
| `AGENTS.md` | Project-specific AI rules |

## License

Public domain. Use however you want.
