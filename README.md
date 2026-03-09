# Gemini

Gemini ist eine Software zur Visualisierung und Steuerung von SPSen (Speicherprogrammierbare Steuerungen) über eine Weboberfläche. 
Sie ermöglicht die gleichzeitige Kommunikation mit verschiedenen SPS-Modellen und bietet eine benutzerfreundliche Oberfläche zur Überwachung und Steuerung von Prozessen.

- ASP.NET WebServer
  - intern
	- Json Based Web API
	- WebSocket Support	
	- keine Auslieferung von statischen Dateien (Bilder, JS, CSS, etc.) direkt über Nginx 
	- Programm weiter laufen lassen, wenn kein WebClient mehr läuft
	- Neu-einladen von SPS-Configuration im laufenden Betrieb		
	- Log vom Server auf Weboberfläche
	- Liste der vorhandenen Datenbanken
	- Liste der Lesefehler aus der SPS-Kommunikation
     - NiceToHave: Logging mit Source Generated Logging für bessere Performance
	 - ToDo: Liste der verbundenen Clients im Webinterface anzeigen		
	
	- Sicherheit
	  - HTTPS Support über Rechnernamen | avahi-daemon notwendig
	  - Zertifikat Download über Webinterface? -> braucht man nicht, da Zertifikate in der Regel auf dem Server installiert werden und nicht vom Client heruntergeladen werden müssen.
	  - JWT vs. Cookie Authentifizierung => Cookie Authentifizierung 
	
	- Benutzerverwaltung
	  - Mehrbenutzerfähigkeit mit unterschiedlichen Rechten (Admin, User, etc.)
	  
	- Sollwerte
	  - Statische Sollwert-Bilder
	  - dynamisch erzeugtes, eingeblendetes Sollwerte-Menü 		
	  - Liste der zuletzt geänderten Sollwerte im Webinterface anzeigen	
	  - Altwert und Neuwert bei Sollwertänderung im Webinterface (Liste) anzeigen	
	   - ToDo: dynamisch erstellte Sollwert-Bilder (aus Json-Config?)
	   - ToDo: Filter bei Liste Sollwertänderungen
		
	- Datenexport
	  - Excel-Export einstellbarer Werte und Zeiträume
	  - Excel-Export aus Charts
	   - ToDo: Warte-Cursor (Excel Download) bei /excel beim Laden der TagComments 	
	
	- Kurvendarstellug	
	  - Charts zur Visualisierung von Prozessdaten	
	  - Chartes mit Echtzeit-Datenaktualisierung | wieder entfernt
	  - Charts mit Werten aus der Datenbank
	  - Status-Kurven
	  - Status-Kurven Achsbeschriftung "Ein" / "Aus" statt "1"/"0"	 
	  - SplashScreen während Charts geladen werden "Browser berechnet Darstellung"
	  - Charts mit Zoom und Pan Funktionalität
	  - dynamisch erstellte Charts aus Json-Config
	  - Chart Zeitspanne in URL codiert
	  - Chart Datenkomprimierung
       - ToDo: Hist-Kurven Zusammenstellung in Anlehnung an Excel-Auswahl
	
	- Ereignisse und Benachrichtigungen 
       - ToDo: Alarmierung (E-Mail, SMS, Push-Benachrichtigung)
	
    - Graphische Darstellung
	 - ToDo: SVG Support für Bilder		
		
- S700/1200/1500/300/400 PLC Communication Library
	- TCP/IP Communication mit mehreren SPSen gleichzeitig
	- Multiple Data Types (Bool, Int, DInt, Real, etc.)
	- Meldung nur bei geänderten Werten (Polling)
	- keine doppelten Abfragen bei mehreren Clients
	- ToDo: Unterstützung für OpcUa Kommunikation
	- NiceToHave: VPN, wenn zwei Netzwerkkarten verbaut sind / nur zum testen?
			
- SQLite Database
	- SPS Verwaltung in Datenbank und Confifg-Datei (Datenbank mit Vorrang vor Config-Datei)
	- Auslesen über mehrere Datenbankdateien
	- gleichzeitigen Zugriff auf die Datenbank verhindern (lock / gepuffertes Schreiben) 
	- WAL (Write Ahead Log) Modus für bessere Performance/weniger Dateikonflikte
