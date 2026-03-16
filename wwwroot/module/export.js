
import fetchSecure from '../module/fetch.js';
import { JsonTag } from '../module/data.js';
import * as alert from '../module/alert.js';

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

function ChartConfig(id, caption, subCaption, chart1Tags, chart2Tags) {
        this.Id = id;
        this.Caption = caption;
        this.SubCaption = subCaption;
        this.Chart1Tags = chart1Tags;
        this.Chart2Tags = chart2Tags;   
}

const locLists = new Map(); //Map of Maps

async function loadOptions(selectId, locListName, link) {  
    document.body.classList.add('is-loading');
    const locList = new Map();
    const response = await fetchSecure(link, {
        method: 'POST'
    });

    if (!response.ok) {
        throw new Error(`Antwort status: ${response.status}`);
        return;
    }

    const json = await response.json();

    json.forEach((t) => {
        if (t.N?.length > 0) {
            //console.log(`${t.N}=${t.V}`)
            //console.log(`${(t.V.length > 0 || Number.isInteger(t.V) ?  t.V : t.N)} = ${t.N}`)
            locList.set((t.V.length > 0 || Number.isInteger(t.V)) ? t.V : t.N, t.N);

            const para = document.createElement("OPTION");
            para.setAttribute("value", t.V?.length > 0 ? t.V : t.N);
            document.getElementById(selectId).appendChild(para);
        }
    });

    locLists.set(locListName, locList);
    document.body.classList.remove('is-loading');
}

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

/* Download DB-Dateien */
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

function getExcelFromForm() {

    document.body.classList.add('is-loading');

    const inputs = document.querySelectorAll('.sortable-item > input');
    const interval = document.getElementById('interval').value;
    const tags = new Map();

    for (let i = 0; i < inputs.length; i++) {
        const comment = inputs[i].value;
        const tagName = locLists.get('allTagComments').get(comment);
        //console.info(`${allTagComments.size} hinzufügen: ${tagName}=${comment}`)
        tags.set(tagName, comment);
    }

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
        await fetchSecure('/export', {
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


async function confImport() {
    const configName = document.getElementById('configlist').value;
    const configList = locLists.get('allConfigs');
    let configId;

    locLists.get('allConfigs').forEach(function (value, key) {
        if (value == configName) 
            configId = key;       
    });

    if (configId == 'undefined') { 
        console.error(`Konfiguration mit dem Namen ${configName} nicht bekannt.`);
        return;
    }

    const url = `/chart/config/${configId}`;
    console.info(`Lade Konfiguration ${configName} mit ID ${configId} von ${url}`)
    const res = await fetchSecure(url, {
        method: 'POST',
        headers: { "Content-Type": "application/json" }
    });

    if (res.ok) {
        const config = await res.json();
        console.info(config);
        alert.success(`Konfiguration [${config.Id}] '${config.Caption}' geladen.`);

        
        

        console.info(`TEST:${config.Chart1Tags.keys}`);
        //TagCollection
        // public record TagCollection(int Id, string Name, string Author, DateTime Start, DateTime End, int Interval, Tag[] Tags);


    } else {
        alert.error('Konfiguration laden - Nicht erlaubte Operation - Status ' + res.status);
    }

}


async function confExport() {
    const url = '/chart/config/create';
    const caption = document.getElementById('configName').value;
    const subCaption = 'Excel-Export Konfiguration';    
    const inputs = document.querySelectorAll('.sortable-item > input');   
    const tags1 = new Map();

    console.info(`Exportiere Konfiguration: ${caption} mit ${inputs.length} Tags`);

    for (let i = 0; i < inputs.length; i++) {
        const comment = inputs[i].value;
        const tagName = locList.get('allTagComments').get(comment);
        tags1.set(tagName, comment);       
    }
                   
    const chartConfig = new ChartConfig(0, caption, subCaption, Object.fromEntries(tags1), Object.fromEntries(new Map()) );
    const json = JSON.stringify(chartConfig); 
    //console.warn(json);

    const res = await fetchSecure(url, {
        method: 'POST',
        headers: { "Content-Type": "application/json" },
        body: json
    });

    if (res.ok) {
        alert.success(`Konfiguration ${caption} gespeichert`);
    } else {
        alert.error('Konfiguration speichern - Nicht erlaubte Operation - Status ' + res.status);
    }

}


export { loadOptions, setDatesToStartOfMonth, excelExport as export, getExcelFromForm, getDbFromForm, TagCollection, confExport, confImport }