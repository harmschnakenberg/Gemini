# Gemini

Gemini ist eine Software zur Visualisierung und Steuerung von SPSen (Speicherprogrammierbare Steuerungen) über eine Weboberfläche. 
Sie ermöglicht die gleichzeitige Kommunikation mit verschiedenen SPS-Modellen und bietet eine benutzerfreundliche Oberfläche zur Überwachung und Steuerung von Prozessen.

- ASP.NET WebServer
	- Json Based Web API
	- WebSocket Support
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
	- ToDo: dynamische Dashboards
	- ToDo: HTTPS Support über Rechnernamen
	- ToDo: dynamisch erstellte Sollwert-Bilder (aus Json-Config?)
	- ToDo: Alarmierung (E-Mail, SMS, Push-Benachrichtigung)
	- ToDo: SVG Support für Bilder	
	- Neu-einladen von SPS-Configuration im laufenden Betrieb
	- Todo: Logging mit Source Generated Logging für bessere Performance
	
- S700/1200/1500/300/400 PLC Communication Library
	- TCP/IP Communication mit mehreren SPSen gleichzeitig
	- Multiple Data Types (Bool, Int, DInt, Real, etc.)
	- Meldung nur bei geänderten Werten (Polling)
	- ToDo: keine doppelten Abfragen bei mehreren Clients // TESTEN!!
		
- SQLite Database
	- SPS Verwaltung in Datenbank und Confifg-Datei (Datenbank mit Vorrang vor Config-Datei)
	- Auslesen über mehrere Datenbankdateien
	- gleichzeitigen Zugriff auf die Datenbank verhindern (lock)
	- WAL (Write Ahead Log) Modus für bessere Performance/weniger Dateikonflikte
