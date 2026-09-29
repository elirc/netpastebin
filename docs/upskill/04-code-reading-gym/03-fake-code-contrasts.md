> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Fake Code Contrasts

Each bad snippet is illustrative fake code, not from this repo.

## Contrast 1: Trusting Client Validation

```csharp
// Illustrative fake code: not from this repo.
app.MapPost("/api/pastes", (CreatePasteRequest request, PasteStore store) => store.CreateAsync(request));
```

Better: validate as done in [Program.cs](../../../backend/Program.cs#L70-L76). Failure mode: bad enum/content/delete key reaches storage.

## Contrast 2: Storing Raw Delete Key

```csharp
// Illustrative fake code: not from this repo.
DeleteKeyHash = request.DeleteKey
```

Better: hash in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L236-L240). Senior caveat: use slow password hashing for real secrets.

## Contrast 3: Searching Unlisted Pastes

```csharp
// Illustrative fake code: not from this repo.
return pastes.Where(p => p.Content.Contains(query));
```

Better: filter public and non-expired first in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L169-L177).

## Contrast 4: Missing Effect Cleanup

```jsx
// Illustrative fake code: not from this repo.
useEffect(() => { fetch(url).then(setSelectedPaste); }, [route]);
```

Better: stale response guard in [main.jsx](../../../frontend/src/main.jsx#L51-L77). Stronger: `AbortController`.

## Contrast 5: Leaking Object URLs

```jsx
// Illustrative fake code: not from this repo.
URL.createObjectURL(blob);
```

Better: revoke after download in [main.jsx](../../../frontend/src/main.jsx#L543-L553).

## Contrast 6: Blocking File I/O In Requests

```csharp
// Illustrative fake code: not from this repo.
File.WriteAllText(path, json);
```

Better: async stream serialization in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L204-L205). Caveat: still whole-file write.

## Contrast 7: Coupling List DTO To Detail DTO

```csharp
// Illustrative fake code: resembles current risk.
return Results.Ok(pastes.Select(PasteResponse.FromPaste));
```

This is currently real in [Program.cs](../../../backend/Program.cs#L31-L35). Better future: `PasteSummaryResponse` without content.

## Contrast 8: Timer Without Cleanup

```jsx
// Illustrative fake code: not from this repo.
useEffect(() => setInterval(tick, 30000), []);
```

Better: cleanup in [main.jsx](../../../frontend/src/main.jsx#L393-L396).

## Contrast 9: Enum Reordering

```csharp
// Illustrative fake code: not from this repo.
public enum PasteVisibility { Unlisted = 0, Public = 1 }
```

Risk: frontend sends numeric values at [main.jsx](../../../frontend/src/main.jsx#L323-L326). Reordering breaks existing clients.

## Contrast 10: Swallowing Store Errors

```csharp
// Illustrative fake code: not from this repo.
catch { return Results.Ok(); }
```

Better: let failures surface during learning; later add exception middleware and structured logs.
