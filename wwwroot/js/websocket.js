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
            if (inputs[i].nodeName == 'INPUT')
                inputs[i].value = obj.V;
            else
                inputs[i].innerHTML = obj.V;
        }
    }
}

function initWebsocket(tags) {
    if (tags.length == 0)
        return;

    const socketUrl = 'ws://' + window.location.host + '/ws';
    const websocket = new WebSocket(socketUrl);

    websocket.onopen = () => {
        console.log('✅ WebSocket-Verbindung hergestellt.');

        const jsonString = JSON.stringify(tags);
        websocket.send(jsonString);
        console.log('⬆️ Initiales Objekt an Server gesendet:', tags);
    };

    websocket.onmessage = (event) => {
        // Empfangene Daten (Text) als JSON-Objekt parsen
        try {
            const updatedObject = JSON.parse(event.data);
            //console.log('⬇️ Update vom Server empfangen:', updatedObject);
            drawTags(updatedObject);
        } catch (e) {
            console.error('Fehler beim Parsen der Nachricht:', e);
        }
    };

    websocket.onclose = (event) => {
        if (event.wasClean) {
            console.log(`❌ Verbindung sauber geschlossen, Code=${event.code} Grund=${event.reason}`);
        } else {
            console.error('❌ Verbindung unerwartet unterbrochen. ' + event.error);
        }
    };

    websocket.onerror = (error) => {
        console.error('⚠️ WebSocket-Fehler:', error);
    };
}

async function loadMenu(endpoint, path) {
    const logo = "<svg id='logo'><style> svg { width:35px; height:35px; background-color: #ddd; position:absolute; right:2px; bottom:2px; margin:2px;}</style>" +
        "<line x1='0' y1='0' x2='0' y2='35' style='stroke:darkcyan;stroke-width:2'></line>" +
        "<polygon points='10,0 10,15 25,0' style='fill:#00004d;'></polygon>" +
        "<polygon points='10,20 10,35 25,35' style='fill:#00004d;'></polygon>" +
        "<polygon points='20,17 37,0 37,35' style='fill:darkcyan;'></polygon>" +
        "</svg>"

    const nav = document.createElement("ul");
    nav.id = "sidemenu";
    nav.innerHTML = logo;
    document.body.insertBefore(nav, document.body.children[0]);

    let file = await fetch(path);
    let text = await file.text();
    //console.info(`Menü JSON: ${text}`);
    const json = JSON.parse(text);

    for (var item of json.Sollwerte) {
        const li = document.createElement("li");
        const a = document.createElement("a");
        a.setAttribute("href", `/menu/${endpoint}/${item.id}`)
        //a.setAttribute("href", `/html/soll/${item.link}`);
        a.classList.add("menuitem");        
        a.innerHTML = item.name;

        document.getElementById("sidemenu").appendChild(li).appendChild(a);
    }
}

// Token-Schlüssel konstant halten
const TOKEN_KEY = 'accessToken';
const LOGGED_USER = 'userName';

// Funktion zum Überprüfen des Login-Status beim Laden der Seite
function checkLoginStatus() {
    let span = document.getElementById('loginMessage');

    if (!span) {
        span = document.createElement("span");
        span.setAttribute('id', 'loginMessage');
        document.body.appendChild(span);
    }

    if (sessionStorage.getItem(TOKEN_KEY)) {
        span.innerHTML = sessionStorage.getItem(LOGGED_USER);
        span.style.backgroundcolor = 'lawngreen';
    } else {
        span.textContent = 'Kein Benutzer';
        span.style.color = 'grey';
    }
}

window.onload = () => {
    initUnits();
    checkLoginStatus();
    initWebsocket(initTags());
    loadMenu('soll', '/js/sollmenu.json');
}
