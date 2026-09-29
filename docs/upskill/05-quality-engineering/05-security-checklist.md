> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Security Checklist

Use before merging feature work.

| Check | Current evidence | Action |
| --- | --- | --- |
| Server-side validation | [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L11-L40), [UpdatePasteRequest.cs](../../../backend/Models/UpdatePasteRequest.cs#L9-L28) | Keep backend authoritative. |
| Authorization | Delete-key hash check in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L110-L112) | Treat as resource secret, not identity. |
| IDOR | Slug routes in [Program.cs](../../../backend/Program.cs#L59-L113) | Make intentional access rules explicit. |
| Secret transport | Delete key query in [main.jsx](../../../frontend/src/main.jsx#L187-L190) | Change to body/header. |
| XSS | Paste rendered as React text in [main.jsx](../../../frontend/src/main.jsx#L474-L476) | Avoid `dangerouslySetInnerHTML`. |
| CSRF | No cookie auth found | If cookie auth is added, add anti-forgery strategy. |
| SQL injection | No SQL/EF found | If DB added, use parameterized queries/EF. |
| SSRF | No outbound HTTP found | If webhooks/imports added, validate URLs. |
| Rate limiting | No rate-limit middleware found | Add limits on create/edit/delete/search. |
| CORS | Frontend origins in [Program.cs](../../../backend/Program.cs#L7-L16) | Keep origin list tight. |
| Secrets in repo | `.env` ignored in [.gitignore](../../../.gitignore#L10-L11) | Do not commit real keys. |
| Dependency risk | `latest` deps in [package.json](../../../frontend/package.json#L11-L17) | Pin and audit. |

Pre-merge question: "Can a user affect a paste they should not affect?" Answer with anchors.
