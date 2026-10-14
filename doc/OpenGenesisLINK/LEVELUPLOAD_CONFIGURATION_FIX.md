# OGL Asset Upload: UserLevel-Schwelle (`LevelUpload`)

## Korrektur

Im bisherigen Code las `AssetTransactionModule` die Upload-Schwelle nur aus
`[Startup]`; der moderne Viewer-CAPS-Pfad (`BunchOfCapsModule`)
verwendete `[Startup]` und nur bei Wert `0` einen Fallback auf
`[Permissions]`. Der dokumentierte Wert in `[Permissions]` schützte
klassische UDP-Uploads deshalb nicht zuverlässig.

Beide Transportwege verwenden jetzt
`OpenSim.Framework.AssetUploadLevelPolicy.Resolve` mit einheitlicher
Reihenfolge:

1. Ist `LevelUpload` in `[Permissions]` ausdrücklich gesetzt,
   gilt genau dieser Wert (auch `0`).
2. Sonst wird ein vorhandener `[Startup] LevelUpload`-Wert verwendet
   (Abwärtskompatibilität mit älteren Installationen).
3. Ohne Einstellung gilt weiterhin `0`.

## Beispiel für die Simulator-OpenSim.ini

```ini
[Permissions]
    LevelUpload = 200
```

`0` erlaubt gewöhnlichen Usern die von dieser Regel erfassten Uploads;
`200` verlangt `UserLevel >= 200`. Änderungen greifen nach einem
Neustart des jeweiligen **Simulators**; ein reiner ROBUST-Neustart
reicht nicht.

**Wichtig:** Das ist eine **Upload-Schwelle und kein globales Asset-Schreibverbot**.
Die bisherigen Prüfungen betreffen im UDP-Pfad normale, nicht temporäre
Texturen, TGA-Texturen, Sounds und Animationen. Im Viewer-CAPS-Pfad
werden Texturen, Sounds, Animationen und Mesh beim Upload-Request
kontrolliert. Andere Upload- und Update-Wege (beispielsweise bestimmte
Skript-, Notizkarten-, Bekleidungs-/Bodypart-Updates, temporäre Assets,
administrative Imports und serviceinterne Asset-Schreibzugriffe) werden
durch `LevelUpload` **nicht durchgängig** gesperrt. Eine vollständige
Upload-Policy erfordert gesonderte, serverseitige Autorisierung für
alle relevanten Pfade.

Eine eigenständige `[Mesh] LevelUpload`-Schwelle wird nicht
ausgewertet; die frühere missverständliche Kommentarzeile in
`OpenSimDefaults.ini` wurde berichtigt. Mesh nutzt denselben
`[Permissions] LevelUpload` wie die anderen genannten CAPS-Uploads.

## Tests

```bash
python3 tools/ci/verify_asset_upload_level_consistency.py
dotnet run -c Release --project tools/ci/AssetUploadLevelRegression/AssetUploadLevelRegression.csproj
```

Die Tests prüfen die gemeinsame Verwendung im UDP- und CAPS-Modul,
fehlen-de Werte, `[Startup]`-Fallback, `[Permissions]`-Vorrang,
und ein ausdrücklich gesetztes `[Permissions] LevelUpload = 0`.
