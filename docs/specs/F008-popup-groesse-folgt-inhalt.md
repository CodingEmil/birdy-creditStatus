# F008 — Popup-Größe folgt Inhalt (Spec, Issue #28)

## Problem Statement

Das Popup ist fix 380×860. Bei wenigen Konten (F007-Leere-Regel: keine Karte ohne
Konto) bleibt unten viel Leerraum; der Inhalt scrollt im `ScrollViewer`, das Fenster
atmet nicht.

## Solution

Nach jedem Render (Öffnen, Refresh, Hinzufügen/Umbenennen/Entfernen) Inhaltshöhe
messen und Fensterhöhe anpassen: Cap = min(860, Arbeitshöhe − Rand), kein
künstliches Minimum, Breite fix 380. Über dem Cap scrollt der `ScrollViewer` wie
bisher. Danach Fenster neu unten-rechts andocken. Schlägt die Messung fehl,
bleibt 860 (nie Absturz, nie unsichtbar).

## User Stories

1. As Single-User, I want dass das Popup bei wenigen Karten schrumpft, so that kein Leerraum unten bleibt.
2. As Single-User, I want dass das Popup bei vielen Karten maximal bis zum Cap wächst und dann scrollt, so that nichts vom Bildschirm läuft.

## Implementation Decisions

- Messung: `CardsPanel` nach Layout-Update messen (Verfügungsbreite = Fensterbreite − Padding), Fensterhöhe = Inhalt + kleines Chrome-Extra.
- Cap: `min(860, Arbeitshöhe − 32)`; kein Minimum.
- Rechenkern (`Fit`, `MaxHeight`) als Core-Helfer `PopupSizing` (testbar); Fenster-Interaktion bleibt im Code-Behind mit try/catch (Fallback 860).
- Aufruf am Ende von `UpdateUi` und `RenderCached` (deckt Öffnen/Refresh/Dialog-Aktionen ab).

## Testing Decisions

- `PopupSizing`-Tests: Clamp ohne Minimum, Cap-Formel inkl. kleiner Arbeitshöhe.
- `dotnet build` 0 Warnungen / 0 Fehler; Suite grün.
- Visuell: Dev-/Test-Install mit 0/1/vielen Konten (Klickpfad durch Nutzer).

## Out of Scope

- Breitenänderung; Animation beim Größenwechsel; Mindesthöhe; Scrollverhalten ändern.

## Further Notes

- Abnahme-Feedback zu F007 (F007 selbst unverändert).
