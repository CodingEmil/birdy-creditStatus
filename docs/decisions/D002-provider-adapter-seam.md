# D002 — Provider-Adapter hinter normiertem Snapshot-Modell

## Context

Drei Anbieter (Claude Code, Codex, OpenCode Go) mit jeweils eigenen Fenstern (zum Beispiel Session, Woche, Monat) und eigenen Dashboard-Quellen. Die UI soll nur einen stabilen Vertrag kennen, neue Anbieter sollen ohne Core-Umbau möglich sein.

## Decision

Jeder Anbieter bekommt einen Adapter hinter einem schmalen Interface: `fetchSnapshot(auth) → QuotaSnapshot[]`. Der Adapter kapselt Auth-Abruf, Fetch und Mapping auf das normierte Modell (Anbieter, Fenster, Rest-Tokens, Reset, Zeitstempel, Fehlerzustand). UI und Abruf kennen nur das normierte Modell.

## Why

Tiefe Module hinter kleiner Naht: substantielles Provider-Verhalten (Eigenheiten, Fehler, Mapping) verschwindet hinter einer stabilen Schnittstelle. Hohe Leverage pro Interface-Einheit, kleine Blast Radius bei Anbieter-Änderungen, natürliche Testfläche am Interface.

## Consequences

Neuer Anbieter bedeutet neuer Adapter, Core und UI bleiben stabil. Dafür muss jeder Adapter die realen Quellen (heute: Anbieter-Websites) pro Spec recherchieren und per Prototyp belegen. Adapter-Fehler bleiben lokal und werden als `n/a` pro Karte sichtbar.
