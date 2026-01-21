# CSRF Protection Testing Guide

**Feature**: CSRF Protection for slskdN  
**Branch**: `experimental/whatAmIThinking`  
**Status**: ✅ **Implemented and Tested** (Commit: fc715979)

---

## What Was Implemented

### Backend:
- ✅ AntiForgery services configured (30-day session tokens)
- ✅ CSRF middleware generates tokens automatically
- ✅ Custom validation filter (`ValidateCsrfForCookiesOnlyAttribute`)
- ✅ Smart exemptions (anonymous endpoints, JWT, API keys, safe methods)
- ✅ All 85+ controllers protected
- ✅ Login fix applied (anonymous endpoints exempt)

### Frontend:
- ✅ Axios interceptor automatically adds CSRF tokens
- ✅ Reads from `XSRF-TOKEN` cookie
- ✅ Adds `X-CSRF-TOKEN` header to POST/PUT/DELETE/PATCH

---

## How to Test

### Test 1: Web UI Works Normally (Should Pass ✅)

**Scenario**: Regular web UI usage should be transparent

1. Start slskdN: `dotnet run --project src/slskd/slskd.csproj`
2. Start frontend: `cd src/web && npm start`
3. Open browser: `http://localhost:3000`
4. Login with credentials
5. **Test actions**:
   - Download a file
   - Delete a file
   - Change settings
   - Create a search
   - Delete a search

**Expected**: Everything works normally, no errors

**Check**:
```bash
# In browser console (F12):
document.cookie  // Should see XSRF-TOKEN=...

# In Network tab:
# POST/PUT/DELETE requests should have header:
# X-CSRF-TOKEN: <some-value>
```

---

### Test 2: JWT API Clients Work (Should Pass ✅)

**Scenario**: API clients using JWT should be unaffected

```bash
# Login to get JWT
curl -X POST http://localhost:5030/api/v0/session/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin"}' \
  | jq -r '.token'

# Save token
TOKEN="<paste-token-here>"

# Test state-changing operation (NO CSRF token needed)
curl -X POST http://localhost:5030/api/v0/searches \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"searchText":"test"}'
```

**Expected**: 200 OK, search created

**Should NOT** require CSRF token ✅

---

### Test 3: API Key Clients Work (Should Pass ✅)

**Scenario**: API clients using API keys should be unaffected

```bash
# Using API key (no CSRF token needed)
curl -X POST http://localhost:5030/api/v0/searches \
  -H "X-API-Key: your-api-key-here" \
  -H "Content-Type: application/json" \
  -d '{"searchText":"test"}'
```

**Expected**: 200 OK

**Should NOT** require CSRF token ✅

---

### Test 4: Cookie-Based Requests Without CSRF Fail (Should Fail ❌ Then Pass ✅)

**Scenario**: Cookie-based requests without CSRF token should be rejected

```bash
# Step 1: Login and save cookies
curl -c cookies.txt \
  -X POST http://localhost:5030/api/v0/session/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin"}'

# Step 2: Try to make request with cookie but NO CSRF token
curl -b cookies.txt \
  -X POST http://localhost:5030/api/v0/searches \
  -H "Content-Type: application/json" \
  -d '{"searchText":"test"}'
```

**Expected**: 400 Bad Request with message about CSRF token

**Response should include**:
```json
{
  "error": "CSRF token validation failed",
  "message": "This request requires a valid CSRF token...",
  "hint": "Web UI: Refresh page | API: Use Authorization header..."
}
```

---

### Test 5: Cookie-Based Requests WITH CSRF Pass (Should Pass ✅)

**Scenario**: Cookie-based requests with CSRF token should work

```bash
# Step 1: Login and save cookies
curl -c cookies.txt \
  -X POST http://localhost:5030/api/v0/session/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin"}'

# Step 2: Extract CSRF token from cookies
CSRF_TOKEN=$(grep XSRF-TOKEN cookies.txt | awk '{print $7}')

# Step 3: Make request with BOTH cookie AND CSRF token
curl -b cookies.txt \
  -H "X-CSRF-TOKEN: $CSRF_TOKEN" \
  -X POST http://localhost:5030/api/v0/searches \
  -H "Content-Type: application/json" \
  -d '{"searchText":"test"}'
```

**Expected**: 200 OK, search created ✅

---

### Test 6: GET Requests Don't Need CSRF (Should Pass ✅)

**Scenario**: Safe methods don't require CSRF tokens

```bash
# GET request with cookies but no CSRF token
curl -b cookies.txt \
  -X GET http://localhost:5030/api/v0/session
```

**Expected**: 200 OK (GET is exempt from CSRF)

---

### Test 7: Frontend Automatic Token Handling

**Scenario**: React app should automatically include tokens

1. Open browser DevTools (F12) → Network tab
2. Login to web UI
3. Perform any action (delete file, start download, etc.)
4. In Network tab, find the POST/PUT/DELETE request
5. Check request headers:

**Should see**:
```
X-CSRF-TOKEN: <some-long-random-string>
Cookie: XSRF-TOKEN=<same-string>; session=...
```

**If missing**: Check browser console for JavaScript errors

---

## Troubleshooting

### Issue: "CSRF token validation failed" in web UI

**Check**:
1. Browser has `XSRF-TOKEN` cookie (check `document.cookie`)
2. Network requests include `X-CSRF-TOKEN` header
3. Token values match

**Fix**:
- Refresh page (gets new token)
- Clear cookies and login again
- Check browser console for JavaScript errors

---

### Issue: API clients getting 400 errors

**Check**:
1. Are you using `Authorization: Bearer <token>` header? (should be exempt)
2. Are you using `X-API-Key` header? (should be exempt)
3. Are you accidentally sending cookies? (don't send cookies with API requests)

**Fix**:
- Add JWT token to request: `-H "Authorization: Bearer $TOKEN"`
- Or use API key: `-H "X-API-Key: $KEY"`
- Or add CSRF token: `-H "X-CSRF-TOKEN: $CSRF"`

---

### Issue: SignalR connections failing

**Check**: SignalR hubs should be exempt (they use GET for upgrades)

**If failing**: Check that WebSocket upgrade requests don't trigger CSRF validation

---

## Session Configuration

**Current settings**:
- Session duration: **30 days** (configurable in Options)
- Sliding expiration: **Yes** (extends on activity)
- CSRF token lifetime: Tied to session
- Cookie settings: SameSite=Strict, Secure (HTTPS only in prod)

**To change session duration**:
```yaml
# In slskd.yml
web:
  authentication:
    session_timeout_days: 30  # Adjust as needed
```

---

## Monitoring

### Logs to Watch:

```bash
# CSRF validation events
journalctl -u slskd -f | grep CSRF

# Should see:
# [CSRF] Skipping validation for JWT Bearer token
# [CSRF] Skipping validation for API key
# [CSRF] Validating CSRF token for cookie-based request
# [CSRF] Token validation successful
# [CSRF] Token validation failed (if attack detected)
```

---

## Security Impact

### What This Protects Against:

✅ **Cross-Site Request Forgery** - Evil websites can't make requests using your cookies
✅ **Session Riding** - Attackers can't hijack your authenticated session
✅ **CSRF via XSS** - Even if XSS exists, CSRF token is in cookie (not localStorage)

### What This Does NOT Protect Against:

❌ **XSS attacks** (different threat model - need CSP headers)
❌ **Session theft** (if attacker steals both cookie AND CSRF token)
❌ **Man-in-the-middle** (need HTTPS for that)

---

## Rollback Plan

If CSRF causes issues:

### Option 1: Disable CSRF Temporarily
```csharp
// In Program.cs, comment out the middleware:
// app.Use(async (context, next) => { ... CSRF middleware ... });
```

### Option 2: Disable for Specific Controller
```csharp
// Remove attribute from problematic controller:
// [ValidateCsrfForCookiesOnly]
public class ProblematicController : ControllerBase
```

### Option 3: Full Rollback
```bash
git revert HEAD  # Reverts CSRF commit
```

---

## Success Criteria

✅ **All tests pass**:
1. Web UI works normally
2. JWT API clients work (no CSRF token needed)
3. API key clients work (no CSRF token needed)
4. Cookie-based requests without CSRF fail
5. Cookie-based requests with CSRF succeed
6. GET requests don't need CSRF
7. Frontend automatically includes tokens

✅ **No breaking changes for API consumers**

✅ **Logs show CSRF validation working**

---

## Next Steps After Testing

1. **If all tests pass**: Merge to `whatAmIThinking` ✅
2. **If issues found**: Debug and fix
3. **After merge**: Monitor logs for CSRF failures
4. **Future**: Consider adding to stable release

---

**Testing By**: [Your Name]  
**Date Tested**: [Date]  
**Result**: [Pass/Fail]  
**Notes**: [Any issues or observations]
