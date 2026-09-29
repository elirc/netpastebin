> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Validation, Auth, And Permissions

## Validation Layers

| Layer | Evidence | Role |
| --- | --- | --- |
| UI convenience | Disable create above 100,000 chars in [main.jsx](../../../frontend/src/main.jsx#L347-L349) | Helps honest users. |
| API request validation | [CreatePasteRequest.Validate](../../../backend/Models/CreatePasteRequest.cs#L11-L40), [UpdatePasteRequest.Validate](../../../backend/Models/UpdatePasteRequest.cs#L9-L28) | Authoritative input boundary. |
| Store invariants | Expiry checks in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L31-L34) and [PasteStore.cs](../../../backend/Data/PasteStore.cs#L110-L112) | Protect persisted state and reads. |

## Auth Status

No ASP.NET Identity, JWT, cookie auth, `[Authorize]`, policy handlers, or tenant scoping were found. Permissions are resource-secret based: edit and delete require a delete key checked by hash in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L110-L112) and [PasteStore.cs](../../../backend/Data/PasteStore.cs#L141-L144).

Precise term: this is authorization by possession of a shared secret, not authentication. It does not identify a user.

## IDOR Thinking

IDOR means insecure direct object reference: a user accesses or mutates a resource they should not. Here, slug routes expose resources directly in [Program.cs](../../../backend/Program.cs#L59-L68), [Program.cs](../../../backend/Program.cs#L82-L97), and [Program.cs](../../../backend/Program.cs#L99-L113). Read access is intentionally link-based; edit/delete access depends on delete key.

Junior misses:

- Treating a random slug as permission.
- Trusting hidden UI controls.
- Forgetting unlisted pastes must stay out of recent/search lists.

Senior checks:

- Are secrets logged? Query-string delete key risk in [main.jsx](../../../frontend/src/main.jsx#L187-L190).
- Are brute-force attempts rate-limited? No rate limiting found.
- Are auth errors distinguishable? Wrong key and missing paste both return not found in [Program.cs](../../../backend/Program.cs#L90-L96).

## Security Drill

Threat-model delete:

1. Attacker has slug, not key.
2. Attacker guesses keys.
3. App receives many DELETE requests.
4. What stops them today? Hash check only.
5. What would you add? Rate limiting, body-based secret, structured logging without secret values, tests.
