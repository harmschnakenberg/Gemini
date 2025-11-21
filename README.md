# Gemini

Gemini ist eine Software zur Visualisierung und Steuerung von SPSen (Speicherprogrammierbare Steuerungen) über eine Weboberfläche. 
Sie ermöglicht die gleichzeitige Kommunikation mit verschiedenen SPS-Modellen und bietet eine benutzerfreundliche Oberfläche zur Überwachung und Steuerung von Prozessen.

- ASP.NET WebServer
	- Json Based Web API
	- WebSocket Support
	- Statische Sollwert-Bilder
	- Charts zur Visualisierung von Prozessdaten	
	- Chartes mit Echtzeit-Datenaktualisierung
	- Todo: Charts mit Zoom und Pan Funktionalität
	- ToDo: dynamische Dashboards
	- ToDo: HTTPS Support
	- ToDo: JWT Authentifizierung
	- ToDo: dynamisch erstellte Sollwert-Bilder (aus Json-Config?)
	- ToDo: Benutzerverwaltung
	- ToDo: Excel Export
	- ToDo: Alarmierung (E-Mail, SMS, Push-Benachrichtigung)
	- ToDo: SVG Support für Bilder
	- ToDo: Programm weiter laufen lassen, wenn kein WebClient mehr läuft
- S700/1200/1500/300/400 PLC Communication Library
	- TCP/IP Communication mit mehreren SPSen gleichzeitig
	- Multiple Data Types (Bool, Int, DInt, Real, etc.)
	- Meldung nur bei geänderten Werten (Polling)
	- ToDo: keine doppelten Abfragen bei mehreren Clients 
		
- SQLite Database
	- SPS Verwaltung in Datenbank?
	- ToDo: Auslesen über mehrere Datenbankdateien
