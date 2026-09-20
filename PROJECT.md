# Project

## Problem

Ich muss drei Browser-Dashboards manuell öffnen, um meine Rest-Tokens zu sehen. Es gibt keinen einfachen Blick in der Windows-Statusleiste.

## Goal

Tray-App für Windows 11: Klick auf das Tray-Icon öffnet ein kleines Popup unten rechts mit den aktuellen Token-Ständen aller drei AI-Pläne. Aktualisierung beim Öffnen plus Refresh-Button (kein Hintergrund-Polling, siehe D010).

## Users / Actors

Nur ich (Single-User, ein Windows-11-Rechner, kein Sync, kein Multi-User).

## Core Use Cases

- Klick auf das Tray-Icon → Popup mit Ständen je Anbieter und Fenster, „zuletzt aktualisiert um HH:MM" und Refresh-Button
- API-Keys beziehungsweise Tokens einmal hinterlegen, danach Ablage im Windows Credential Manager

## Scope

- Nur Tray mit Popup (kein großes Hauptfenster)
- Anbieter in v1: Claude Code, Codex und OpenCode Go (erweiterbar)
- Je Anbieter die nativen Fenster (zum Beispiel Session, Woche, Monat) als Rest-Tokens
- Einfacher Installer

## Non-Goals

- Kein Verlauf und keine Graphen
- Kein Auto-Top-Up und kein Kauf
- Keine Warnungen oder Toasts in v1 (eventuell später)
- Kein macOS, Linux oder Mobile; kein Multi-User und kein Cloud-Sync

## Constraints

- Nur Windows 11 (x64)
- Einfacher Installer
- Authentifizierung: manuell hinterlegte Keys beziehungsweise Tokens, Ablage im Windows Credential Manager
- Fehlerfall: Anzeige von „n/a – Key prüfen / offline", kein Absturz

## Success Criteria

- Klick auf das Tray-Icon zeigt alle drei Stände in unter 5 Sekunden, ohne Browser
- Abruf beim Öffnen (Cache zuerst, dann live), manueller Refresh jederzeit möglich
