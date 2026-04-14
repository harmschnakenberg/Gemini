
import fetchSecure from '../module/fetch.js';
import * as alert from '../module/alert.js';
import createSvgInstance from '../module/svg.js';

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
    }
}

function initSvg() {
    const objs = document.querySelectorAll('[data-svg]')

    for (let i = 0; i < objs.length; i++) {        
        createSvgInstance(objs[i])
    }

}

function initTags() {
    
    const tagNames = [];
    const objs = document.querySelectorAll('[data-name]')
    for (let i = 0; i < objs.length; i++) {
        const tagName = objs[i].getAttribute('data-name');
        if (!tagNames.includes(tagName)) {
            tagNames.push(tagName);
        }

        if (objs[i].tagName == 'INPUT') {
            if (objs[i].classList.contains("checkbox")) {
                objs[i].setAttribute("readonly", "true");
                objs[i].addEventListener("click", function () {
                    if (this.value == TICKEDBOX)
                        this.value = UNTICKEDBOX;
                    else
                        this.value = TICKEDBOX;
                });
                objs[i].addEventListener("blur", function () {
                    //Script zum Schreiben in die SPS anfügen
                    updInputEvent(this, lastValOnFocus);
                });
            }

            if (!objs[i].disabled) {
                //Script zum Schreiben in die SPS anfügen
                objs[i].onfocus = function () {
                    lastValOnFocus = this.value;
                    //alert.test(lastValOnFocus);
                };
                objs[i].onchange = function () { updInputEvent(this); };
            }
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
            //else if (tagElms[i].classList.contains("valuedisplay")) {     
            //    // tagElms[i].querySelector('.iv').textContent = obj.V;
            //    tagElms[i].textContent = obj.V;
            //}
            //else if (tagElms[i].classList.contains("pumpe")) {
            //    tagElms[i].style.fill = 'red';
            //}
            else {
                tagElms[i].setAttribute('data-value', obj.V);

                //console.info(`Nicht erwarteter Werte-Tag ${tagElms[i].tagName}`);             
                //tagElms[i].innerHTML = obj.V;
            }
        }
    }
}


// Schreibe einen Wert in die SPS
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

export { JsonTag, drawTags, initSvg, initTags, initUnits, initWebsocket, tagNameToObject, updateTag, getAlteredTags };
                    