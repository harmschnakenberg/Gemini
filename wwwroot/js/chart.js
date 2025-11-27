
//let lineChart;
let startDate = new Date();
//startDate.setHours(0);

const chartsMap = {}; // chartId -> Chart
const datasetsMap = {}; // Name -> datasetIndex
const colors = [
    'rgba(75, 192, 192, 1)',
    'rgba(255, 99, 132, 1)',
    'rgba(54, 162, 235, 1)',
    'rgba(255, 206, 86, 1)',
    'rgba(153, 102, 255, 1)',
    'rgba(255, 159, 64, 1)'
];

window.onload = () => {
    initCharts();
}

function initCharts() {
    //console.log("initCharts()");

    //const d = new Date();
    //const yyyy = d.getFullYear();
    //const MM = d.getMonth();
    //const dd = d.getDate();
    //const HH = d.getHours();
    //const mm = d.getMinutes();
    //const ss = d.getSeconds();

    let c = document.getElementsByClassName("chart");
    console.log(`initCharts() ${c.length} Charts gefunden.`);
    for (let i = 0; i < c.length; i++) {
        const chartId = c[i].id;

        if (chartsMap.hasOwnProperty(chartId)) {
            console.warn(`Chart ${chartId} gibt es schon.`)
            continue;
        }
            
        chartsMap[chartId] = initChart(chartId);   
        console.log(`initCharts() Chart ${chartId} wirt iniitiert. ${chartMap.size}`);
    }
}


function initChart(chartId) {
    //console.info('initChart(' + chartId + ')');
    const elm = document.getElementById(chartId);

    if (!elm) {
        console.warn(`HTML-Element mit Id ${chartId} existiert nicht.`);
        return;
    }

    const ctx = elm.getContext('2d');
       
    return new Chart(ctx, {
        type: "line",
        //defaults: {
        //    color: '#ffffff',
        //    borderColor: '#ffffff'
        //},      
        data: {
            labels: [],
            datasets: []
        },
        options: {
            //responsive: true,
            interaction: {
                mode: 'nearest',
                axis: 'x',
                intersect: false
            },
            animation: false,
            spanGaps: true,
            datasets: {
                line: {
                    pointRadius: 0
                }
            },
            plugins: {
                legend: {
                    labels: {
                        color: '#ffffff'
                    },
                    display: true,
                    position: 'bottom'
                }
                //,tooltip: {
                //    titleColor: '#00ff00', // Tooltip-Titel grün
                //    bodyColor: '#0000ff'   // Tooltip-Text blau
                //}
                //, colors: {
                //    forceOverride: true
                //}
            },
            scales: {
                x: {
                    title: {
                        color: '#ffffff',
                        display: true,
                        text: 'Zeit'
                    },
                    type: 'time',
                    time: {
                        unit: 'minute',
                        displayFormats: {
                            minute: 'HH:mm '
                        },
                        tooltipFormat: 'dd.MM.yyyy HH:mm'                        
                    },
                    min: startDate,
                    ticks: {
                        source: 'data',                        
                        //minRotation: 90,   
                        major: { enabled: true },
                        stepSize: 15,
                        color: '#ffffff'
                    },
                    grid: {
                        display: true,
                        drawTicks: true,
                        color: '#666666'
                    }
                    
                },
                y: {
                    ticks: {
                        color: '#ffffff'
                    },
                    grid: {
                        color: '#888888'
                    }
                }
            }
        }        
    });
}


async function addChartDataDb(chartId, tags, start, end) {

    console.info(`Chart ${chartId}; Start ${start}, End ${end}`)
    let s = new Date(start);
    let e = new Date(end);

    if (isNaN(s) || isNaN(e)) {
        console.error(`Chart Zeitbereich ${s} bis ${e} ist ungültig.`);
        return;
    }

    const params = new URLSearchParams();
    const tagnames = Array.from(tags.keys());
    params.append("tagnames", tagnames);
    params.append("start", s.toISOString());
    params.append("end", e.toISOString());

    const link = `/db?${params}`;
    const response = await fetch(link);
    //console.log(`/db?${params}`);

    if (!response.ok) {
        throw new Error(`Response status: ${response.status}`);
        return;
    }

    startDate = rundeZeitAufViertelstunde(s);

    var x = document.getElementById("rawDataLink");
    x.setAttribute('href', link);
    x.innerHTML = `Rohdaten ${s.toLocaleString()} bis ${e.toLocaleString()}`;
 
    const json = await response.json();
    addChartData(chartId, json, tags);
    
}


function addChartData(chartId, arr, tags) {
    if (!chartsMap.hasOwnProperty(chartId)) {
        console.info(`addChartData(${chartId}, arr) initiiert Chart.`);
        chartsMap[chartId] = initChart(chartId);
    }

    arr.forEach((item) => {
        const dsIdx = ensureDataset(chartId, item.N, tags);
        const ds = chartsMap[chartId].data.datasets[dsIdx];
        ds.data.push({ x: item.T, y: item.V });
    });

    chartsMap[chartId].update('none');
}

function changeLabels(chartId, map) {
    console.info("Label" + chartsMap[chartId].config.data.labels);
    console.info("Keys " + Array.from(map.values()).toString());
    //chartsMap[chartId].data.labels.forEach((label) => {
    //    console.info("Label" + chartsMap[chartId].data.labels);
    //        label = map.get(label);
    //});
}


// neuen Stift erstellen, wenn Dataset nicht existiert 
function ensureDataset(chartId, name, tags) {
    if (datasetsMap.hasOwnProperty(name)) {
        return datasetsMap[name];
    }

    if (!chartsMap.hasOwnProperty(chartId)) {
        console.info(`ensureDataset(${chartId}, name) initiiert Chart.`);
        chartsMap[chartId] = initChart(chartId);
    }

    const lineChart = chartsMap[chartId];
    const color = colors[Object.keys(datasetsMap).length % colors.length];
    const ds = {
        label: tags.get(name),
        data: [], // {x: Date, y: Number} 
        borderColor: color,
        backgroundColor: color,
        pointRadius: 1,
        fill: false,
        tension: 0.0
    };

    lineChart.data.datasets.push(ds);

    const idx = lineChart.data.datasets.length - 1;
    datasetsMap[name] = idx;
    //console.log(`Chart ${chartId}; datasetsMap[${name}] = ${idx};`)
    return idx;
}

// Hilfsfunktion: Datum aus Zeitstempeln
//function toDate(ts) {
//    if (ts instanceof Date)
//        return ts;
//    const d = typeof ts === 'number' ? new Date(ts) : new Date(ts);

//    return isNaN(d.getTime()) ? null : d;
//}

function rundeZeitAufViertelstunde(date) {
    // Zeit in Minuten umwandeln
    let minuten = date.getMinutes();
    let stunden = date.getHours();

    // Auf die nächste Viertelstunde abrunden
    let gerundeteMinuten = Math.floor(minuten / 15) * 15;

    // Stunden anpassen, falls Aufrundung über 60 Minuten führt
    let gerundeteStunden = stunden + Math.floor(gerundeteMinuten / 60);
    gerundeteMinuten = gerundeteMinuten % 60;

    // Neue Date-Objekt mit gerundeter Zeit erstellen
    let neueDatum = new Date(date.getFullYear(), date.getMonth(), date.getDate(), gerundeteStunden, gerundeteMinuten);

    console.info("gerundete Zeit ist " + neueDatum);
    return neueDatum;
}


function removeData(chartId) {
    if (!chartsMap.hasOwnProperty(chartId)) {
        return;
    }
    //console.info("Lösche Daten aus " + chartId);

    chartsMap[chartId].data.datasets.forEach((ds) => {
        ds.data = [];
    });

    chartsMap[chartId].update();
}


function loadChart(chartId, startId, endId, tags) {
    //ToDo: in Backgroundworker auslagern
    const start = new Date(document.getElementById(startId).value);
    const end = new Date(document.getElementById(endId).value);

    if (isNaN(start) || isNaN(end)) {
        console.error(`Zeitbereich ${start} bis ${end} ist ungültig.`);
        return;
    }

    const id = document.getElementById(chartId).id;
    removeData(id);
    addChartDataDb(id, tags, start, end);
}

function setDates(startId, endId, std) {
    var now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    document.getElementById(endId).value = now.toISOString().slice(0, 16);

    var begin = new Date();
    begin.setUTCHours(begin.getHours() - std);
    document.getElementById(startId).value = begin.toISOString().slice(0, 16);
}

function post(path, params, method = 'post') {

    // The rest of this code assumes you are not using a library.
    // It can be made less verbose if you use one.
    const form = document.createElement('form');
    form.method = method;
    form.action = path;

    for (const key in params) {
        if (params.hasOwnProperty(key)) {
            const hiddenField = document.createElement('input');
            hiddenField.type = 'hidden';
            hiddenField.name = key;
            hiddenField.value = params[key];

            form.appendChild(hiddenField);
        }
    }

    document.body.appendChild(form);
    form.submit();
}