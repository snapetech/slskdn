# Soulseek Native Discovery

slskdN exposes the native Soulseek interest and recommendation protocol through the backend API. These calls are opt-in and use normal Soulseek server commands; they do not alter search, browse, transfer, room, or private-message behavior unless an operator or UI action calls them.

## API

Interest management:

- `POST /api/v0/soulseek/interests` with `{ "item": "ambient" }`
- `DELETE /api/v0/soulseek/interests/{item}`
- `POST /api/v0/soulseek/hated-interests` with `{ "item": "noise" }`
- `DELETE /api/v0/soulseek/hated-interests/{item}`

Recommendations:

- `GET /api/v0/soulseek/recommendations`
- `GET /api/v0/soulseek/recommendations/global`
- `GET /api/v0/soulseek/users/similar`
- `GET /api/v0/soulseek/users/{username}/interests`
- `GET /api/v0/soulseek/items/{item}/recommendations`
- `GET /api/v0/soulseek/items/{item}/similar-users`

Private messages:

- `POST /api/v0/conversations/batch` with `{ "usernames": ["alice", "bob"], "message": "hello" }`

Batch private messages are deduplicated case-insensitively by the runtime and capped at 100 unique recipients per call. slskdN stores one outbound message row per recipient so existing conversation views continue to work.

## Taste Recommendations

`POST /api/v0/taste-recommendations` accepts `includeSoulseekRecommendations: true`. When set, slskdN asks the Soulseek server for native recommendations and folds those raw item strings into the existing review-first recommendation response.

Native Soulseek recommendations remain raw search seeds. They are safe to promote to Wishlist for review, but slskdN does not treat them as verified MusicBrainz identities.

## UI Integration

Search includes a **Soulseek Discovery** panel. It can manage interests, load personal/global native recommendations, branch from an item, inspect similar users, start normal searches from recommendation items, and save raw recommendation seeds to Wishlist in disabled review mode.

The **Federated Taste** panel has an **Include Soulseek native** toggle. This keeps native recommendation use explicit because Soulseek returns item strings, not verified release identities.

User cards expose a heart interest action. Interests are loaded only when the popup is opened, so large room, search, and message views do not fan out native interest requests in the background.

Messages includes a **Batch Private Message** action in the workspace toolbar. It uses the native multi-recipient private-message command when available and persists one local conversation per recipient so legacy conversation views still behave normally.

## Safety

These calls share the existing Soulseek safety limiter using separate source buckets:

- `soulseek-interest`
- `soulseek-recommendations`
- `soulseek-user-interests`
- `soulseek-similar-users`
- `soulseek-item-recommendations`
- `soulseek-item-similar-users`
- `taste-recommendations-soulseek`

This keeps discovery features from starving manual searches or browse requests.
