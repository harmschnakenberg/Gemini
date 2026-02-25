# Gemini

Gemini ist eine Software zur Visualisierung und Steuerung von SPSen (Speicherprogrammierbare Steuerungen) über eine Weboberfläche. 
Sie ermöglicht die gleichzeitige Kommunikation mit verschiedenen SPS-Modellen und bietet eine benutzerfreundliche Oberfläche zur Überwachung und Steuerung von Prozessen.

- ASP.NET WebServer
	- Json Based Web API
	- WebSocket Support
	- keine Auslieferung von statischen Dateien (Bilder, JS, CSS, etc.) über Nginx
	- Statische Sollwert-Bilder
	- Charts zur Visualisierung von Prozessdaten	
	- Chartes mit Echtzeit-Datenaktualisierung
	- Charts mit Werten aus der Datenbank
	- Excel-Export einstellbarer Werte und Zeiträume
	- Excel-Export aus Charts
	- Programm weiter laufen lassen, wenn kein WebClient mehr läuft
	- Charts mit Zoom und Pan Funktionalität
	- Benutzerverwaltung
	- JWT vs. Cookie Authentifizierung => Cookie Authentifizierung 
	- dynamisch erzeugtes, eingeblendetes Sollwerte-Menü 	
	- HTTPS Support über Rechnernamen
	- Neu-einladen von SPS-Configuration im laufenden Betrieb	
	- Mehrbenutzerfähigkeit mit unterschiedlichen Rechten (Admin, User, etc.)
	- Liste der zuletzt geänderten Sollwerte im Webinterface anzeigen
	- Status-Kurven
	- SplashScreen während Charts geladen werden "Browser berechnet Darstellung"
	- Log vom Server auf Weboberfläche
	- Liste der vorhandenen Datenbanken
	- Status-Kurven Achsbeschriftung "Ein" / "Aus" statt "1"/"0"
	- ToDo: Warte-Cursor (Excel Download) bei /excel beim Laden der TagComments 	
	- ToDo: dynamisch erstellte Sollwert-Bilder (aus Json-Config?)
	- ToDo: Alarmierung (E-Mail, SMS, Push-Benachrichtigung)
	- ToDo: SVG Support für Bilder		
	- ToDo: Liste der verbundenen Clients im Webinterface anzeigen	
	- ToDo: Altwert und Neuwert bei Sollwertänderung im Webinterface (Liste) anzeigen
	- ToDo: Hist-Kurven Zusammenstellung in Anlehnung an Excel-Auswahl
	- NiceToHave: Logging mit Source Generated Logging für bessere Performance
	
- S700/1200/1500/300/400 PLC Communication Library
	- TCP/IP Communication mit mehreren SPSen gleichzeitig
	- Multiple Data Types (Bool, Int, DInt, Real, etc.)
	- Meldung nur bei geänderten Werten (Polling)
	- keine doppelten Abfragen bei mehreren Clients
	- ToDo: Unterstützung für OpcUa Kommunikation
			
- SQLite Database
	- SPS Verwaltung in Datenbank und Confifg-Datei (Datenbank mit Vorrang vor Config-Datei)
	- Auslesen über mehrere Datenbankdateien
	- gleichzeitigen Zugriff auf die Datenbank verhindern (lock)
	- WAL (Write Ahead Log) Modus für bessere Performance/weniger Dateikonflikte
