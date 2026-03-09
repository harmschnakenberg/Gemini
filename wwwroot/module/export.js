
import fetchSecure from '../module/fetch.js';
import { JsonTag } from '../module/data.js';

function TagCollection(id, name, author, start, end, interval, tags) {
    //   public record TagCollection(int Id, string Name, string Author, DateTime Start, DateTime End, int Interval, Tag[] Tags);
    this.Id = id;
    this.Name = name;
    this.Author = author;
    this.Start = start;
    this.End = end;
    this.Interval = interval;
    this.Tags = tags;
}


const allTagComments = new Map();

async function loadOptions(selectId, link) {

    document.body.style.opacity = "0.5";

    //const ws = await import('../module/fetch.js');
    const response = await fetchSecure(link, {
        method: 'POST'
    });

    if (!response.ok) {
        throw new Error(`Antwort status: ${response.status}`);
        return;
    }

    const json = await response.json();

    json.forEach((t) => {
        allTagComments.set(t.V?.length > 3 ? t.V : t.N, t.N);
        const para = document.createElement("OPTION");
        //console.info(`# ${t.V}=${t.N}`);       
        para.setAttribute("value", t.V?.length > 3 ? t.V : t.N);
        document.getElementById(selectId).appendChild(para);
    });
    document.body.style.opacity = "1";
    // document.body.style.cursor = "auto";
}

function setDatesToStartOfMonth(startId, endId) {
    var now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    document.getElementById(endId).value = now.toISOString().slice(0, 16);

    var begin = new Date();
    begin.setUTCMonth(begin.getUTCMonth() - 1, 1);
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

    //const ws = await import('../module/fetch.js');
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

    document.body.style.cursor = "wait";
    document.body.style.opacity = "0.5";

    const inputs = document.querySelectorAll('.sortable-item > input');
    const interval = document.getElementById('interval').value;
    const tags = new Map();

    for (let i = 0; i < inputs.length; i++) {
        const comment = inputs[i].value;
        const tagName = allTagComments.get(comment);
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

    document.body.style.opacity = "1";
    document.body.style.cursor = "auto";
}

async function excelExport(startId, endId, ival, tags) {

    const s = new Date(document.getElementById(startId).value);
    const e = new Date(document.getElementById(endId).value);
    const arr = [];
    let filename = 'excel.xlsx';

    //console.info(`Übergebene Tags: ${tags.size}`);
    //const ws = await import('../module/fetch.js');

    tags.forEach(function (value, key) {
        //console.info('Exportvorbereitung: ' + key + ' = ' + value);
        const j = new JsonTag(key, value, new Date())
        arr.push(j);
    });

    if (arr.length > 0)
        await fetchSecure('/excel', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                Accept: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
            },
            body: new URLSearchParams({ start: s.toISOString(), end: e.toISOString(), interval: ival, tags: JSON.stringify(arr) })
        }) //https://stackoverflow.com/questions/44168090/fetch-api-to-force-download-file
            .then((res) => {
                try {
                    filename = res.headers.get('Content-Disposition').split("filename=")[1];
                    //filename = res.headers.get('Content-Disposition').split("filename*=UTF-8''")[1]; //UTF8-Filename
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
    const url = '/excel/config/all';

    //const alert = import('../module/alert.js');
    //const ws = await import('../js/websocket.js');
    const res = await fetchSecure(url, {
        method: 'GET'
    });

    if (res.ok) {

        console.info(res);

        const tc = res.json;
        alertSuccess(tc.value);
        /*const tc = JSON.parse(txt);*/

        console.info(`ID:${tc.value}, Name:${tc.name}`);
        //TagCollection
        // public record TagCollection(int Id, string Name, string Author, DateTime Start, DateTime End, int Interval, Tag[] Tags);


    } else {
        alertError('Konfiguration - Nicht erlaubte Operation - Status ' + res.status);
    }

}

async function confExport() {
    const url = '/excel/config/create';
    const chartName = document.getElementById('configName').value;
    const start = new Date(document.getElementById('start').value);
    const end = new Date(document.getElementById('end').value);
    const interval = document.getElementById('interval').value;
    const inputs = document.querySelectorAll('.sortable-item > input');
    const tagNames = [];

    for (let i = 0; i < inputs.length; i++) {
        const comment = inputs[i].value;
        const tagName = allTagComments.get(comment);
        tagNames.push({ tagName: tagName, tagComment: comment, tagValue: null, chartFlag: false });
        //Tag(string tagName, string tagComment, object? tagValue, bool chartFlag)
    }

    //(string Name, string Author, DateTime Start, DateTime End, MiniExcel.Interval Interval, Tag[] Tags)
    const str = JSON.stringify({ id: 0, name: chartName, author: '', start: start.toISOString(), end: end.toISOString(), interval: parseInt(interval), tags: tagNames });
    console.log(str);

    //const ws = await import('../module/fetch.js');
    const res = await fetchSecure(url, {
        method: 'POST',
        headers: { "Content-Type": "application/json" },
        body: str
    });

    if (res.ok) {
        alertSuccess('Konfiguration gespeichert');
    } else {
        alertError('Konfiguration - Nicht erlaubte Operation - Status ' + res.status);
    }

}

export { loadOptions, setDatesToStartOfMonth, excelExport, getExcelFromForm, getDbFromForm, TagCollection }