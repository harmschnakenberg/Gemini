
//let lineChart;
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
    console.log("initCharts()");

    const d = new Date();
    const yyyy = d.getFullYear();
    const MM = d.getMonth();
    const dd = d.getDate();
    const HH = d.getHours();
    const mm = d.getMinutes();
    const ss = d.getSeconds();

    const s = new Date(yyyy, MM, dd, HH - 8, mm, ss);
    const e = new Date(yyyy, MM, dd, HH, mm, ss);
    document.getElementById('start').value = new Date(s).toLocaleString('sv').replace(' ', 'T').slice(0, -3);
    document.getElementById('end').value = e.toLocaleString('sv').replace(' ', 'T').slice(0,-3);

    let c = document.getElementsByClassName("chart");
    console.log(`initCharts() ${c.length} Charts gefunden.`);
    for (let i = 0; i < c.length; i++) {
        if (chartsMap.hasOwnProperty(c[i].id)) 
            continue;

        console.log(`initCharts() Chart ${c[i].id} wirt iniitiert.`);
        chartsMap[c[i].id] = initChart(c[i].id);           
    }
}


function initChart(chartId) {
    console.info('initChart(' + chartId + ')');
    const elm = document.getElementById(chartId);

    if (!elm) {
        console.warn(`Element mit Id ${chartId} existiert nicht.`);
        return;
    }

    const ctx = elm.getContext('2d');

    return new Chart(ctx, {
        type: "line",
        data: {
            labels: [],
            datasets: []
        },
        options: {
            interaction: {
                mode: 'nearest',
                axis: 'x',
                intersect: false
            },
            plugins: {
                legend: { display: true, position: 'bottom' }
            },
            scales: {
                x: {
                    type: 'time',
                    time: {
                        displayFormats: {
                            quarter: 'HH:mm DD.MMM.YYYY'
                        }
                    }
                }
            }
        }
    });
}


async function addChartDataDb(chartId, tagnames, start, end) {

    console.info(`Start ${start}, End ${end}`)
    let s = new Date(start);
    let e = new Date(end);

    if (isNaN(s) || isNaN(e)) {
        console.error(`Chart Zeitbereich ${s} bis ${e} ist ungültig.`);
        return;
    }

    const params = new URLSearchParams();

    params.append("tagnames", tagnames);
    params.append("start", s.toISOString());
    params.append("end", e.toISOString());

    const response = await fetch(`/db?${params}`);
    if (!response.ok) {
        throw new Error(`Response status: ${response.status}`);
    }
    const json = await response.json();

    addChartData(chartId, json);
}



function addChartData(chartId, arr) {

    //if (typeof lineChart === 'undefined') {
    //    console.warn("lineChart ist nicht definiert!");
    //    initCharts();
    //}
    //else
    //    console.info(`lineChart ist vom Typ ${typeof lineChart}`);

   

    if (!chartsMap.hasOwnProperty(chartId))
        chartsMap[chartId] = initChart(chartId);

    let lineChart = chartsMap[chartId];

    arr.forEach((item) => {
        const dsIdx = ensureDataset(lineChart, item.N);
        const ds = lineChart.data.datasets[dsIdx];
        ds.data.push({ x: item.T, y: item.V });
    });

    lineChart.update('none');
}

// neuen Stift erstellen, wenn Dataset nicht existiert 
function ensureDataset(lineChart, name) {
    if (datasetsMap.hasOwnProperty(name)) {
        return datasetsMap[name];
    }

    const color = colors[Object.keys(datasetsMap).length % colors.length];
    const ds = {
        label: name,
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
    return idx;
}

// Hilfsfunktion: Datum aus Zeitstempeln 
function toDate(ts) {
    if (ts instanceof Date)
        return ts;
    const d = typeof ts === 'number' ? new Date(ts) : new Date(ts);

    return isNaN(d.getTime()) ? null : d;
}

//für später: Datensätze entfernen
function removeData(chartId) {
    if (!chartsMap.hasOwnProperty(chartId)) {
        console.warn(`Chart ${chartId} gibt es nicht.`)
        return;
    }

    const chart = chartsMap[chartId];
    chart.data.labels.pop();
    chart.data.datasets.forEach((dataset) => {
        dataset.data.pop();
    });
    //chart.update('none'); //ohne Animation
    chart.update(); 
}