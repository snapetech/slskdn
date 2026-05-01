# Soulseek Native Discovery

slskdN exposes the native Soulseek interest and recommendation protocol through the backend API. These calls are opt-in and use normal Soulseek server commands; they do not alter search, browse, transfer, room, or private-message behavior unless an operator or UI action calls them.

## What This Adds

| Feature | Where to use it | What it does |
| ------- | --------------- | ------------ |
| Liked interests | Search → Soulseek Discovery, API | Adds or removes raw interest strings from the logged-in Soulseek account |
| Hated interests | Search → Soulseek Discovery, API | Adds or removes raw disliked interest strings from the logged-in Soulseek account |
| Personal recommendations | Search → Soulseek Discovery, API | Asks the Soulseek server for recommendations based on the logged-in account's interests |
| Global recommendations | Search → Soulseek Discovery, API | Asks the Soulseek server for globally recommended item strings |
| User interests | User-card popup, Search → Soulseek Discovery, API | Fetches another user's liked and hated interest strings |
| Similar users | Search → Soulseek Discovery, API | Fetches users the Soulseek server considers similar to the logged-in account |
| Item recommendations | Search → Soulseek Discovery, API | Branches from a raw item string into related item recommendations |
| Item similar users | Search → Soulseek Discovery, API | Finds users associated with a raw item string |
| Batch private messages | Messages toolbar, API | Sends one native multi-recipient private-message command and records local conversation rows per recipient |

Native item values are search/discovery seeds. They are not MusicBrainz IDs, verified releases, or trusted catalog identities.

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

### Response Shapes

Recommendation endpoints return the runtime's native models serialized as JSON. Property casing can appear as camelCase in the web client, but the logical shape is:

```json
{
  "recommendations": [
    { "item": "ambient", "score": 42 }
  ],
  "unrecommendations": [
    { "item": "noise", "score": -5 }
  ]
}
```

User-interest lookup returns:

```json
{
  "username": "alice",
  "liked": ["ambient", "dub"],
  "hated": ["noise"]
}
```

Similar-user lookup returns:

```json
[
  { "username": "alice", "rating": 10 }
]
```

Item-specific endpoints echo the requested item and include either `recommendations` or `usernames`.

## Taste Recommendations

`POST /api/v0/taste-recommendations` accepts `includeSoulseekRecommendations: true`. When set, slskdN asks the Soulseek server for native recommendations and folds those raw item strings into the existing review-first recommendation response.

Native Soulseek recommendations remain raw search seeds. They are safe to promote to Wishlist for review, but slskdN does not treat them as verified MusicBrainz identities.

## UI Integration

Search includes a **Soulseek Discovery** panel. It can manage interests, load personal/global native recommendations, branch from an item, inspect similar users, start normal searches from recommendation items, and save raw recommendation seeds to Wishlist in disabled review mode.

Discovery panel actions:

- **Add Interest / Add Hated** update the logged-in Soulseek account's native interest lists.
- **Remove Interest / Remove Hated** remove raw strings from those native lists.
- **My Recs** and **Global** load recommendation lists and show scores when the server provides them.
- **Item Recs** branches from the item field into related item strings.
- **Similar Users** and **Item Users** load user lists and let you inspect native interests for a selected user.
- **Search** starts a normal slskdN Soulseek search from a raw recommendation string.
- **Wishlist** creates a disabled, review-only Wishlist row with `autoDownload: false`.

The **Federated Taste** panel has an **Include Soulseek native** toggle. This keeps native recommendation use explicit because Soulseek returns item strings, not verified release identities.

User cards expose a heart interest action. Interests are loaded only when the popup is opened, so large room, search, and message views do not fan out native interest requests in the background.

Messages includes a **Batch Private Message** action in the workspace toolbar. It uses the native multi-recipient private-message command when available and persists one local conversation per recipient so legacy conversation views still behave normally.

## Compatibility

Native discovery is backward-compatible with legacy clients because all new commands are server commands and are sent only after an explicit authenticated UI/API action. Existing search, browse, transfer, room, and one-to-one private-message flows keep their normal protocol paths.

Batch private messages use the Soulseek server's native multi-recipient command. This does not require each recipient to support new peer-message behavior; recipients receive ordinary private messages through the server path.

Type-1 peer-message obfuscation is documented separately in [Soulseek Type-1 Obfuscation](soulseek-type1-obfuscation.md). Native discovery does not require obfuscation and does not change transfer traffic.

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

Additional guardrails:

- Mutating interest APIs reject blank item strings.
- Batch private messages reject blank messages and blank recipient lists.
- Batch private messages are capped at 100 unique recipients by the runtime.
- User-card interest lookup is lazy to avoid sending one server request per rendered user.
- Wishlist handoff is review-only and disabled by default so native recommendation strings do not auto-download.
