const socketUrl = 'ws://' + window.location.host + '/ws';

// Initiales JavaScript-Objekt
const initialDataObject = {
    counter: 0,
    message: "Initial request from client",
    serverTime: new Date().toISOString() // Wird vom Server aktualisiert
};

const websocket = new WebSocket(socketUrl);

websocket.onopen = () => {
    console.log('✅ WebSocket-Verbindung hergestellt.');

    // Objekt als JSON-String an den Server senden
    const jsonString = JSON.stringify(initialDataObject);
    websocket.send(jsonString);
    console.log('⬆️ Initiales Objekt an Server gesendet:', initialDataObject);
};

websocket.onmessage = (event) => {
    // Empfangene Daten (Text) als JSON-Objekt parsen
    try {
        const updatedObject = JSON.parse(event.data);
        console.log('⬇️ Update vom Server empfangen:', updatedObject);
    } catch (e) {
        console.error('Fehler beim Parsen der Nachricht:', e);
    }
};

websocket.onclose = (event) => {
    if (event.wasClean) {
        console.log(`❌ Verbindung sauber geschlossen, Code=${event.code} Grund=${event.reason}`);
    } else {
        console.error('❌ Verbindung unerwartet unterbrochen.');
    }
};

websocket.onerror = (error) => {
    console.error('⚠️ WebSocket-Fehler:', error);
};