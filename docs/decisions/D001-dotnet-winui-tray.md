# D001 — .NET WinUI 3 als Single-Prozess-Tray-App

## Context

Die App läuft nur auf Windows 11 (x64), nur als Tray mit glassy Popup, mit Hintergrund-Polling und Installer mit Autostart. Zur Wahl standen `.NET` (WinUI/WPF), `Tauri` und `Electron`, sowie ein Prozess mit Timer gegenüber einem getrennten Hintergrund-Dienst.

## Decision

`.NET WinUI 3` (Windows App SDK) als einziger Tray-Prozess. Timer-gesteuertes Polling lebt im selben Prozess. Das Popup nutzt native glassy Materialien (Mica/Acrylic).

## Why

Nativ für alles, was das Projekt braucht: Tray-Icon, Popup-Positionierung unten rechts, Credential Manager, Autostart und Installer-Ökosystem. Kein Web-Runtime-Overhead, schneller Start, kleine Verteilung. Ein Prozess reicht für Single-User und hält die Nahtstellen minimal.

## Consequences

Einfachere Verteilung und weniger bewegliche Teile. Dafür Bindung an Windows und an das Windows App SDK. Ein späterer Port auf andere Systeme oder ein Dienst-Modell wäre ein bewusster Neuentscheid.
