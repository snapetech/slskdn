
## Persistent Tabbed Interface for Rooms and Chat

Implement a tabbed interface for Rooms and Chat, similar to how Browse currently works:

### Changes
1. **Tab-based UI** - Replace the horizontal search bar with small tabs + a `+` button to open new searches
2. **Persistent state** - Remember opened rooms/chats in localStorage (like Browse sessions)
3. **Survive crashes/restarts** - Tabs should restore on page reload or app restart
4. **Explicit close** - Rooms/chats stay open until user explicitly closes them (X button on tab)

### Implementation Notes
- Can reuse the tabbed browsing logic from `Browse.jsx`/`BrowseSession.jsx`
- Use localStorage with LRU cache cleanup (already implemented for Browse)
- Consider using `lz-string` compression for larger chat histories

### Related
- Browse tabs implementation: `src/web/src/components/Browse/Browse.jsx`
- Browse session persistence: `src/web/src/components/Browse/BrowseSession.jsx`
