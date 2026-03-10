
//const myCharts = new Map();
//const workers = new Map();
let lastValOnFocus = null;
let tagCollections = [];

/* Module laden */
import loadSiteMenu from '../module/sitemenu.js';
import plcUpdate from '../module/plc.js';
import fetchSecure from '../module/fetch.js';
import * as user from '../module/user.js';
import * as data from '../module/data.js';
import * as alert from '../module/alert.js';
import * as excel from '../module/export.js';
import * as dragdrop from '../module/dragdrop.js';
import * as chart from '../js/chart.js';

/* Module in HTML bereitstellen */
window.user = user;
window.plcUpdate = plcUpdate;
window.excel = excel;
window.dragdrop = dragdrop;
window.chart = chart;

/* Initiale Aufrufe */
user.checkLoginStatus();
data.initUnits();
data.initWebsocket(data.initTags());
loadSiteMenu('soll', '/html/soll/sollmenu.json');

document.addEventListener('DOMContentLoaded', (event) => {
    // Initialisierung der Drag-and-Drop-Funktionalität für vorhandene Elemente
    dragdrop.setupDragAndDrop();
});


//window.onload = () => {

    //checkLoginStatus();
   /* initUnits();*/
    //let localTags = initTags();
    //console.info(localTags);
    //if (localTags && localTags.length > 0)
    //    initWebsocket(localTags);
//    loadMenu('soll', '/html/soll/sollmenu.json');
    //list = document.getElementById('sortable-list');


    //document.addEventListener('DOMContentLoaded', (event) => {
    //    // Initialisierung der Drag-and-Drop-Funktionalität für vorhandene Elemente
    //    setupDragAndDrop();
    //});
//}



//async function login() {
//    const body = document.getElementsByTagName('BODY')[0];
//    const el = document.getElementById('loginMessage');
//    el.textContent = 'Logge ein...';

//    const userName = document.getElementById("username").value;
//    const userToken = document.getElementById("password").value;

//    try {
//        body.style.cursor = 'wait';
//        const response = await fetchSecure(`${API_URL}/login`, {
//            method: "POST",
//            headers: { "Content-Type": "application/json" },
//            body: JSON.stringify({ userName, userToken })
//        });

//        if (response.ok) {
//            const data = await response.json();
//            const csrfToken = data.userToken;
//            sessionStorage.setItem(TOKEN_NAME, csrfToken);
//            sessionStorage.setItem(LOGGED_USER, userName);
//            el.textContent = userName;
//            el.style.color = 'green';
//        } else {
//            el.textContent = 'Login fehlgeschlagen. Überprüfe Anmeldedaten.';
//            el.style.color = 'red';
//        }
//    } catch (e) { console.error("Fehler beim Login: " + e); }
//    finally {
//        body.style.cursor = 'auto';
//    }
//}

//async function logout() {
//    const body = document.getElementsByTagName('BODY')[0];
//    const el = document.getElementById('loginMessage');
//    el.textContent = 'Logge aus...';
//    body.style.cursor = 'wait';
//    const userName = sessionStorage.getItem(LOGGED_USER);
//    const userToken = 'FantasieToken';
//    const response = await fetchSecure(`${API_URL}/logout`, {
//        method: "POST",
//        headers: { "Content-Type": "application/json" },
//        body: JSON.stringify({ userName, userToken })
//    });

//    if (response.ok) {
//        if (userName) {
//            el.textContent = userName + ' ausgeloggt';
//            el.style.color = 'red';
//            sessionStorage.removeItem(TOKEN_NAME);
//            sessionStorage.removeItem(LOGGED_USER);
//        }
//    }
//    else {
//        el.textContent = `'${userName}' ausloggen fehlgeschlagen. Status ${response.status}`;
//        el.style.color = 'orange';
//    }

//    body.style.cursor = 'auto';
//}



//// Funktion zum Überprüfen des Login-Status beim Laden der Seite
//function checkLoginStatus() {
//    let span = document.getElementById('loginMessage');

//    if (!span) {
//        span = document.createElement('span');
//        span.setAttribute('id', 'loginMessage');
//        span.style.padding = '0.2rem 0.5rem';
//        span.style.border = '1px solid grey';
//        span.style.borderRadius = '0.5rem';

//        const a = document.createElement('a');
//        a.setAttribute('href', '/');
//        a.appendChild(span);

//        document.body.appendChild(a);
//    }

//    const urlParams = new URLSearchParams(window.location.search);
//    if (urlParams.has('auth') || urlParams.get('auth') == 'failed') {
//        sessionStorage.removeItem(LOGGED_USER);
//        console.log("SessionStorage bereinigt.");
//    }

//    const loggedUser = sessionStorage.getItem(LOGGED_USER);
//    if (loggedUser) {
//        span.innerHTML = loggedUser;
//        span.style.backgroundcolor = 'lawngreen';
//    } else {
//        span.textContent = 'Kein Benutzer';
//        span.style.color = 'grey';
//        fetchSecure("/logout")
//    }
//}


//async function loadMenu(endpoint, path) {
//    const logo = "<svg id='logo'><style> svg { width:35px; height:35px; background-color: #ddd; position:absolute; right:2px; bottom:2px; margin:2px;}</style>" +
//        "<line x1='0' y1='0' x2='0' y2='35' style='stroke:darkcyan;stroke-width:2'></line>" +
//        "<polygon points='10,0 10,15 25,0' style='fill:#00004d;'></polygon>" +
//        "<polygon points='10,20 10,35 25,35' style='fill:#00004d;'></polygon>" +
//        "<polygon points='20,17 37,0 37,35' style='fill:darkcyan;'></polygon>" +
//        "</svg>"

//    const nav = document.createElement("ul");
//    nav.id = "sidemenu";
//    nav.innerHTML = logo;
//    document.body.insertBefore(nav, document.body.children[0]);

//    let file = await fetch(path);
//    let text = await file.text();
//    //console.info(`Menü JSON: ${text}`);
//    const json = JSON.parse(text);

//    const li = document.createElement("li");
//    const a = createLink('/', 'Hauptmenü');
//    document.getElementById("sidemenu").appendChild(li).appendChild(a);

//    //console.info(`Menü JSON: ${json.Sollwerte}`);
//    for (var item of json.Sollwerte) {
//        //console.info(`${item}, ${item.Id}`)
//        const li = document.createElement("li");
//        const a = createLink(`/${endpoint}/${item.Id}`, item.Name)
//        document.getElementById("sidemenu").appendChild(li).appendChild(a);
//    }
//}

//function createLink(href, display) {
//    const a = document.createElement("a");
//    a.setAttribute("href", href)
//    a.classList.add("menuitem");
//    a.innerHTML = display;
//    return a;
//}

//function alertMessage(adjClass, txt) {
//    let alert = document.getElementById('alert');

//    if (!alert) {
//        alert = document.createElement('span');
//        alert.setAttribute('id', 'alert');
//        document.body.insertBefore(alert, document.body.children[0]);
//    }

//    let msg = document.createElement('div');
//    msg.classList.add(adjClass);
//    msg.innerHTML = `<b>${txt}</b>`

//    setTimeout(function () { msg.remove(); }, 5000);

//    alert.appendChild(msg);
//}

//function alertSuccess(txt) {
//    alertMessage('success', txt)
//}

//function alertWarn(txt) {
//    alertMessage('warn', txt)
//}

//function alertError(txt) {
//    alertMessage('error', txt)
//}

//// Ein Wrapper um fetch, der CSRF automatisch handhabt
//async function fetchSecure(url, options = {}) {
//    // Sicherstellen, dass wir überhaupt ein Token haben (z.B. beim ersten Start)
//    if (!currentCsrfToken) {
//        await refreshCsrfToken();
//    }

//    // Standard-Header vorbereiten
//    if (!options.headers) options.headers = {};

//    // Content-Type setzen, falls JSON gesendet wird
//    if (!(options.body instanceof FormData) && !options.headers['Content-Type']) {
//        options.headers['Content-Type'] = 'application/json';
//    }

//    // CSRF Token in Header packen
//    options.headers['RequestVerificationToken'] = currentCsrfToken;

//    // 1. Versuch: Request senden
//    let response = await fetch(url, options);

//    // Wenn der Server 400 Bad Request meldet, könnte das Token abgelaufen sein.
//    // (ASP.NET Core Antiforgery gibt 400 zurück, wenn das Token nicht stimmt)
//    if (response.status === 400) {
//        console.warn("400 Fehler erhalten - Versuche Token Refresh...");

//        // Versuchen, Token zu erneuern
//        const refreshed = await refreshCsrfToken();

//        if (refreshed) {
//            // Token im Header aktualisieren
//            options.headers['RequestVerificationToken'] = currentCsrfToken;

//            // 2. Versuch: Request wiederholen
//            response = await fetch(url, options);
//        }
//    }
//    /*else
//        console.log(`${url} => ${response.status}`) */

//    return response;
//}

///* Holt ein frisches Token vom Server */
//async function refreshCsrfToken() {
//    try {
//        const response = await fetch('/antiforgery/token', { method: 'GET' });
//        if (response.ok) {
//            //const data = await response.json();
//            //currentCsrfToken = data.token;
//            console.log("🔐 Token erneuert."); //, currentCsrfToken
//            return true;
//        }
//    } catch (e) {
//        console.error("🔓 Konnte Token nicht erneuern.", e);
//    }
//    return false;
//}

//function JsonTag(name, value, time) {
//    this.N = name;
//    this.V = value;
//    this.T = time;
//}


//function initUnits() {
//    //Form
//    const inputs = document.getElementsByTagName('input');

//    for (let i = 0; i < inputs.length; i++) {
//        if (inputs[i].hasAttribute('data-unit')) {
//            const tagUnit = inputs[i].getAttribute('data-unit');
//            const para = document.createElement("span");
//            const node = document.createTextNode(tagUnit);
//            para.appendChild(node);

//            inputs[i].parentNode.insertBefore(para, inputs[i].nextSibling);
//        }
//    }
//}

//function initTags() {

//    const tagNames = [];
//    const inputs = document.querySelectorAll('[data-name]')
//    for (let i = 0; i < inputs.length; i++) {
//        const tagName = inputs[i].getAttribute('data-name');
//        if (!tagNames.includes(tagName)) {
//            tagNames.push(tagName);
//        }

//        if (inputs[i].classList.contains("checkbox")) {
//            inputs[i].setAttribute("readonly", "true");
//            inputs[i].addEventListener("click", function () {
//                if (this.value == TICKEDBOX)
//                    this.value = UNTICKEDBOX;
//                else
//                    this.value = TICKEDBOX;
//            });
//            inputs[i].addEventListener("blur", function () {
//                //Script zum Schreiben in die SPS anfügen
//                updInputEvent(this, lastValOnFocus);
//            });
//        }

//        if (!inputs[i].disabled) {
//            //Script zum Schreiben in die SPS anfügen
//            inputs[i].onfocus = function () {
//                lastValOnFocus = this.value;
//                //alert.test(lastValOnFocus);
//            };
//            inputs[i].onchange = function () { updInputEvent(this); };
//        }

//    }
//    return tagNames.map(tagNameToObject);
//}

//function initWebsocket(tags) {
//    if (tags.length == 0)
//        return;

//    const socketUrl = 'wss://' + window.location.host + '/ws';
//    const websocket = new WebSocket(socketUrl);

//    websocket.onopen = () => {
//        console.log('✅ WebSocket-Verbindung hergestellt.');

//        const jsonString = JSON.stringify(tags);
//        websocket.send(jsonString);
//        //console.log('⬆️ Initiales Objekt an Server gesendet:', tags);
//    };

//    websocket.onmessage = (event) => {
//        // Empfangene Daten (Text) als JSON-Objekt parsen
//        try {
//            const updatedObject = JSON.parse(event.data);
//            //console.log('⬇️ Update vom Server empfangen:', updatedObject);
//            drawTags(updatedObject);
//        } catch (e) {
//            console.error('Fehler beim Parsen der Nachricht:', e);
//        }
//    };

//    websocket.onclose = (event) => {
//        if (event.wasClean) {
//            console.log(`❌ Verbindung sauber geschlossen, Code=${event.code} Grund=${event.reason}`);
//        } else {
//            console.error('❌ Verbindung unerwartet unterbrochen. ' + event.error);
//        }
//    };

//    websocket.onerror = (error) => {
//        console.error('⚠️ WebSocket-Fehler:', error);
//    };
//}

//function tagNameToObject(name) {
//    return new JsonTag(name, null, new Date());
//}

//async function updInputEvent(obj) {
//    const t = obj.getAttribute('data-name');
//    let v = obj.value;
//    if (v == TICKEDBOX) v = 1;
//    if (v == UNTICKEDBOX) v = 0;

//    console.info(`Trigger Änderung ${t} von ${lastValOnFocus} auf ${v}`);

//    const tag = new JsonTag(t, v, new Date());
//    const link = `/tag/write`;
//    const res = await fetchSecure(link, {
//        method: 'POST',
//        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
//        body: new URLSearchParams({ tagName: t, tagVal: v, oldVal: lastValOnFocus })
//    });
//    console.info(new URLSearchParams(tag, { oldVal: lastValOnFocus }));

//    if (res.ok) {
//        const result = await res.json();
//        console.log(result.Text);
//        alertMessage(result.Type, result.Text);
//    } else {
//        console.error('Wertänderung - Nicht erlaubte Operation - Status ' + res.status);
//    }
//}

//function drawTags(arr) {
//    if (arr.length < 1)
//        return;

//    const inputs = document.querySelectorAll('[data-name]')
//    for (let i = 0; i < inputs.length; i++) {
//        const tagName = inputs[i].getAttribute('data-name');
//        let obj = arr.find(o => o.N === tagName);
//        if (obj) {
//            if (inputs[i].nodeName == 'INPUT') {
//                if (inputs[i].classList.contains("checkbox"))
//                    inputs[i].value = obj.V > 0 ? TICKEDBOX : UNTICKEDBOX;
//                else
//                    inputs[i].value = obj.V;
//            }
//            else
//                inputs[i].innerHTML = obj.V;
//        }
//    }
//}



////Manipulation der CPU-Eigenschaften
////Verwendung: /source
//async function updPlc(verb, obj) {
//    const plcName = obj.parentNode.parentNode.children[0].children[0].value;
//    const plcType = obj.parentNode.parentNode.children[1].children[0].value;
//    const plcIp = obj.parentNode.parentNode.children[2].children[0].value;
//    const plcRack = obj.parentNode.parentNode.children[3].children[0].value;
//    const plcSlot = obj.parentNode.parentNode.children[4].children[0].value;
//    const plcIsActive = obj.parentNode.parentNode.children[5].children[0].checked;
//    const plcComm = obj.parentNode.parentNode.children[6].children[0].value;
//    const plcId = obj.parentNode.parentNode.children[7].children[0].value;

//    //document.getElementsByTagName('h1')[0].innerHTML = `Id ${plcId}, ${plcName}, ${plcType}, ${plcIp}, ${plcRack}, ${plcSlot}, ${plcIsActive}, ${plcComm}|`;
//    try {
//        //const alert = import('../module/alert.js');
//        //const ws = await import('../module/fetch.js');
//        const res = await fetchSecure('/source/' + verb, {
//            method: 'POST',
//            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
//            body: new URLSearchParams({
//                plcId: plcId,
//                plcName: plcName,
//                plcType: plcType,
//                plcIp: plcIp,
//                plcRack: plcRack,
//                plcSlot: plcSlot,
//                plcIsActive: plcIsActive,
//                plcComm: plcComm
//            })
//        });

//        if (!res.ok)
//            alertError('Datenquellenverwaltung - Nicht erlaubte Operation - Status ' + res.status);
//        else {
//            const data = await res.json();

//            console.log(data);
//            if (data.Type == 'reload') {
//                alertSuccess(`Operation ${verb} erfolgreich. ${data.Text}`);
//                setTimeout(location.reload(), 5000);
//            }
//            else
//                alertMessage(data.Type, data.Text);
//        }

//    } catch (error) {
//        console.error('Fehler beim Abrufen: ', error);
//    }

//}

////Benuterdaten von Tabellenreihe in Formular kopieren
//function getUserData(row) {

//    const username = row.children[0].children[0].value;
//    const userrole = row.children[1].children[0].value;
//    const userid = row.children[2].children[0].value;

//    document.getElementById('userid').value = userid;
//    document.getElementById('username').value = username;
//    document.getElementById('role').value = userrole;
//}

//async function updateUser(verb) {
//    const userid = document.getElementById('userid').value
//    const username = document.getElementById('username').value;
//    const userrole = document.getElementById('role').value;
//    const userpwd = document.getElementById('pwd').value;

//    try {
//        //const ws = await import('../module/fetch.js');
//        const res = await fetchSecure('/user/' + verb, {
//            method: 'POST',
//            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
//            body: new URLSearchParams({ id: userid, name: username, role: userrole, pwd: userpwd })
//        });

//        if (res.ok) {
//            location.reload();
//        } else {
//            alert('Benuterverwaltung - Nicht erlaubte Operation - Status ' + res.status);
//        }
//    } catch (error) {
//        console.error(error.message);
//    }
//}


//import * as excel from '../js/excel.js';


//function TagCollection(id, name, author, start, end, interval, tags) {
//    //   public record TagCollection(int Id, string Name, string Author, DateTime Start, DateTime End, int Interval, Tag[] Tags);
//    this.Id = id;
//    this.Name = name;
//    this.Author = author;
//    this.Start = start;
//    this.End = end;
//    this.Interval = interval;
//    this.Tags = tags;
//}

//function setupDragAndDrop() {
//    // Event Listener für alle sortierbaren Elemente hinzufügen
//    document.querySelectorAll('.sortable-item').forEach(item => {
//        item.addEventListener('dragstart', handleDragStart);
//        item.addEventListener('dragend', handleDragEnd);
//        item.addEventListener('dragover', handleDragOver);
//        // dragenter und dragleave sind nützlich für visuelles Feedback,
//        // aber dragover reicht für die Sortierung.
//    });
//}

//function handleDragStart(e) {
//    draggingItem = e.target;
//    e.dataTransfer.effectAllowed = 'move';
//    // Verzögert das Hinzufügen der dragging Klasse, damit ein "Ghost"-Bild entsteht
//    setTimeout(() => {
//        e.target.classList.add('dragging');
//    }, 0);
//}

//function handleDragEnd(e) {
//    e.target.classList.remove('dragging');
//    draggingItem = null;
//    // Entfernt alle 'over' Klassen von allen Elementen am Ende des drags
//    document.querySelectorAll('.sortable-item').forEach(item => {
//        item.classList.remove('over');
//    });
//}

//function handleDragOver(e) {
//    e.preventDefault(); // Notwendig, damit das drop Event funktioniert
//    const afterElement = getDragAfterElement(list, e.clientY);

//    // Visuelles Feedback hinzufügen
//    document.querySelectorAll('.sortable-item').forEach(item => {
//        item.classList.remove('over');
//    });
//    if (afterElement) {
//        afterElement.classList.add('over');
//    }

//    // Element an der neuen Position einfügen
//    if (afterElement == null) {
//        list.appendChild(draggingItem);
//    } else {
//        list.insertBefore(draggingItem, afterElement);
//    }
//}

//// Hilfsfunktion zur Ermittlung des Elements, nach dem das gezogene Element platziert werden soll
//function getDragAfterElement(container, y) {
//    const draggableElements = [...container.querySelectorAll('.sortable-item:not(.dragging)')];

//    return draggableElements.reduce((closest, child) => {
//        const box = child.getBoundingClientRect();
//        const offset = y - box.top - box.height / 2;
//        // Wenn der Offset kleiner 0 ist und näher an der Mitte als das bisherige "closest" Element...
//        if (offset < 0 && offset > closest.offset) {
//            return { offset: offset, element: child };
//        } else {
//            return closest;
//        }
//    }, { offset: Number.NEGATIVE_INFINITY }).element;
//}


//// --- Funktionen zum Hinzufügen und Löschen von Elementen ---

//function addItem() {
//    const input = document.getElementById('newItemInput');
//    const value = input.value.trim();

//    if (value !== '') {
//        createListItem(value);
//        input.value = '';
//    }
//}

//function createListItem(text) {
//    const li = document.createElement('li');
//    li.classList.add('sortable-item');
//    li.setAttribute('draggable', true); // Macht das Element drag-fähig

//    const inputField = document.createElement('input');
//    inputField.setAttribute('type', 'text');
//    inputField.setAttribute('list', 'comments');
//    inputField.setAttribute('id', 'c' + document.getElementsByTagName('li').length);
//    inputField.value = text;
//    inputField.classList.add('myButton');
//    inputField.setAttribute('readonly', 'readonly');
//    inputField.style.width = '91%';
//    inputField.addEventListener('blur', function () { isValid(this) });

//    const deleteBtn = document.createElement('button');
//    deleteBtn.textContent = 'Löschen';
//    deleteBtn.classList.add('delete-btn');
//    deleteBtn.onclick = function () {
//        li.remove(); // Löscht das Listenelement direkt
//    };

//    li.appendChild(inputField);
//    li.appendChild(deleteBtn);
//    list.appendChild(li);

//    // Wichtig: Die Drag-and-Drop-Listener müssen für das neue Element registriert werden
//    li.addEventListener('dragstart', handleDragStart);
//    li.addEventListener('dragend', handleDragEnd);
//    li.addEventListener('dragover', handleDragOver);

//    isValid(inputField);
//}

//function isValid(input) {
//    const ops = document.querySelectorAll('#comments > OPTION');
//    input.style.backgroundColor = 'white';

//    for (let i = 0; i < ops.length; i++) {
//        if (ops[i].value == input.value)
//            return;
//    }

//    input.style.backgroundColor = 'rgba(255, 99, 71, 0.5)';
//}

//async function loadOptions(selectId, link) {

//    document.body.style.opacity = "0.5";

//    //const ws = await import('../module/fetch.js');
//    const response = await fetchSecure(link, {
//        method: 'POST'
//    });

//    if (!response.ok) {
//        throw new Error(`Antwort status: ${response.status}`);
//        return;
//    }

//    const json = await response.json();

//    json.forEach((t) => {
//        allTagComments.set(t.V?.length > 3 ? t.V : t.N, t.N);
//        const para = document.createElement("OPTION");
//        //console.info(`# ${t.V}=${t.N}`);
//        para.setAttribute("value", t.V?.length > 3 ? t.V : t.N);
//        document.getElementById(selectId).appendChild(para);
//    });
//    document.body.style.opacity = "1";
//    // document.body.style.cursor = "auto";
//}

//function setDatesToStartOfMonth(startId, endId) {
//    var now = new Date();
//    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
//    document.getElementById(endId).value = now.toISOString().slice(0, 16);

//    var begin = new Date();
//    begin.setUTCMonth(begin.getUTCMonth() - 1, 1);
//    begin.setUTCHours(0, 0, 0);
//    document.getElementById(startId).value = begin.toISOString().slice(0, 16);
//}

/* Download DB-Dateien */
//async function getDbFromForm(startId, endId) {

//    document.body.style.cursor = "wait";
//    document.body.style.opacity = "0.5";
//    const s = new Date(document.getElementById(startId).value);
//    const e = new Date(document.getElementById(endId).value);
//    let filename = 'db.zip';

//    //const ws = await import('../module/fetch.js');
//    await fetchSecure('/db/download', {
//        method: 'POST',
//        headers: {
//            'Content-Type': 'application/x-www-form-urlencoded',
//            Accept: 'application/zip'
//        },
//        body: new URLSearchParams({ start: s.toISOString(), end: e.toISOString() })
//    })
//        .then((res) => {
//            //console.info(res.headers);
//            filename = res.headers.get('Content-Disposition').split('filename=')[1].split(";")[0];
//            return res.blob();
//        })
//        .then(blob => URL.createObjectURL(blob))
//        .then(url => {
//            var link = document.createElement('a');
//            link.download = filename;
//            link.href = url;
//            link.click();
//        });

//    document.body.style.opacity = "1";
//    document.body.style.cursor = "auto";
//}

//function getExcelFromForm() {

//    document.body.style.cursor = "wait";
//    document.body.style.opacity = "0.5";

//    const inputs = document.querySelectorAll('.sortable-item > input');
//    const interval = document.getElementById('interval').value;
//    const tags = new Map();

//    for (let i = 0; i < inputs.length; i++) {
//        const comment = inputs[i].value;
//        const tagName = allTagComments.get(comment);
//        //console.info(`${allTagComments.size} hinzufügen: ${tagName}=${comment}`)
//        tags.set(tagName, comment);
//    }

//    //for (const x of tags.keys()) {
//    //    console.info(`Tag zur Übergabe: ${x} = ${tags.get(x)}`);
//    //}

//    //console.info(`Übergebene Tags: ${tags.size}`);

//    if (tags.size > 0)
//        excelExport('start', 'end', interval, tags);
//    else
//        console.warn("Keine Tags für Excel-Export ausgewählt.");

//    document.body.style.opacity = "1";
//    document.body.style.cursor = "auto";
//}

//async function excelExport(startId, endId, ival, tags) {

//    const s = new Date(document.getElementById(startId).value);
//    const e = new Date(document.getElementById(endId).value);
//    const arr = [];
//    let filename = 'excel.xlsx';

//    //console.info(`Übergebene Tags: ${tags.size}`);
//    //const ws = await import('../module/fetch.js');

//    tags.forEach(function (value, key) {
//        //console.info('Exportvorbereitung: ' + key + ' = ' + value);
//        const j = new JsonTag(key, value, new Date())
//        arr.push(j);
//    });

//    if (arr.length > 0)
//        await fetchSecure('/excel', {
//            method: 'POST',
//            headers: {
//                'Content-Type': 'application/x-www-form-urlencoded',
//                Accept: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
//            },
//            body: new URLSearchParams({ start: s.toISOString(), end: e.toISOString(), interval: ival, tags: JSON.stringify(arr) })
//        }) //https://stackoverflow.com/questions/44168090/fetch-api-to-force-download-file
//            .then((res) => {
//                try {
//                    filename = res.headers.get('Content-Disposition').split("filename=")[1];
//                    //filename = res.headers.get('Content-Disposition').split("filename*=UTF-8''")[1]; //UTF8-Filename
//                } catch (e) {
//                    console.warn("Fehler beim Auslesen des Dateinamens aus den Response-Headern: " + e.message);
//                }
//                return res.blob();
//            })
//            .then(blob => URL.createObjectURL(blob))
//            .then(url => {
//                var link = document.createElement('a');
//                link.download = filename;
//                link.href = url;
//                link.click();
//            });

//}

//export { loadOptions, setDatesToStartOfMonth, addItem, list };


/* CHARTS */

////Initialisiert das Chart.js Liniendiagramm.
//function initChart(chartId, isStatusChart = false) {
//    const elm = document.getElementById(chartId);

//    if (!elm) {
//        console.warn(`HTML-Element mit Id ${chartId} existiert nicht.`);
//        return;
//    }

//    const zoomOptions = {
//        //limits: {
//        //    x: { min: 'original', max: 200, minRange: 50 },
//        //    y: { min: 'original', max: 200, minRange: 50 }
//        //},
//        pan: {
//            enabled: true,
//            mode: 'x', // Enables horizontal panning
//            scaleMode: 'x',
//        },
//        zoom: {
//            wheel: {
//                enabled: true,
//            },
//            pinch: {
//                enabled: true
//            },
//            mode: 'xy',
//            onZoomComplete({ chart }) {
//                chart.update('none');
//            },
//            scaleMode: 'x'
//        }
//    };

//    let yTicks = {
//        color: '#ffffff'
//    };

//    if (isStatusChart) {
//        yTicks = {
//            color: '#ffffff',
//            stepSize: 1,
//            callback: function (value, index, values) {
//                if (isStatusChart) {
//                    const maxTick = Math.max(...values.map(t => t.value));
//                    switch (value) {
//                        case 0:
//                            return ['Aus', value];
//                        case maxTick:
//                            return ['Ein', value];
//                        default:
//                            return ['Aus', value, 'Ein'];
//                    }
//                }
//                else
//                    return value;
//            }
//        };
//    }

//    const ctx = elm.getContext('2d');

//    let myChart = new Chart(ctx, {
//        type: 'line',
//        data: {
//            labels: [],
//            datasets: []
//        },
//        options: {
//            responsive: true,
//            maintainAspectRatio: false,
//            animation: false,
//            spanGaps: true,
//            // parsing: false, //Geht nicht?
//            scales: {
//                x: {
//                    type: 'time',
//                    //bounds: 'ticks', // Stellt sicher, dass min/max Ticks sichtbar sind
//                    time: {
//                        unit: 'hour',
//                        displayFormats: {
//                            day: 'dd.MM.yyyy HH:mm',
//                            hour: 'HH:mm',
//                            minute: 'HH:mm:ss'
//                        },
//                        tooltipFormat: 'dd.MM.yyyy HH:mm'
//                    },
//                    //min: startDate,
//                    ticks: {
//                        source: 'auto', //'data', //'auto'
//                        autoSkip: true,
//                        //display: true,
//                        //maxTicksLimit: 10, // Zeigt maximal 10 Ticks an
//                        //count: 20,
//                        //minRotation: 90,   
//                        major: { enabled: true },
//                        //stepSize: 0.25,
//                        color: '#ffffff',
//                        z: 1,
//                        //beforeBuildTicks: function(ax){
//                        //   console.log(ax._unit);
//                        //},

//                        //callback: function (value, index, ticks) {
//                        //    if (index === 0 || index === ticks.length - 1) {
//                        //        return this.getLabelForValue(value); 
//                        //    }

//                        //    return this.getLabelForValue(value);
//                        //}

//                        //callback: function (val, index, ticks) {
//                        //    if (ticks.length < 10)
//                        //        return this.getLabelForValue(val);
//                        //    else if (ticks.length < 100)
//                        //        return index % 2 === 0 ? this.getLabelForValue(val) : '';
//                        //    else
//                        //        return index % 10 === 0 ? this.getLabelForValue(val) : '';
//                        //}

//                    },
//                    title: {
//                        display: true,
//                        color: '#ffffff',
//                        text: 'Zeit'
//                    },
//                    grid: {
//                        display: true,
//                        drawTicks: true,
//                        color: '#666666'
//                    }
//                },
//                y: {
//                    title: {
//                        display: true,
//                        position: 'bottom',
//                        color: '#ffffff',
//                        text: 'Wert'
//                    },
//                    //suggestedMax: function (context) {                        
//                    //    //const maxDataSet = context.chart.data.datasets.length;
//                    //    const maxVal = Math.max(...values.map(t => t.value));
//                    //    return maxVal + 1;
//                    //},
//                    ticks: yTicks,
//                    grid: {
//                        display: true,
//                        drawTicks: true,
//                        color: '#555555'
//                    }
//                }
//                //plugins: {
//                //    filler: { propagate: false } // Verhindert das Durchscheinen nach unten
//                //}
//            },
//            plugins: {
//                zoom: zoomOptions,
//                legend: {
//                    labels: {
//                        color: '#ffffff'
//                    },
//                    position: 'bottom',
//                    display: true
//                }//,
//                //decimation: {
//                //    enabled: true
//                //}
//            }
//        }
//    });

//    myCharts.set(chartId, myChart);

//    const worker = new Worker('/js/chartworker.js', { type: "module" });
//    worker.onmessage = workermessage;
//    workers.set(chartId, worker);
//}


///**
// * Sendet die Anfrage an den Web Worker, um Daten zu laden und zu verarbeiten.
// */
//function loadChart(chartId, startId, endId, intervalId, LABEL_ALIASES) {

//    document.body.style.cursor = "wait";
//    document.body.style.opacity = "0.5";
//    document.getElementById('customspinner').style.visibility = 'visible';

//    // Setze die Fortschrittsanzeige zurück und zeige sie an
//    document.getElementById('progressBar').value = 0;
//    document.getElementById('progressText').textContent = '0%';
//    document.getElementById('progressContainer').style.display = 'block';

//    const startDate = new Date(document.getElementById(startId).value);
//    const endDate = new Date(document.getElementById(endId).value);
//    const interval = parseInt(document.getElementById(intervalId).value);

//    const params = new URLSearchParams();

//    if (!startDate || !endDate || isNaN(startDate) || isNaN(endDate)) {
//        alert(`Bitte Start- und Enddatum für ${chartId} auswählen.`);
//        params.delete('start');
//        params.delete('end');
//        return;
//    }

//    const tagnames = Array.from(LABEL_ALIASES.keys());
//    params.set("tagnames", tagnames);

//    if (!params.has('start'))
//        params.append("start", startDate.toISOString());
//    if (!params.has('end'))
//        params.append("end", endDate.toISOString());

//    //if (TimeSpanInDays(startDate, endDate) > 7)
//    //    if (confirm(`Der ausgewählte Zeitraum von ${TimeSpanInDays(startDate, endDate)} Tagen kann zu einer großen Datenmenge führen.\r\nDer Browser könnte längere Zeit zum Berechnen der Darstellung benötigen. Die Daten können auf dem Server komprimiert werden, um die Anzahl der Datenpunkte zu verringern. Daten auf dem Server komprimieren? Abbrechen lädt alle Datenpunkte unkomprimiert.`))
//    if (!isNaN(interval))
//        params.append('interval', interval);

//    const link = `/db?${params}`;

//    if (!document.getElementById(chartId + 'link')) {
//        const aEl = document.createElement('a');
//        aEl.setAttribute("id", chartId + 'link');
//        aEl.setAttribute("href", link);
//        aEl.style.color = 'white';
//        aEl.style.textDecoration = 'none';
//        aEl.style.display = 'block';
//        aEl.innerHTML = 'Rohdaten';
//        //<a id="rawDataLink" style="position:absolute; bottom:0.5rem; left:0.5rem;color:white; text-decoration:none;">Rohdaten</a>
//        document.getElementById('rawDataLinks').appendChild(aEl);
//    }

//    //let colors = theme.CHART_COLORS;

//    // Sende alle notwendigen Daten an den Worker
//    workers.get(chartId).postMessage({
//        chartId: chartId,
//        url: link,
//        aliases: LABEL_ALIASES
//    });
//}

///**
// * Empfängt die verarbeiteten Daten vom Web Worker und aktualisiert das Chart.
// */
//function workermessage(e) {

//    const message = e.data;

//    if (message.type === 'progress') {
//        // Fortschritts-Update verarbeiten
//        const percentage = message.percentage;
//        //     console.log(percentage);
//        progressBar.value = percentage;
//        progressText.textContent = `${percentage}%,  ${message.processedCount}/${message.totalCount} Datensätze`;

//        if (message.processedCount == message.totalCount)
//            document.getElementById(message.chartId + 'link').innerHTML = `Rohdaten [${message.totalCount} Datensätze]`;

//    } else if (message.type === 'complete') {
//        const chartId = message.chartId;
//        const newDatasets = e.data.datasets;
//        const myChart = myCharts.get(chartId);
//        //console.log(`Daten vom Worker für ${chartId} empfangen. ${newDatasets.length} Datensätze.`);

//        // Ersetze die existierenden Datensätze durch die neuen
//        myChart.data.datasets = newDatasets;

//        // Aktualisiere das Diagramm
//        myChart.update('none');
//        progressContainer.style.display = 'none';
//        document.body.style.opacity = "1";
//        document.body.style.cursor = "auto";
//        document.getElementById('customspinner').style.visibility = 'hidden';
//    } else if (message.type === 'error') {
//        // Fehlerbehandlung
//        progressContainer.style.display = 'none';
//        alert(`Fehler beim Laden/Verarbeiten der Daten für ${message.chartId}: ${message.error}`);
//    }
//}

//function setDatesHours(startId, endId, hh) {
//    var now = new Date();
//    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
//    document.getElementById(endId).value = now.toISOString().slice(0, 16);

//    var begin = new Date();
//    begin.setUTCHours(begin.getHours() - hh);
//    document.getElementById(startId).value = begin.toISOString().slice(0, 16);
//}

//function getAllTags(tags) {
//    const allTags = new Map();

//    tags.forEach(x);

//    function x(tag) {
//        tag.forEach(function (value, key) {
//            allTags.set(key, value);
//        });
//    }
//    return allTags;
//}

//function zoom(chartIds, factor) {
//    chartIds.forEach(function (value) {
//        if (myCharts.has(value)) {
//            myCharts.get(value).zoom(factor);
//        }
//    });
//}

//function resetZoom(chartIds) {
//    chartIds.forEach(function (value) {
//        if (myCharts.has(value)) {
//            myCharts.get(value).resetZoom();
//        }
//    });
//}

//function panX(chartIds, pixel) {
//    chartIds.forEach(function (value) {
//        if (myCharts.has(value)) {
//            myCharts.get(value).pan({ x: pixel }, undefined, 'default')
//        }
//    });
//}

