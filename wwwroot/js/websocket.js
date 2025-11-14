
function JsonTag(name, value, time) {
    this.N = name;
    this.V = value;
    this.T = time;
}

function initUnits() {
    //Form
    const inputs = document.getElementsByTagName('input');

    for (let i = 0; i < inputs.length; i++) {
        if (inputs[i].hasAttribute('data-unit')) {
            const tagUnit = inputs[i].getAttribute('data-unit');
            const para = document.createElement("span");
            const node = document.createTextNode(tagUnit);
            para.appendChild(node);

            inputs[i].parentNode.insertBefore(para, inputs[i].nextSibling);
        }
    }
}

function tagNameToObject(name) {
    return new JsonTag(name, null, new Date());
}

function initTags() {
    const tagNames = [];
    const inputs = document.querySelectorAll('[data-name]')
    for (let i = 0; i < inputs.length; i++) {
        const tagName = inputs[i].getAttribute('data-name');
        if (!tagNames.includes(tagName)) {
            tagNames.push(tagName);
        }
    }
    return tagNames.map(tagNameToObject);
}

function drawTags(arr) {
    if (arr.length < 1)
        return;

    const inputs = document.querySelectorAll('[data-name]')
    for (let i = 0; i < inputs.length; i++) {
        const tagName = inputs[i].getAttribute('data-name');
        let obj = arr.find(o => o.N === tagName);
        if (obj) {
            inputs[i].value = obj.V;
        }
    }
}

function initWebsocket() {

    const socketUrl = 'ws://' + window.location.host + '/ws';
    const websocket = new WebSocket(socketUrl);

    websocket.onopen = () => {
        console.log('✅ WebSocket-Verbindung hergestellt.');

        // Objekt als JSON-String an den Server senden
        const tags = initTags();
        const jsonString = JSON.stringify(tags);
        websocket.send(jsonString);
        console.log('⬆️ Initiales Objekt an Server gesendet:', tags);
    };

    websocket.onmessage = (event) => {
        // Empfangene Daten (Text) als JSON-Objekt parsen
        try {
            const updatedObject = JSON.parse(event.data);
            console.log('⬇️ Update vom Server empfangen:', updatedObject);
            drawTags(updatedObject);
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
}

window.onload = () => {
    initUnits();
    initWebsocket();
}
    


