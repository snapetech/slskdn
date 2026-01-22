# Frontend Migration Plan

## Current State

The frontend uses:
- **react-scripts** v5.0.0 (Create React App)
- **craco** v7.0.0 for configuration overrides
- **React** v16.8.6 (quite outdated)
- **Semantic UI React** v2.1.0
- **react-router-dom** v5.0.0

## Why Migrate?

1. **react-scripts is effectively unmaintained** - CRA is no longer actively developed
2. **Security vulnerabilities** - Older transitive dependencies have known issues
3. **Build performance** - Modern bundlers (Vite, esbuild) are significantly faster
4. **React 16 is EOL** - Missing modern features (concurrent mode, transitions, etc.)

## Recommended Migration Path

### Phase 1: Update React (Low Risk)
1. Update React 16 → React 18
2. Update react-router-dom 5 → 6 (significant API changes)
3. Test all components thoroughly

### Phase 2: Replace CRA with Vite (Medium Risk)
1. Install Vite + @vitejs/plugin-react
2. Migrate craco.config.js → vite.config.js
3. Update Semantic UI LESS compilation setup
4. Update build scripts in package.json
5. Update CI/CD pipeline

### Phase 3: Component Library Evaluation (Future)
- Semantic UI React is aging; consider migration to:
  - Mantine (React-focused, modern)
  - Radix UI + Tailwind (headless + utility CSS)
  - Keep Semantic UI if it works well enough

## Migration Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Semantic UI LESS compilation | High | Test build early in migration |
| react-router v6 API changes | Medium | Review all route definitions |
| SignalR integration | Medium | Test WebSocket connections |
| Build output differences | Low | Compare bundle sizes |

## Timeline Estimate

- Phase 1: 2-3 days (mostly testing)
- Phase 2: 3-5 days (build system changes)
- Phase 3: TBD (depends on if needed)

## Dependencies to Update

```
react: ^16.8.6 → ^18.2.0
react-dom: ^16.8.6 → ^18.2.0
react-router-dom: ^5.0.0 → ^6.x
react-scripts: ^5.0.0 → REMOVE
@craco/craco: ^7.0.0 → REMOVE
```

## Notes

- This is a documentation-only plan for now
- No actual migration work should begin until existing features are stable
- Consider feature-flagging new UI components during transition

