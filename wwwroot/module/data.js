
const TICKEDBOX = '☒';
const UNTICKEDBOX = '☐';
   
let lastValOnFocus = null;

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
                //alert.test(lastValOnFocus);
            };
            inputs[i].onchange = function () { updInputEvent(this); };
        }

    }

    //console.log(`${tagNames.length} Tags angefragt.`)
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
        alertMessage(result.Type, result.Text);
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


export { JsonTag, drawTags, initTags, initUnits, initWebsocket, tagNameToObject };
                    