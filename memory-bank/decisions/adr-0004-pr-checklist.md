# ADR-0004: Pre-Commit Checklist

> **Status**: Active  
> **Date**: 2025-12-08  
> **Purpose**: Validation checklist before submitting code

Run through this checklist before committing. If any check fails, fix it.

---

## Backend (C#)

### 1. No New Abstractions
```bash
# Check for factory/wrapper patterns you might have added
grep -rn "Factory\|Wrapper\|Builder\|Provider" src/slskd/YourNewCode/
```
If found: Remove them. Use DI directly.

### 2. No Swallowed Exceptions
```bash
# Check for catch blocks that return null/default
grep -A2 "catch.*Exception" src/slskd/YourNewCode/
```
If found: Let exceptions propagate unless you're actually handling them.

### 3. No Logging Spam
```bash
# Check for entry/exit logging
grep -n "Entering\|Exiting\|Starting\|Finished" src/slskd/YourNewCode/
```
If found: Remove it. Log meaningful events only.

### 4. Async Event Handlers Have Try-Catch
```bash
# Find async void handlers
grep -B2 -A5 "async void" src/slskd/YourNewCode/
```
If found without try-catch: Add it. This is critical.

### 5. DI Registration Added
```bash
# Check your service is registered
grep "YourService" src/slskd/Program.cs
```
If not found: Add registration in `ConfigureDependencyInjectionContainer()`.

### 6. Copyright Header Correct
```bash
# Check header
head -5 src/slskd/YourNewCode/*.cs
```
- New slskdN files: `company="slskdN Team"`
- Modified upstream: Keep original `company="slskd Team"`

### 7. No Hardcoded Values
```bash
# Check for magic numbers/strings
grep -n "\"http\|:5030\|localhost" src/slskd/YourNewCode/
```
If found: Use Options or constants.

---

## Frontend (React/JSX)

### 8. API Functions Return Safe Values
```bash
# Check your lib functions
grep -A5 "export const" src/web/src/lib/yourFeature.js
```
Must return `[]` not `undefined` for array endpoints.

### 9. Error Handling Uses Toast
```bash
# Check error handling
grep -A3 "catch.*error" src/web/src/components/YourComponent/
```
Must use: `toast.error(error?.response?.data ?? error?.message ?? error)`

### 10. No Class Components
```bash
# Check for class syntax
grep "class.*extends\|React.Component" src/web/src/components/YourComponent/
```
If found: Convert to function component with hooks.

### 11. No PropTypes/TypeScript
```bash
# Check for type annotations
grep "PropTypes\|: string\|: number\|interface\|type " src/web/src/components/YourComponent/
```
If found: Remove them. This is plain JS.

### 12. No React 17+ Features
```bash
# Check for new features
grep "useId\|useDeferredValue\|useTransition\|startTransition" src/web/src/components/YourComponent/
```
If found: Remove. We're on React 16.8.6.

---

## Tests

### 13. Tests Pass
```bash
dotnet test
```
Must pass.

### 14. No Flaky Tests
Check for:
- Random values in assertions
- Time-dependent assertions
- Order-dependent tests

Use `[InlineAutoData]` with fixed values for edge cases.

---

## Build

### 15. Lint Passes
```bash
./bin/lint
cd src/web && npm run lint
```
Must pass.

### 16. Build Succeeds
```bash
dotnet build
cd src/web && npm run build
```
Must succeed.

---

## Quick Validation Script

```bash
#!/bin/bash
# Save as: validate.sh

echo "=== Checking for common issues ==="

# Backend checks
echo "Checking for factories/wrappers..."
grep -rn "Factory\|Wrapper" src/slskd/ --include="*.cs" | grep -v "FTP\|DbContext" && echo "WARNING: Found factory/wrapper patterns"

echo "Checking for async void without try-catch..."
grep -B1 -A10 "async void" src/slskd/ --include="*.cs" | grep -v "try" && echo "WARNING: Found async void without try-catch"

# Frontend checks
echo "Checking for undefined returns..."
grep -rn "return response" src/web/src/lib/ | grep -v "return \[\]" && echo "WARNING: Possible undefined return"

echo "Checking for class components..."
grep -rn "extends.*Component" src/web/src/components/ && echo "WARNING: Found class component"

echo "=== Running tests ==="
dotnet test --verbosity quiet

echo "=== Running lint ==="
./bin/lint

echo "=== Done ==="
```

---

## Summary

Before every commit:

1. ✅ No factories/wrappers/builders
2. ✅ No swallowed exceptions
3. ✅ No logging spam
4. ✅ Async void has try-catch
5. ✅ DI registered
6. ✅ Copyright header correct
7. ✅ No hardcoded values
8. ✅ API functions return `[]` not `undefined`
9. ✅ Error handling uses toast
10. ✅ Function components only
11. ✅ No PropTypes/TypeScript
12. ✅ No React 17+ features
13. ✅ Tests pass
14. ✅ No flaky tests
15. ✅ Lint passes
16. ✅ Build succeeds

---

*Last updated: 2025-12-08*

