# FileRelay

FileRelay ist eine robuste Datei-Transferlösung für Windows-Umgebungen. Die Anwendung überwacht beliebig viele Quellordner und kopiert neu erstellte Dateien automatisiert auf konfigurierbare Netzwerkziele (UNC-Pfade oder gemappte Laufwerke). Jeder Kopiervorgang erfolgt unter Angabe expliziter Domain-Credentials, die sicher über die Windows Data Protection API (DPAPI) verschlüsselt werden.

## Features

- Überwachung von 100+ Quellordnern inklusive optionaler Unterordner
- Intelligente Dateiübertragung mit Schreibsperrenprüfung, atomarem Kopieren und optionalem Löschen der Quelle
- Mehrere Zielpfade pro Quelle inkl. Konfliktstrategie (Ersetzen, Versionieren, Ignorieren)
- Asynchrone, thread-sichere Transfer-Queue mit Retry-Mechanismen und Prüfsummenvalidierung
- Credential-Verwaltung mit DPAPI-Verschlüsselung und Rotationserinnerungen
- WPF-Bedienoberfläche mit Live-Status, Log-Viewer, Wizard zur Quellen-/Zielanlage und Exportfunktionen
- Serilog-basierte Log-Rotation (30 Tage standardmäßig)
- Management-Schnittstelle über Named Pipes, unterstützt Desktop-Modus und optionalen Windows-Service-Modus
- Unit-Tests für zentrale Komponenten und Integrationstest-Szenario

## Projektstruktur

```
FileRelay.sln
├── src
│   ├── FileRelay.Core          # Kernlogik (Watchers, Queue, Copy, Credentials)
│   ├── FileRelay.ServiceHost   # Windows Service Host (Worker Service)
│   └── FileRelay.UI            # WPF-Oberfläche und Verwaltungsclient
├── tests
│   └── FileRelay.Tests         # Unit- und Integrationstests
├── examples
│   └── config.sample.json      # Beispielkonfiguration
└── README.md
```

## Build & Entwicklung

1. Lösung in Visual Studio 2022 (17.4+) oder neuer öffnen (`FileRelay.sln`).
2. NuGet-Pakete wiederherstellen.
3. `FileRelay.ServiceHost` als Startprojekt wählen, um den Dienst im Konsolenmodus zu testen.
4. Alternativ die WPF-Oberfläche (`FileRelay.UI`) starten, um die Konfiguration vorzunehmen.

### Tests ausführen

```
dotnet test FileRelay.sln
```

> Hinweis: Tests für die Credential-Verschlüsselung erfordern Windows.

## Konfiguration

Die Anwendung speichert Konfiguration und Logs standardmäßig unter `%ProgramData%\FileRelay`. Eine Beispielkonfiguration befindet sich unter `examples/config.sample.json`.

Wichtige Parameter:

- `Sources`: Liste der überwachten Quellen inkl. Filter, Targets und Löschoptionen.
- `Targets`: Für jedes Ziel Pfad, Credential-Referenz, Konfliktstrategie, Subfolder-Mapping, Retry-Override.
- `Credentials`: Domain-Benutzer inklusive verschlüsseltem Passwort (DPAPI-Blob).
- `Options`: Globale Einstellungen wie Parallelität, Retry-Strategie, Log-Pfad, Named-Pipe-Endpunkt.

### Credentials speichern

Die WPF-Oberfläche erlaubt das Hinterlegen neuer Credentials. Passwörter werden direkt bei der Eingabe über DPAPI verschlüsselt und niemals im Klartext gespeichert.

## Windows-Service Installation

Die Lösung unterstützt einen Service-Modus, der parallel zum UI-Client laufen kann. Installation über eine administrative Eingabeaufforderung:

```powershell
# Binärdatei nach %ProgramFiles%\FileRelay kopieren
New-Item -ItemType Directory -Force -Path "C:\Program Files\FileRelay"
Copy-Item -Path .\publish\FileRelay.ServiceHost.exe -Destination "C:\Program Files\FileRelay" -Force

# Service registrieren
sc.exe create FileRelay binPath= "\"C:\Program Files\FileRelay\FileRelay.ServiceHost.exe\"" start= auto DisplayName= "FileRelay File Service"

# Dienst starten
sc.exe start FileRelay
```

Für Updates `sc.exe stop FileRelay`, neue Binärdateien kopieren und anschließend wieder starten.

## Kommunikation UI ↔ Service

UI und Service tauschen Informationen über eine lokale Named Pipe (Standard: `net.pipe://localhost/FileRelay`) aus. Befehle/Antworten sind JSON-formatiert.

- `get-status`: Laufzeitstatus (Quellen, Queue-Länge, Zeitstempel)
- `get-configuration`: Aktuelle JSON-Konfiguration
- `apply-configuration`: Persistiert neue Konfiguration, rotiert Credentials und aktualisiert Watcher

## Logging & Monitoring

- Serilog schreibt in `%ProgramData%\FileRelay\logs\filrelay-service.log`
- Logs rotieren täglich, Aufbewahrung 30 Tage (konfigurierbar)
- UI besitzt Exportfunktion für Logs & Queue-Snapshot
- Kritische Fehler können optional per E-Mail oder Windows Event Log weitergeleitet werden (Hook vorgesehen)

## Sicherheit

- Alle Netzwerkzugriffe laufen unter angegebenen Domain-Credentials via `WNetAddConnection2`
- Credentials werden ausschließlich DPAPI-verschlüsselt in der JSON-Konfiguration gespeichert
- Optionale Credential-Rotation via UI
- Whitelisting von Zielpfaden kann über zusätzliche Validierungsregeln erweitert werden

## Beispiel: Quelle programmatisch hinzufügen

```csharp
using FileRelay.Core.Configuration;
using FileRelay.Core.Messaging;
using FileRelay.UI.Services;

var client = new ManagementClient("net.pipe://localhost/FileRelay");
var config = await client.GetConfigurationAsync(CancellationToken.None);

var source = new SourceConfiguration
{
    Name = "Scanner 01",
    Path = @"C:\\Scanner\\Out",
    Recursive = true,
    Targets =
    {
        new TargetConfiguration
        {
            Name = "DMS",
            DestinationPath = @"\\\\fileserver\\eingang",
            CredentialId = config!.Credentials.First().Id,
            ConflictMode = ConflictMode.Replace,
            VerifyChecksum = true,
            SubfolderTemplate = "{date:yyyyMMdd}"
        }
    }
};

config!.Sources.Add(source);
await client.ApplyConfigurationAsync(config, CancellationToken.None);
```

## Lizenz

Dieses Projekt nutzt ausschließlich Open-Source-Abhängigkeiten. Der Quellcode kann innerhalb des Unternehmens erweitert und angepasst werden.
