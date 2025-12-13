# Soulfind Integration Notes (Dev-Time Only)

> **Critical Clarification**: Soulfind is **NOT** a runtime dependency of slskdn.  
> **Purpose**: Reference implementation + test harness + protocol oracle  
> **Usage**: Development and testing ONLY

---

## What Soulfind Is (For Us)

### 1. Protocol Reference Implementation

Soulfind is an open-source Soulseek server that **already solved** many protocol edge cases:
- Search result formatting
- Room membership semantics
- User status handling
- Timestamp formats
- Error codes and responses

**How we use it**:
- Read Soulfind's code to understand server-side behavior
- Copy/adapt logic where it saves time (with attribution)
- Cross-check our Soulseek client implementation against Soulfind's expectations

**What we DON'T do**:
- Run Soulfind in production
- Depend on Soulfind for any runtime behavior
- Build APIs that require Soulfind

---

### 2. Test Harness for Integration Testing

**Problem**: Testing Soulseek client code is hard:
- Can't abuse the real server (rate limits, bans, noise)
- Need controlled, repeatable scenarios
- Need to simulate edge cases (server down, banned users, etc.)

**Solution**: Run Soulfind locally in tests:

```bash
# In integration test setup
docker run -d --name soulfind-test -p 2242:2242 soulfind/soulfind:latest

# Run slskdn tests against it
dotnet test --filter Category=SoulseekIntegration

# Simulate disaster mode
docker stop soulfind-test
dotnet test --filter Category=DisasterMode

# Cleanup
docker rm -f soulfind-test
```

**What we test**:
- Search capture and normalization
- Transfer observation pipeline
- Disaster mode failover (kill Soulfind mid-test)
- Scene migration (rooms → DHT scenes)
- Graceful degradation

---

## What Soulfind Is NOT (For Us)

### ❌ NOT a Runtime Component

**We do NOT**:
- Deploy Soulfind alongside slskdn in production
- Route slskdn traffic through Soulfind
- Store shadow index in Soulfind
- Use Soulfind for DHT/overlay coordination

**Why?**
- Violates "no central servers" constraint
- Doubles infrastructure complexity
- Conflicts with DHT-first architecture
- Adds unnecessary maintenance burden

---

### ❌ NOT a Mesh Node

**We do NOT**:
- Make Soulfind a "special beacon" in the mesh
- Give Soulfind privileged DHT keys
- Use Soulfind for scene coordination

**Why?**
- DHT + overlay already handle all coordination
- Would create a "more equal than others" peer
- Defeats the purpose of decentralization

---

### ❌ NOT an Index Backend

**We do NOT**:
- Store shadow index shards in Soulfind's DB
- Query Soulfind for MBID lookups
- Use Soulfind as a cache tier

**Why?**
- Shadow index is DHT-native (distributed)
- Soulfind would be a single point of failure
- Defeats disaster mode resilience

---

## Where Soulfind Code IS Valuable

### ✅ Protocol Behavior Reference

**Useful Soulfind modules** (read and learn from):

1. **Search handling**
   - `soulfind/src/search/*.py` (or equivalent)
   - How to format search results
   - How to handle wildcards and exclusions

2. **Room management**
   - `soulfind/src/rooms/*.py`
   - Membership lists, join/leave semantics
   - How messages are routed

3. **User status**
   - How "user online" state is tracked
   - Status update frequencies
   - Client expectations

**How to use**:
- Read Soulfind code when implementing slskdn capture pipeline
- Borrow parsing logic where appropriate (with attribution)
- Use as ground truth when Soulseek protocol docs are unclear

---

### ✅ Test Fixtures

**Soulfind as test dependency**:

```yaml
# docker-compose.test.yml (dev only)
services:
  soulfind-test:
    image: soulfind/soulfind:latest
    ports:
      - "2242:2242"
    environment:
      - SOULFIND_USERS=testuser1,testuser2,testuser3
    volumes:
      - ./test-fixtures:/data
  
  slskdn-under-test:
    build: .
    depends_on:
      - soulfind-test
    environment:
      - SLSKD_SOULSEEK_SERVER=soulfind-test:2242
      - MESH_DISASTER_MODE_AUTO=true
```

**Test scenarios**:

```csharp
[Fact]
public async Task CaptureShould_NormalizeSearchResults()
{
    // Arrange: Soulfind running with test data
    var search = await client.SearchAsync("Radiohead");
    
    // Act: Trigger capture
    await trafficObserver.OnSearchResultsAsync("Radiohead", search);
    
    // Assert: Shadow index updated
    var mbidHints = await shadowIndex.QueryAsync("mbid:release:...");
    Assert.NotEmpty(mbidHints);
}

[Fact]
public async Task DisasterMode_Should_ActivateWhenServerDown()
{
    // Arrange: Connected to Soulfind
    Assert.Equal(SoulseekHealth.Healthy, healthMonitor.CurrentHealth);
    
    // Act: Kill Soulfind
    await KillSoulfindContainer();
    await Task.Delay(TimeSpan.FromMinutes(1));
    
    // Assert: Disaster mode active
    Assert.Equal(SoulseekHealth.Unavailable, healthMonitor.CurrentHealth);
    Assert.True(disasterMode.IsActive);
}
```

---

## What This Means for Implementation

### Design Documents

**NO CHANGES NEEDED** to what we just planned:
- Phase 6 (Virtual Soulfind) is purely DHT + overlay
- All runtime behavior is decentralized
- Shadow index is DHT shards, not a Soulfind DB
- Scenes are DHT topics, not Soulfind rooms

### Additional Work

**NEW**: Add Soulfind to test infrastructure:

1. **Test Setup**
   - Add `docker-compose.test.yml` with Soulfind
   - Add test fixtures (fake users, libraries)
   - Add CI scripts to spin up/down Soulfind

2. **Protocol Reference**
   - Document which Soulfind modules we reference
   - Add code comments: "// Logic borrowed from Soulfind's room.py"
   - Add LICENSE note if we adapt substantial chunks

3. **Integration Tests**
   - Mark tests that need Soulfind with `[Trait("Category", "Soulfind")]`
   - Add setup/teardown for Soulfind containers
   - Add disaster mode simulation tests

---

## Configuration

**Production** (no Soulfind):
```yaml
soulseek:
  server: "vps.slsknet.org:2242"  # Official server (or unavailable)
  
mesh:
  enabled: true
  shadow_index:
    enabled: true
  disaster_mode:
    auto: true  # Failover to mesh when server unavailable
```

**Development/Testing** (with Soulfind):
```yaml
soulseek:
  server: "localhost:2242"  # Local Soulfind instance
  
mesh:
  enabled: true
  shadow_index:
    enabled: true
  disaster_mode:
    auto: true
    
# Test-specific
testing:
  use_soulfind_fixture: true
  soulfind_container: "soulfind-test"
```

---

## Summary

### ✅ Use Soulfind For:
1. **Protocol reference** - understand server behavior
2. **Test harness** - controlled Soulseek environment
3. **Disaster mode testing** - simulate server failure
4. **Development speedup** - borrow proven logic

### ❌ Don't Use Soulfind For:
1. ~~Runtime indexing~~ (use DHT shadow index)
2. ~~Mesh coordination~~ (use overlay)
3. ~~Scene management~~ (use DHT topics)
4. ~~Production deployment~~ (pure DHT + overlay)

---

## Action Items

**For Documentation**:
- ✅ Keep Phase 6 design as-is (pure DHT + overlay)
- ⬜ Add this file to clarify Soulfind scope
- ⬜ Add test setup documentation

**For Implementation**:
- ⬜ Add Soulfind to `docker-compose.test.yml`
- ⬜ Add integration test suite with Soulfind fixture
- ⬜ Document protocol references in code comments
- ⬜ Add LICENSE attribution if adapting code

---

**Bottom Line**: Soulfind helps us **build** Virtual Soulfind faster and more correctly, but it's not **part of** the Virtual Soulfind mesh at runtime.

The mesh is pure DHT + overlay, exactly as designed.

















