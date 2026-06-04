import fetchSecure from '../module/fetch.js';
import { JsonTag } from '../module/data.js';
import * as alert from '../module/alert.js';
import { addItem, deleteAllItems } from '../module/dragdrop.js';


/**
 *  Zusammenstellung von Tags mit Metainformationen für den Export.
 * @param {number} id
 * @param {string} name
 * @param {string} author
 * @param {Date} start
 * @param {Date} end
 * @param {number} interval
 * @param {Tag[]} tags
 * 
 */
class TagCollection {
    constructor(id, name, author, start, end, interval, tags) {
        //   public record TagCollection(int Id, string Name, string Author, DateTime Start, DateTime End, int Interval, Tag[] Tags);
        this.Id = id;
        this.Name = name;
        this.Author = author;
        this.Start = start;
        this.End = end;
        this.Interval = interval;
        this.Tags = tags;
    }
}

/**
 * Zusammenstellung von Tags mit Metainformationen für Kurvendarstellung und Export.
 * @param {number|string} id - ID kann numerisch oder als String übergeben werden (z.B. aus DOM-Werten)
 * @param {string} author
 * @param {string} caption
 * @param {string} subCaption
 * @param {Object.<string,string>} chart1Tags - Objekt mit TagName->Caption Paaren
 * @param {Object.<string,string>} chart2Tags - Objekt mit TagName->Caption Paaren
 * @constructor
 */
function ChartConfig(id, author, caption, subCaption, chart1Tags, chart2Tags) {
    this.Id = id;
    this.Author = author;
    this.Caption = caption;
    this.SubCaption = subCaption;
    this.Chart1Tags = chart1Tags;
    this.Chart2Tags = chart2Tags;
}

/**
 * Map-Container für lokal geladene Listen.
 * Map von Listennamen auf eine Map von Schlüssel->Wert.
 */
/* Reasoning: locLists wird als Map verwendet, die für jeden Listennamen eine Map<string,string> enthält. */
 /** @type {Map<string, Map<string, string>>} */
const locLists = new Map(); //Map of Maps

/**
 * Konvertiert ein Map-Objekt in ein Plain Object, damit es als JSON-String serialisiert werden kann.
 * @param {Map<any, any>} map - Map mit beliebigen Schlüssel/Wert-Paaren
 * @returns {Object.<string, any>} Plain-Object mit string keys
 */
/* Reasoning: Map wird als Iterable von [key,value] genutzt und in ein Objekt mit string keys konvertiert. */
const autoConvertMapToObject = (map) => {
    const obj = {};
    [...map].forEach(([key, value]) => (obj[key] = value));
    return obj;
}

/**
 * lädt Optionen in ein datalist oder select Element
 *
 * @param {string} dataListId - Id des Ziel-Elements im DOM datalist oder select
 * @param {string} locListName - Name unter dem die geladene Liste in `locLists` abgelegt wird
 * @param {string} link - URL für den Fetch-Request
 * @param {boolean} [setValue=true] - Ob das `value` Attribut der <option>-Elemente gesetzt werden soll
 * @returns {Promise<void>}
 */
/* Reasoning: DOM ids und URL sind Strings; setValue ist boolean; Funktion ist async und gibt nichts zurück. */
async function loadDataListOptions(dataListId, locListName, link, setValue = true) {  
    document.body.classList.add('is-loading');

    //console.log(`Lade Optionen in DOM Id '${dataListId}' aus Liste '${locListName}'`);

    const response = await fetchSecure(link, {
        method: 'POST'
    });

    if (!response.ok) {
        throw new Error(`Antwort status: ${response.status}`);
        return;
    }

    const json = await response.json();

    if (!json) {
        throw new Error(`kein gültiges JSON: ${json}`);
        return;
    }

    // console.log(json);

    /* Vorhandenen Optionen entfernen */
    let selectItem = document.getElementById(dataListId);
    let options = selectItem.getElementsByTagName('option');

    if (options.length > 0)
        for (var i = options.length; i--;) {
            selectItem.removeChild(options[i]);
        }

    /* Optionen anhängen */
    const locList = new Map();
    
    json.forEach((t) => {
        let key = t.N; //Name
        let val = t.V; //Freitext
        if (t.N?.length > 0) {
            //console.info(`ID ${key}: ${val}`)            
            locList.set((val.length > 0 || Number.isInteger(val)) ? val : key, key);
            const para = document.createElement("OPTION");
            if (setValue)
                para.setAttribute("value", key);
           
            para.innerHTML = val.length > 0 ? val : key;
            selectItem.appendChild(para);
        }
    });

    locLists.set(locListName, locList);
    document.body.classList.remove('is-loading');
}

/**
 * Setzt die Werte der übergebenen Input-Elemente auf den Beginn des aktuellen Monats und das aktuelle Datum.
 * @param {string} startId - Id des Start-Datetime-Inputs
 * @param {string} endId - Id des End-Datetime-Inputs
 * @returns {void}
 */
/* Reasoning: Funktion arbeitet mit DOM ids (strings) und ändert Input.value */
function setDatesToStartOfMonth(startId, endId) {
    var now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    document.getElementById(endId).value = now.toISOString().slice(0, 16);

    var begin = new Date();
    const d = begin.getUTCDate(); 
    begin.setUTCMonth(begin.getUTCMonth() - (d < 5 ? 1 : 0), 1);
    begin.setUTCHours(0, 0, 0);
    document.getElementById(startId).value = begin.toISOString().slice(0, 16);
}

/**
 * Lädt die Datenbank als ZIP-Archiv für den angegebenen Zeitraum herunter. Die Start- und Endwerte werden aus den übergebenen Input-Elementen gelesen.
 * @param {string} startId - Id des Start-Datetime-Inputs
 * @param {string} endId - Id des End-Datetime-Inputs
 * @returns {Promise<void>}
 */
/* Reasoning: Async download function, keine Rückgabe erwartet. */
async function getDbFromForm(startId, endId) {

    document.body.style.cursor = "wait";
    document.body.style.opacity = "0.5";
    const s = new Date(document.getElementById(startId).value);
    const e = new Date(document.getElementById(endId).value);
    let filename = 'db.zip';

    await fetchSecure('/db/download', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            Accept: 'application/zip'
        },
        body: new URLSearchParams({ start: s.toISOString(), end: e.toISOString() })
    })
        .then((res) => {
            //console.info(res.headers);
            filename = res.headers.get('Content-Disposition').split('filename=')[1].split(";")[0];
            return res.blob();
        })
        .then(blob => URL.createObjectURL(blob))
        .then(url => {
            var link = document.createElement('a');
            link.download = filename;
            link.href = url;
            link.click();
        });

    document.body.style.opacity = "1";
    document.body.style.cursor = "auto";
}


/**
 *  Lese alle im Export-Formular dynamisch erzeugten Listeneinträge in eine Map(TagName, Comment) 
 *
 * @returns  {Map<string, string>} Map mit TagName als Key und Comment als Value
 */
/* Reasoning: Keys sind TagName (string), Values sind Kommentar (string) */
function getTagMapFromForm() {
    const tags = new Map();
    const inputs = document.querySelectorAll('.sortable-item > input');
    for (let i = 0; i < inputs.length; i++) {
        const comment = inputs[i].value;
        const tagName = locLists.get('allTagComments').get(comment);        
        tags.set(tagName, comment);
    }
    return tags;
}

/**
 * Erzeugt aus den Listeneinträgen wohlgeformtes JSON und schreibt es in das Textarea-Element mit der übergebenen Id. 
 * Die Listeneinträge werden zuvor in eine Map(TagName, Comment) umgewandelt, damit die JSON-Struktur übersichtlich bleibt. 
 * @param {string} textareaId - Id des Textarea-Elements in das JSON geschrieben wird
 * @returns {void}
 */
/* Reasoning: textareaId ist eine DOM id (string) */
function itemSelectionToJson(textareaId) {
    const tagsMap = getTagMapFromForm();     
    //console.warn(tagsMap);
    document.getElementById(textareaId).value = JSON.stringify(autoConvertMapToObject(tagsMap), null, 2);
}

/**
 * Erzeugt Listeneinträge aus JSON von textarea
 * @param {any} textareaId Id der Textarea mit JSON-string
 */
function jsonToItemSelection(textareaId) {
    const txt = document.getElementById(textareaId).value;
    const comments = Object.values(JSON.parse(txt));
 
    deleteAllItems();

    comments.forEach(c => {
        document.getElementById('newItemInput').value = c;
        addItem();
    });

}

// Source - https://stackoverflow.com/a/7220510
// Posted by user123444555621, modified by community. See post 'Timeline' for change history
// Retrieved 2026-06-02, License - CC BY-SA 3.0
// Wandelt Plain JSON in HTML mit CSS-Klassen in Abhängigkeit von JSON-Typ.
/**
 * Hebt JSON für die Anzeige hervor und liefert HTML-String zurück.
 * @param {any} json - JSON-Objekt oder JSON-String
 * @returns {string} HTML-formatierter String mit <span>-Klassen
 */
/* Reasoning: Funktion gibt stets einen string zurück; akzeptiert Objekt oder String */
function syntaxHighlight(json) {
    if (typeof json != 'string') {
        json = JSON.stringify(json, undefined, 2);
    }
    json = json.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    return json.replace(/("(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(true|false|null)\b|-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?)/g, function (match) {
        var cls = 'jsonnumber';
        if (/^"/.test(match)) {
            if (/:$/.test(match)) {
                cls = 'jsonkey';
            } else {
                cls = 'jsonstring';
            }
        } else if (/true|false/.test(match)) {
            cls = 'jsonboolean';
        } else if (/null/.test(match)) {
            cls = 'jsonnull';
        }
        return '<span class="' + cls + '">' + match + '</span>';
    });
}

/**
* Liest die Werte der Input-Elemente für Start, Ende und Intervall aus, sowie die Listeneinträge für die Tags, und ruft die Funktion excelExport mit diesen Werten auf.
* @returns {void}
*/
/* Reasoning: Intervall aus DOM ist String; getTagMapFromForm liefert Map<string,string> */
function getExcelFromForm() {

    document.body.classList.add('is-loading');

    const interval = document.getElementById('interval').value;
    const tags = getTagMapFromForm();

    //for (const x of tags.keys()) {
    //    console.info(`Tag zur Übergabe: ${x} = ${tags.get(x)}`);
    //}

    //console.info(`Übergebene Tags: ${tags.size}`);

    if (tags.size > 0)
        excelExport('start', 'end', interval, tags);
    else
        console.warn("Keine Tags für Excel-Export ausgewählt.");

    document.body.classList.remove('is-loading');
}

/**
 * Ruft den Excel-Export mit den übergebenen Werten auf und löst den Download der generierten Excel-Datei aus.
 * @param {string} startId - Id des Start-Datetime-Inputs
 * @param {string} endId - Id des End-Datetime-Inputs
 * @param {string|number} ival - Intervall als String oder Zahl (wird unverändert weitergereicht)
 * @param {Map<string,string>} tags - Map mit TagName->Comment
 * @returns {Promise<void>}
 */
/* Reasoning: tags ist Map von TagName zu Kommentar, ival kann aus einem Input kommen und ist daher String; akzeptiere auch Zahl. */
async function excelExport(startId, endId, ival, tags) {

    const s = new Date(document.getElementById(startId).value);
    const e = new Date(document.getElementById(endId).value);
    const arr = [];
    let filename = 'kreu.xlsx';

    tags.forEach(function (value, key) {
        //console.info('Exportvorbereitung: ' + key + ' = ' + value);
        const j = new JsonTag(key, value, new Date())
        arr.push(j);
    });

    if (arr.length > 0)
        await fetchSecure('/export/excel', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                Accept: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
            },
            body: new URLSearchParams({ start: s.toISOString(), end: e.toISOString(), interval: ival, tags: JSON.stringify(arr) })
        }) //https://stackoverflow.com/questions/44168090/fetch-api-to-force-download-file
            .then((res) => {
                try {
                    filename = res.headers.get('Content-Disposition').split("filename=")[1].split(';')[0];                   
                } catch (e) {
                    console.warn("Fehler beim Auslesen des Dateinamens aus den Response-Headern: " + e.message);
                }
                return res.blob();
            })
            .then(blob => URL.createObjectURL(blob))
            .then(url => {
                var link = document.createElement('a');
                link.download = filename;
                link.href = url;
                link.click();
            });

}

/**
 * Importiert eine Chart-Konfiguration vom Server und wendet sie im DOM an.
 * @param {number|string|undefined} configId - ID der zu ladenden Konfiguration; kann aus DOM stammen (string) oder Zahl sein
 * @returns {Promise<void>}
 */
/* Reasoning: configId kann undefined sein (checked), ansonsten id kommt vom Server als number oder String. */
async function confImport(configId) {
    //const configName = document.getElementById('configlist').value;    
    //let configId = getKeyFromMap('allConfigs', configName);

    if (configId === undefined) {
        let txt = `Die zu ladende Konfiguration hat keine gültig ID.`; 
        console.error(txt);
        alert.error(txt);
        return;
    }

    const url = `/chart/config/${configId}`;
    //console.info(`Lade Konfiguration ${configName} mit ID ${configId} von ${url}`)
    const res = await fetchSecure(url, {
        method: 'POST',
        headers: { "Content-Type": "application/json" }
    });

    if (res.ok) {
        const config = await res.json();
        //console.info(config);
        alert.success(`Konfiguration [${config.Id}] '${config.Caption}' geladen.`);

        deleteAllItems();
                           
        for (let tagName in config.Chart1Tags) {
            let tagCaption = config.Chart1Tags[tagName];
            // console.info(`${tagName}=${tagCaption}`);

            document.getElementById('newItemInput').value = tagCaption;
            addItem();
        };

        document.getElementById('configId').value = config.Id;
        document.getElementById('caption').value = config.Caption;
        document.getElementById('subcaption').value = config.SubCaption;
        document.getElementById('chart1json').value = JSON.stringify(config.Chart1Tags, null, 2);
        document.getElementById('chart2json').value = JSON.stringify(config.Chart2Tags, null, 2);

    } else {
        alert.error('Konfiguration laden - Nicht erlaubte Operation - Status ' + res.status);
    }

}

/**
 * Exportiert die aktuelle Konfiguration der Charts, indem sie in ein ChartConfig-Objekt gepackt und als JSON-String an den Server gesendet wird.
 * @returns {Promise<void>}
 */
/* Reasoning: Liest Werte aus DOM und sendet JSON; keine Rückgabe. */
async function confUpdate(configId = -1) {
 
    //    const url = `/chart/config/update/${configId}`;
    const url = `/chart/config/update/`;
    const author = document.getElementById('loginMessage').innerHTML;
    const caption = document.getElementById('caption').value;
    if (!caption) return;
    const subCaption = document.getElementById('subcaption').value;  
    const chart1txt = document.getElementById('chart1json').value;
    if (!caption) return;
    const chart1json = JSON.parse(chart1txt);
    const chart2txt = document.getElementById('chart2json').value;
    if (!caption) return;
    const chart2json = JSON.parse(chart2txt);

    console.info(`Exportiere Konfiguration: ${caption} von ${author}`);
                                   
    const chartConfig = new ChartConfig(configId, author, caption, subCaption, chart1json, chart2json);
    const json = JSON.stringify(chartConfig); 
    //console.warn(json);

    const res = await fetchSecure(url, {
        method: 'POST',
        headers: { "Content-Type": "application/json" },
        body: json
    });

    if (res.ok) {
        alert.success(`Konfiguration ${caption} gespeichert`);

        //DropDown aktualisieren
       
        loadDataListOptions('configs', 'allConfigs', `/chart/config/allnames`);
    } else {
        alert.error('Konfiguration speichern - Nicht erlaubte Operation - Status ' + res.status);
    }

}

/**
 * 
 * @param {number|string} configId
 * @returns
 */
async function confDelete(configId) {
    if (configId === undefined) {
        let txt = `Die zu löschende Konfiguration hat keine gültig ID.`;
        console.error(txt);
        alert.error(txt);
        return;
    }

    const url = `/chart/config/delete/${configId}`;
    const res = await fetchSecure(url, {
        method: 'POST',
        headers: { "Content-Type": "application/json" }
    });


    if (res.ok) {
        const config = await res.json();
        alert.error(`Konfiguration [${config.Id}] '${config.Caption}' gelöscht.`);

        deleteAllItems();

        document.getElementById('configId').value = null;
        document.getElementById('caption').value = '';
        document.getElementById('subcaption').value = '';
        document.getElementById('chart1json').value = '';
        document.getElementById('chart2json').value = '';

        loadDataListOptions('configs', 'allConfigs', `/chart/config/allnames`);
    } else {
        alert.error('Konfiguration löschen - Nicht erlaubte Operation - Status ' + res.status);
    }
}


export { loadDataListOptions, itemSelectionToJson, jsonToItemSelection, setDatesToStartOfMonth, excelExport as export, getExcelFromForm, getDbFromForm, TagCollection, confUpdate, confImport, confDelete, getTagMapFromForm, syntaxHighlight }