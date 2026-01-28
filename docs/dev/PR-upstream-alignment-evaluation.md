# PR upstream alignment evaluation

**Date:** 2026-01-28  
**Purpose:** For each open PR, assess whether merging it would move slskdn’s dependencies ahead of upstream [slskd/slskd](https://github.com/slskd/slskd), making future merges from upstream harder.

**Method:** Fetched all PRs via `gh pr list --state all`, compared proposed version bumps to upstream’s current versions (from `https://raw.githubusercontent.com/slskd/slskd/master/`).

---

## Upstream baseline (slskd master)

- **Backend:** `net8.0`; NuGet packages match slskdn today (e.g. Asp.Versioning 8.1.0, FluentFTP 49.0.2, IPAddressRange 6.0.0, Microsoft.* 8.0.6, NetAnalyzers 8.0.0, etc.). Upstream does **not** reference MessagePack, Microsoft.CodeAnalysis.CSharp, Microsoft.CodeAnalysis.Analyzers, or Microsoft.Build.* in the main slskd.csproj.
- **Frontend:** Same as slskdn: react ^16.8.6, react-router-dom ^5.0.0, axios ^0.30.2, uuid ^8.3.0, eslint ^8.56.0, semantic-ui-react ^2.1.0, etc.

---

## Summary table

| PR   | Title / change | Would diverge from upstream? | Recommendation |
|------|----------------|-----------------------------|-----------------|
| **94** | Bump nuget group (3): System.Text.Json 8.0.5, System.Net.Http, System.Text.RegularExpressions | **Low** – additive refs; upstream doesn’t pin these. Possible future skew if upstream adds them at different versions. | **Caution** – consider only if needed for build/runtime. |
| **93** | Microsoft.Data.Sqlite 8.0.6 → **10.0.2** | **Yes** – major jump; upstream is 8.0.6. | **Reject** for upstream alignment. |
| **92** | Microsoft.CodeAnalysis.NetAnalyzers 8.0.0 → **10.0.102** | **Yes** – upstream is 8.0.0. | **Reject** for upstream alignment. |
| **91** | Microsoft.CodeAnalysis.CSharp 4.8.0 → 5.0.0, CodeAnalysis.Analyzers 3.3.4 → 3.11.0 | **No** – upstream doesn’t reference these (fork-only). | **OK** – no upstream conflict. |
| **90** | (Closed) CodeAnalysis.Analyzers 3.11.0 | N/A | N/A |
| **89** | Microsoft.AspNetCore.SignalR.Client 8.0.6 → **10.0.2** | **Yes** – major; upstream is 8.0.6. | **Reject** for upstream alignment. |
| **88** | Microsoft.AspNetCore.Authentication.JwtBearer 8.0.6 → **8.0.23** | **No** – same major; patch/minor. Upstream could take same. | **OK** – safe. |
| **87** | Nuget group (2): Microsoft.Extensions.Caching.Memory 8.0.1, System.Text.Json 8.0.5 | **Low** – additive; same as PR 94 note. | **Caution**. |
| **86** | MessagePack 2.5.187 → **3.1.4** | **N/A** – upstream doesn’t use MessagePack (fork-only). Major version = API risk. | **Caution** – no upstream conflict but 3.x may require code changes; test before merge. |
| **85** | @codemirror/language 6.7.0 → 6.12.1 | **No** – minor; upstream uses ^6.0.0. | **OK**. |
| **84** | IPAddressRange 6.0.0 → 6.3.0 | **No** – same major; upstream 6.0.0. | **OK**. |
| **83** | eslint 8.56.0 → **9.39.2** | **Yes** – major; upstream is ^8.56.0. | **Reject** for upstream alignment. |
| **82** | FluentFTP 49.0.2 → 53.0.2 | **Yes** – major; upstream is 49.0.2. | **Reject** for upstream alignment. |
| **81** | @semantic-ui-react/css-patch 1.1.2 → 1.1.3 | **No** – patch. | **OK**. |
| **80** | Asp.Versioning.Mvc.ApiExplorer 8.1.0 → 8.1.1 | **No** – patch; upstream 8.1.0. | **OK**. |
| **79** | @uiw/react-codemirror 4.21.2 → 4.25.4 | **No** – minor; upstream ^4.2.4. | **OK**. |
| **78** | semantic-ui-react 2.1.4 → 2.1.5 | **No** – patch; upstream ^2.1.0. | **OK**. |
| **77** | @codemirror/legacy-modes 6.3.2 → 6.5.2 | **No** – minor. | **OK**. |
| **76** | uuid 8.3.2 → **13.0.0** | **Yes** – major; upstream ^8.3.0. | **Reject** for upstream alignment. |
| **75** | axios 0.30.2 → **1.13.4** | **Yes** – major; upstream ^0.30.2. | **Reject** for upstream alignment. |
| **74** | react-router-dom 5.3.4 → **7.13.0** | **Yes** – major; upstream ^5.0.0. | **Reject** for upstream alignment. |
| **73** | react 16.14.0 → **19.2.4** | **Yes** – major; upstream ^16.8.6. Conventions (slskdn-conventions.mdc) state “React 16.8.6”. | **Reject** for upstream alignment and project policy. |
| **71** | (Merged) AUR repo master branch fix | N/A | Already merged. |
| **65** | (Closed) PKGBUILD aarch64 | N/A | N/A. |
| **64** | (Closed) Repository status | N/A | N/A. |

---

## Safe to merge (no upstream divergence)

- **88** – JwtBearer 8.0.6 → 8.0.23  
- **91** – CodeAnalysis.CSharp + CodeAnalysis.Analyzers (fork-only)  
- **85** – @codemirror/language  
- **84** – IPAddressRange 6.3.0  
- **81** – @semantic-ui-react/css-patch  
- **80** – Asp.Versioning 8.1.1  
- **79** – @uiw/react-codemirror  
- **78** – semantic-ui-react 2.1.5  
- **77** – @codemirror/legacy-modes  

---

## Would diverge from upstream (recommend reject or defer)

| PR   | Reason |
|------|--------|
| **93** | Microsoft.Data.Sqlite 10.x vs upstream 8.0.6 (major). |
| **92** | NetAnalyzers 10.x vs upstream 8.0.0. |
| **89** | SignalR.Client 10.x vs upstream 8.0.6 (major). |
| **83** | eslint 9.x vs upstream 8.x. |
| **82** | FluentFTP 53.x vs upstream 49.x. |
| **76** | uuid 13.x vs upstream 8.x. |
| **75** | axios 1.x vs upstream 0.30.x. |
| **74** | react-router-dom 7.x vs upstream 5.x. |
| **73** | react 19.x vs upstream 16.x; also conflicts with “React 16.8.6” in conventions. |

---

## Caution (additive or fork-only)

- **94, 87** – New/extra NuGet refs (System.Text.Json, etc.). Low direct conflict but could create version skew if upstream adds them later.  
- **86** – MessagePack 3.x: fork-only package, so no upstream ref to conflict with; major upgrade may need code changes.

---

## Recommendation

- **Merge:** 80, 81, 84, 85, 88, 91, 78, 79, 77.  
- **Reject (for upstream alignment):** 73, 74, 75, 76, 82, 83, 89, 92, 93.  
- **Caution / case-by-case:** 86 (MessagePack 3.x), 94, 87 (extra NuGet refs).

If the goal is to keep slskdn easy to merge from slskd later, avoid any PR that bumps a **shared** dependency to a **higher major** (or much higher minor) than upstream. Fork-only packages (MessagePack, CodeAnalysis.CSharp/Analyzers) don’t create upstream merge conflicts but still need testing on major upgrades.
