
import fetchSecure from '../module/fetch.js';
import * as alert from '../module/alert.js';

const TICKEDBOX = '☒';
const UNTICKEDBOX = '☐';   
let lastValOnFocus = null;

function JsonTag(name, value, time) {
    this.N = name;
    this.V = value;
    this.T = time;
}

function initUnits() {  
    //const inputs = document.getElementsByTagName('input');
    const unitElms = document.querySelectorAll('[data-unit]')
    for (let i = 0; i < unitElms.length; i++) {
        if (unitElms[i].tagName == 'INPUT') {
            const tagUnit = unitElms[i].getAttribute('data-unit');
            const para = document.createElement("span");
            const node = document.createTextNode(tagUnit);
            para.appendChild(node);

            unitElms[i].parentNode.insertBefore(para, unitElms[i].nextSibling);
        }
        else if (unitElms[i].classList.contains('wertanzeige')) {
            // SVG Wertanzeige erstellen
            console.info(unitElms[i]);
            const x = unitElms[i].getAttribute('x');;
            const y = unitElms[i].getAttribute('y');;            
            const tagUnit = unitElms[i].getAttribute('data-unit');
            const tagName = unitElms[i].getAttribute('data-name');
            createTextInstance(x, y, tagName, tagUnit);
            unitElms[i].remove();            
        }
        else
            console.warn(`Unbekanntes Element mit data-unit: ${unitElms[i].tagName} ${unitElms[i].classList}`);
    }
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

        if (inputs[i].tagName == 'INPUT' && !inputs[i].disabled) {
            //Script zum Schreiben in die SPS anfügen
            inputs[i].onfocus = function () {
                lastValOnFocus = this.value;
                //alert.test(lastValOnFocus);
            };
            inputs[i].onchange = function () { updInputEvent(this); };
        }

    }

    console.log(`${tagNames.length} Tags angefragt.`)
    return tagNames.map(tagNameToObject);
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

function tagNameToObject(name) {
    return new JsonTag(name, null, new Date());
}

async function updInputEvent(obj) {
    const t = obj.getAttribute('data-name');
    let v = obj.value;

    console.info(`Trigger Änderung ${t} von ${lastValOnFocus} auf ${v}`);
    if (v == TICKEDBOX) v = 1;
    if (v == UNTICKEDBOX) v = 0;
    
    const tag = new JsonTag(t, v, new Date());
    const link = `/tag/write`;
    const res = await fetchSecure(link, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({tagName: t, tagVal: v, oldVal: lastValOnFocus })
    });
    console.info(new URLSearchParams({ tagName: t, tagVal: v, oldVal: lastValOnFocus }));

    if (res.ok) {
        const result = await res.json();
        console.log(result.Text);
        alert.message(result.Type, result.Text);
    } else {
        console.error('Wertänderung - Nicht erlaubte Operation - Status ' + res.status);
    }
}

function drawTags(arr) {
    if (arr.length < 1)
        return;

    const tagElms = document.querySelectorAll('[data-name]')
    for (let i = 0; i < tagElms.length; i++) {
        const tagName = tagElms[i].getAttribute('data-name');
        let obj = arr.find(o => o.N === tagName);
        if (obj) {
            if (tagElms[i].nodeName == 'INPUT') {
                if (tagElms[i].classList.contains("checkbox"))
                    tagElms[i].value = obj.V > 0 ? TICKEDBOX : UNTICKEDBOX;
                else
                    tagElms[i].value = obj.V;
            }
            else if (tagElms[i].nodeName == 'TSPAN') {     
                tagElms[i].querySelector('.iv').textContent = obj.V;
            }
            else {
                console.info(`Nicht erwarteter Werte-Tag ${tagElms[i].tagName}`);             
                tagElms[i].innerHTML = obj.V;
            }
        }
    }
}

function createTextInstance(x, y, name, unit) {
    const svg = document.getElementById('svg-canvas');
    const template = document.getElementById('istwert');

    // 1. Echte Kopie erstellen (deep clone)
    const instance = template.cloneNode(true);
    instance.removeAttribute('id'); // ID entfernen, damit sie nicht mehrfach vorkommt

    // 2. Individuelle Texte setzen
    instance.querySelector('.iv').setAttribute('data-name', name);
    instance.querySelector('.iv').textContent = 0.0;
    instance.querySelector('.iu').textContent = unit;

    // 3. Positionieren (via transform)
    if (x && y) 
    instance.setAttribute('transform', `translate(${x}, ${y})`);

    // 4. In das SVG einfügen
    svg.appendChild(instance);
}

async function updateTag(obj) {
    const tagName = obj.parentNode.parentNode.children[0].children[0].value;
    const tagComm = obj.parentNode.parentNode.children[1].children[0].value;
    const tagChck = obj.parentNode.parentNode.children[3].children[0].checked;

    fetchSecure('/tag/update', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ tagName: tagName, tagComm: tagComm, tagChck: tagChck })
    });
}


async function getAlteredTags() {
    const start = document.getElementById('start').value;
    const end = document.getElementById('end').value;
    const filter = document.getElementById('filter').value;

    if ('URLSearchParams' in window) {
        var searchParams = new URLSearchParams(window.location.search);
        searchParams.set('start', start);
        searchParams.set('end', end);
        searchParams.set('filter', filter);
        window.location.search = searchParams.toString();
    }

    const link = `/soll/history?start=${encodeURIComponent(start)}&end=${encodeURIComponent(end)}&filter=${encodeURIComponent(filter)}`;

    try {
        //const ws = await import('../module/fetch.js');
        const response = await fetchSecure(link);

        if (!response.ok)
            throw new Error(`Response status: ${response.status}`);

        const html = await response.text();
        document.body.innerHTML = html;
    } catch (error) {
        console.error('Fehler beim Laden:', error);
    }

}

export { JsonTag, drawTags, initTags, initUnits, initWebsocket, tagNameToObject, updateTag, getAlteredTags };
                    