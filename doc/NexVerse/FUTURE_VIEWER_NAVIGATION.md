# OGL Pathfinding — spaeterer eigener OpenGenesisLINK-Viewer

**Entscheidung am 11. Oktober 2026:** OGL Pathfinding ist komplett aus OpenGenesisLINK Server 0.9.3.10 entfernt: C#-Kern, Regionsmodule, Navi-/NavMesh-CAPS, NPC-Wegpunktmotor, Konfiguration, CI-Tests und NAV-Abnahmefaelle.

Der bestehende Firestorm-Viewer fuer OpenSimulator bietet keine geeignete vollstaendige Oberflaeche fuer die beabsichtigte OGL-Navigation. Es werden keine leeren oder fingierten NavMesh-Responses ausgeliefert.

**Erst spaeter im eigenstaendigen Viewer-Projekt neu planen:** Darstellungs-/Bearbeitungs-UI, kompatibles Viewer/Server-Protokoll, Kollisionsdaten und Navigation. Authoritative Entscheidungen fuer Scripts/NPCs erfordern auch mit einem eigenen Viewer validierte serverseitige Kollisionen, Landrechte und Grenzen. Ob Code aus der Git-Historie wiederverwendet wird, entscheidet dieses spaetere Projekt.

**Keine Terminierung, keine Vorimplementierung und kein 0.9.3.10-Stable-Gate.**
