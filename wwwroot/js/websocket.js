const LOGGED_USER = 'userName';
const TOKEN_NAME = 'RequestVerificationToken';
const TICKEDBOX = '☒';
const UNTICKEDBOX = '☐';
let currentCsrfToken = null; // Hier speichern wir das Token global
let lastValOnFocus = null;


function JsonTag(name, value, time) {
    this.N = name;
    this.V = value;
    this.T = time;
}

//function AlertMessage(t, txt) {
//    this.type = t;
//    this.text = txt;
//}

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

        if (inputs[i].classList.contains("checkbox")) {
            inputs[i].setAttribute("readonly", "true"); 
            inputs[i].addEventListener("click", function () {
                if (this.value == TICKEDBOX)
                    this.value = UNTICKEDBOX;
                else
                    this.value = TICKEDBOX;
            });
            inputs[i].addEventListener("blur", function () {   
                //Script zum Schreiben in die SPS anfügen
                updInputEvent(this, lastValOnFocus);
            });
        }

        if (!inputs[i].disabled) {
            //Script zum Schreiben in die SPS anfügen
            inputs[i].onfocus = function () {
                lastValOnFocus = this.value;
                alertSuccess(lastValOnFocus);
            };
            inputs[i].onchange = function () { updInputEvent(this); };            
        }

    }
    return tagNames.map(tagNameToObject);
}

async function updInputEvent(obj) {
    const t = obj.getAttribute('data-name');
    let v = obj.value;
    if (v == TICKEDBOX) v = 1;
    if (v == UNTICKEDBOX) v = 0;

    console.info(`Trigger Änderung ${t} von ${lastValOnFocus} auf ${v}`);
  
    const tag = new JsonTag(t, v, new Date());
    const link = `/tag/write`;
    const res = await fetchSecure(link, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({tagName: t, tagVal: v, oldVal: lastValOnFocus })
    });
    console.info(new URLSearchParams(tag, { oldVal: lastValOnFocus }));

    if (res.ok) {
        const result = await res.json();
        console.log(result.Text);
        message(result.Type, result.Text);
    } else {
        console.error('Wertänderung - Nicht erlaubte Operation - Status ' + res.status);
    }
}

function drawTags(arr) {
    if (arr.length < 1)
        return;

    const inputs = document.querySelectorAll('[data-name]')
    for (let i = 0; i < inputs.length; i++) {
        const tagName = inputs[i].getAttribute('data-name');
        let obj = arr.find(o => o.N === tagName);
        if (obj) {
            if (inputs[i].nodeName == 'INPUT') {
                if (inputs[i].classList.contains("checkbox")) 
                    inputs[i].value = obj.V > 0 ? TICKEDBOX : UNTICKEDBOX;
                else
                    inputs[i].value = obj.V;
            }
            else
                inputs[i].innerHTML = obj.V;
        }
    }
}

function initWebsocket(tags) {
    if (tags.length == 0)
        return;

    const socketUrl = 'wss://' + window.location.host + '/ws';
    const websocket = new WebSocket(socketUrl);

    websocket.onopen = () => {
        console.log('✅ WebSocket-Verbindung hergestellt.');

        const jsonString = JSON.stringify(tags);
        websocket.send(jsonString);
        //console.log('⬆️ Initiales Objekt an Server gesendet:', tags);
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

    const li = document.createElement("li");
    const a = createLink('/', 'Hauptmenü');
    document.getElementById("sidemenu").appendChild(li).appendChild(a);

    //console.info(`Menü JSON: ${json.Sollwerte}`);
    for (var item of json.Sollwerte) {
        //console.info(`${item}, ${item.Id}`)
        const li = document.createElement("li");
        const a = createLink(`/${endpoint}/${item.Id}`, item.Name)           
        document.getElementById("sidemenu").appendChild(li).appendChild(a);
    }
}


function alertSuccess(txt) {
    message('success', txt)
}

function alertWarn(txt) {
    message('warn', txt)
}

function alertError(txt) {
    message('error', txt)
}

function message(adjClass, txt) {
    let alert = document.getElementById('alert');

    if (!alert) {
        alert = document.createElement('span');
        alert.setAttribute('id', 'alert');        
        document.body.insertBefore(alert, document.body.children[0]);
    }

    let msg = document.createElement('div');
    msg.classList.add(adjClass);
    msg.innerHTML = `<b>${txt}</b>`

    setTimeout(function () { msg.remove(); }, 5000);

    alert.appendChild(msg);
}

function createLink(href, display) {
    const a = document.createElement("a");
    a.setAttribute("href", href)
    a.classList.add("menuitem");
    a.innerHTML = display;
    return a;
}

// Funktion zum Überprüfen des Login-Status beim Laden der Seite
function checkLoginStatus() {
    let span = document.getElementById('loginMessage');

    if (!span) {
        span = document.createElement('span');
        span.setAttribute('id', 'loginMessage');        
        span.style.padding = '0.2rem 0.5rem';
        span.style.border = '1px solid grey';
        span.style.borderRadius = '0.5rem';

        const a = document.createElement('a');
        a.setAttribute('href', '/');        
        a.appendChild(span);

        document.body.appendChild(a);
    }

    const urlParams = new URLSearchParams(window.location.search);
    if (urlParams.has('auth') || urlParams.get('auth') == 'failed') { 
        sessionStorage.removeItem(LOGGED_USER);
        console.log("SessionStorage bereinigt.");
    }

    const loggedUser = sessionStorage.getItem(LOGGED_USER);
    if (loggedUser) {
        span.innerHTML = loggedUser;
        span.style.backgroundcolor = 'lawngreen';
    } else {
        span.textContent = 'Kein Benutzer';
        span.style.color = 'grey';

        //fetchSecure("/logout");
    }
}

/* Holt ein frisches Token vom Server */
async function refreshCsrfToken() {
    try {
        const response = await fetch('/antiforgery/token', { method: 'GET' });
        if (response.ok) {
            //const data = await response.json();
            //currentCsrfToken = data.token;
            console.log("🔐 Token erneuert."); //, currentCsrfToken
            return true;
        }
    } catch (e) {
        console.error("🔓 Konnte Token nicht erneuern.", e);
    }
    return false;
}

// Ein Wrapper um fetch, der CSRF automatisch handhabt
async function fetchSecure(url, options = {}) {
    // Sicherstellen, dass wir überhaupt ein Token haben (z.B. beim ersten Start)
    if (!currentCsrfToken) {
        await refreshCsrfToken();
    }

    // Standard-Header vorbereiten
    if (!options.headers) options.headers = {};

    // Content-Type setzen, falls JSON gesendet wird
    if (!(options.body instanceof FormData) && !options.headers['Content-Type']) {
        options.headers['Content-Type'] = 'application/json';
    }

    // CSRF Token in Header packen
    options.headers['RequestVerificationToken'] = currentCsrfToken;

    // 1. Versuch: Request senden
    let response = await fetch(url, options);

    // Wenn der Server 400 Bad Request meldet, könnte das Token abgelaufen sein.
    // (ASP.NET Core Antiforgery gibt 400 zurück, wenn das Token nicht stimmt)
    if (response.status === 400) {
        console.warn("400 Fehler erhalten - Versuche Token Refresh...");

        // Versuchen, Token zu erneuern
        const refreshed = await refreshCsrfToken();

        if (refreshed) {
            // Token im Header aktualisieren
            options.headers['RequestVerificationToken'] = currentCsrfToken;

            // 2. Versuch: Request wiederholen
            response = await fetch(url, options);
        }
    }
    /*else
        console.log(`${url} => ${response.status}`) */

    return response;
}


window.onload = () => {
    initUnits();
    checkLoginStatus();
    initWebsocket(initTags());
    loadMenu('soll', '/soll/sollmenu.json');
}

              
                    