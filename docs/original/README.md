> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Pastebin Clone

A small Pastebin-style learning project with:

- C# ASP.NET Core Minimal API backend
- React frontend
- JSON-file persistence, no database or backend NuGet packages

## Project Layout

```text
backend/   ASP.NET Core API
frontend/  React app
```

## Run It

This repo contains code only. Dependencies are not installed for you.

Backend:

```powershell
cd backend
dotnet run
```

Frontend:

```powershell
cd frontend
npm install
npm run dev
```

The frontend expects the API at `http://localhost:5087`. The Vite dev server proxies `/api` requests there.

## API

- `POST /api/pastes` creates a paste.
- `GET /api/pastes/{slug}` reads a paste.
- `GET /api/pastes/{slug}/raw` reads a paste as plain text.
- `GET /api/pastes/recent` lists recent public, non-expired pastes.
- `GET /api/pastes/search?q=term` searches public, non-expired pastes.
- `PUT /api/pastes/{slug}` edits a paste when the delete key is provided.
- `DELETE /api/pastes/{slug}?deleteKey=...` deletes a paste.

## Built Features

- Create, read, edit, delete, and fork pastes
- Raw text paste endpoint
- Search public pastes
- Recent paste previews
- Copy link/text confirmations
- Download paste as a file
- Expiration countdown
- Character and line counts
- Field-level validation messages

## Notes

Paste files are stored under `backend/App_Data/pastes.json` after the API runs.
