# Recherche: Codex-Kontingentquelle (F002)

Stand: 2026-09-19. Primärquelle: `openai/codex`, Branch `main` (lokaler Sparse-Clone unter `/tmp/codex-src`, Read-only).
Lokale Fakten: `codex-cli 0.155.1`, Login-Modus ChatGPT (`codex login status`, `codex doctor`).

## Authablage (lokal beobachtet, nur Struktur, keine Secrets)

`~/.codex/auth.json` (laut `codex doctor`: `auth storage mode: File`, `stored auth mode: chatgpt`):

```json
{
  "auth_mode": "chatgpt",
  "OPENAI_API_KEY": null,
  "tokens": { "id_token": "...", "access_token": "...", "refresh_token": "...", "account_id": "..." },
  "last_refresh": "..."
}
```

Muster identisch zu F001 (`~/.claude/.credentials.json`): vorhandenes CLI-OAuth-Login desselben Users,
Access- plus Refresh-Token. Die exakte Refresh-Route ist **nicht** aus dem Source-Stand belegt und muss
im Implementierungsticket per Prototyp gegen die installierte CLI (0.155.1) verifiziert werden (analog F001/T2 mit CLI 2.1.267).

## Quota-Endpunkt (Source-belegt)

- Der App-Server (`codex-rs/app-server/src/request_processors/account_processor.rs`,
  `get_account_rate_limits_response`) liest Limits **nicht** aus Session-State, sondern per dediziertem
  Backend-Call: `BackendClient::get_rate_limits_with_reset_credits()` bzw.
  `get_rate_limits_with_luna_reserve()` (`codex-rs/backend-client/src/client/rate_limit_resets.rs`).
- Passiver Leser (kein Inference-Side-Effect): `get_rate_limits_with_reset_credits()` — schlichtes
  `GET`, **ohne** `x-openai-codex-luna-reserve`-Header (nur für Reserve-fähige Clients).
- URL (`rate_limit_status_url`, ebd.): `{base}/api/codex/usage` (PathStyle `CodexApi`;
  Default-Base `https://chatgpt.com/backend-api/codex`, vgl. `client_tests.rs`).
- Auth: ChatGPT-Auth erforderlich (`uses_codex_backend`, kein API-Key-, kein FedRAMP-Account);
  Header via `auth_provider.add_auth_headers` (`codex-rs/backend-client/src/client.rs`, `headers()`).
- Antwort (`RateLimitStatusWithResetCredits`): Liste von Snapshots; bevorzugt wird der Bucket
  `limit_id == "codex"`, Fallback erster Eintrag; zusätzlich `account_id`/`user_id`-Abgleich,
  `plan_type`, Reset-Credits (Consume-Aktionen sind Out-of-Scope-Kandidaten, siehe Grill).

## Fenster-Modell (Source-belegt)

- Snapshot-Fenster (`codex-rs/app-server-protocol/src/protocol/v2/account.rs`, `RateLimitSnapshot`):
  `primary` + `secondary` vom Typ `RateLimitWindow` mit `used_percent` (i32, genutzt),
  `window_duration_mins`, `resets_at`.
- Benennung laut TUI-Statuszeile (`codex-rs/tui/src/bottom_pane/status_line_setup.rs`):
  primär = `FiveHourLimit` („Remaining usage on the primary usage limit“),
  sekundär = `WeeklyLimit` („Remaining usage on the secondary usage limit“).
- Mapping auf Domänensprache: Rest = `100 − used_percent` (auf 0..100 begrenzt), Fenster
  **„5 Stunden“** und **„Woche“** — direktes Analogon zu Claude (Session/Woche).

## Offene Prototyp-Punkte (keine Fakten, ins Ticket)

1. `GET …/api/codex/usage` mit dem ChatGPT-Access-Token: exakte Request-Header + Antwortform live verifizieren.
2. Token-Rotation: Refresh-Route/Parameter gegen CLI 0.155.1 verifizieren.
3. Verhalten bei API-Key-Login (`OPENAI_API_KEY` gesetzt): zu erwarten ist kein Plan-Kontingent → `n/a`-Fall.
