# Documentation Cleanup Plan

## Status: Ready to Execute

After completing task validation and updating checkboxes, we need to:

1. **Archive completed planning docs** (merge/graduation plans are done)
2. **Update outdated branch references** (experimental/whatAmIThinking → master)
3. **Remove outdated "experimental" language** for stable features
4. **Consolidate duplicate planning docs**

---

## Files to Archive

### Completed Merge/Graduation Plans (Move to docs/archive/planning/)
- `GRADUATION_TO_MAIN_PLAN.md` - ✅ All tasks complete
- `MERGE_TO_MAIN_EXECUTION_PLAN.md` - ✅ All tasks complete  
- `MERGE_TO_MAIN_QUICK_START.md` - ✅ All tasks complete
- `MERGE_EXECUTION_PLAN.md` - Older version, superseded
- `NEXT_STEPS.md` - References old branch, merge complete

### Keep (Still Relevant)
- [GRADUATION_STATUS.md](GRADUATION_STATUS.md) - Current status tracking

---

## Files Needing Updates

### Branch Reference Updates (experimental/whatAmIThinking → master)
- `README.md` - Update any remaining branch references
- [FEATURES.md](../FEATURES.md) - Update branch references
- `memory-bank/tasks.md` - Update branch references
- `memory-bank/activeContext.md` - Update branch references
- `memory-bank/progress.md` - Update branch references
- `docs/security/*.md` - Check for outdated references
- [SERVICE_FABRIC_TASKS.md](../SERVICE_FABRIC_TASKS.md) - Check for outdated references

### Language Updates (experimental → stable)
- Remove "experimental" markers from stable features
- Change "planned" → "implemented" where appropriate
- Update "coming soon" → "available" for released features

---

## Execution Steps

1. Create archive directory structure
2. Move completed planning docs to archive
3. Update branch references across all docs
4. Update language (experimental → stable)
5. Verify all links still work
6. Update any cross-references
