//import { CHART_COLORS } from './chartTheme1.js';

const myCharts = new Map();
const workers = new Map();

// Standardfarben für Datensätze (Chart.js vergibt auch automatisch Farben)
/*const CHART_COLORS = [
    'rgba(75, 192, 192, 1)',
    'rgba(255, 99, 132, 1)',
    'rgba(54, 162, 235, 1)',
    'rgba(255, 206, 86, 1)',
    'rgba(153, 102, 255, 1)',
    'rgba(255, 159, 64, 1)',
    'rgba(75, 99, 235, 1)',
    'rgba(54, 159, 64, 1)'
]; //*/


const CHART_COLORS = [
    'rgba(0, 255, 255, 0.8)',
    'rgba(255, 255, 0, 0.8)',
    'rgba(0, 128, 255, 0.8)',
    'rgba(0, 255, 0, 0.8)',
    'rgba(255, 0, 0, 0.8)',
    'rgba(0, 255, 128, 0.8)',   
    'rgba(255, 128, 0, 0.8',
    'rgba(128, 0, 255, 0.8)',
    'rgba(255, 0, 255, 0.8)'    
];



 //Initialisiert das Chart.js Liniendiagramm.
function initChart(chartId, isStatusChart = false) {
    const elm = document.getElementById(chartId);

    if (!elm) {
        console.warn(`HTML-Element mit Id ${chartId} existiert nicht.`);
        return;
    }

    const zoomOptions = {
        //limits: {
        //    x: { min: 'original', max: 200, minRange: 50 },
        //    y: { min: 'original', max: 200, minRange: 50 }
        //},
        pan: {
            enabled: true,
            mode: 'x', // Enables horizontal panning
            scaleMode: 'x',
        },
        zoom: {
            wheel: {
                enabled: true,
            },
            pinch: {
                enabled: true
            },
            mode: 'xy',
            onZoomComplete({ chart }) {
                chart.update('none');
            },
            scaleMode: 'x'
        }
    };

    let yTicks = {
        color: '#ffffff'
    };

    if (isStatusChart) {
        yTicks = {
            color: '#ffffff',
            stepSize: 1,
            callback: function (value, index, values) {
                if (isStatusChart) {
                    const maxTick = Math.max(...values.map(t => t.value));
                    switch (value) {
                        case 0:                            
                            return 'Aus';
                        case maxTick:
                            return 'Ein';
                        default:
                            return ["Aus", "Ein"];
                    }
                }
                else
                    return value;
            }
        };
    }

    const ctx = elm.getContext('2d');

    let myChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: [],
            datasets: []
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: false,
            spanGaps: true,
            // parsing: false, //Geht nicht?
            scales: {
                x: {
                    type: 'time',
                    //bounds: 'ticks', // Stellt sicher, dass min/max Ticks sichtbar sind
                    time: {
                        unit: 'hour',
                        displayFormats: {
                            day: 'dd.MM.yyyy HH:mm',
                            hour: 'HH:mm',
                            minute: 'HH:mm:ss'
                        },
                        tooltipFormat: 'dd.MM.yyyy HH:mm'
                    },
                    //min: startDate,
                    ticks: {
                        source: 'auto', //'data', //'auto'
                        autoSkip: true,
                        //display: true,
                        //maxTicksLimit: 10, // Zeigt maximal 10 Ticks an
                        //count: 20,
                        //minRotation: 90,   
                        major: { enabled: true },
                        //stepSize: 0.25,
                        color: '#ffffff',
                        z: 1,
                        //beforeBuildTicks: function(ax){
                        //   console.log(ax._unit);
                        //},

                        //callback: function (value, index, ticks) {
                        //    if (index === 0 || index === ticks.length - 1) {
                        //        return this.getLabelForValue(value); 
                        //    }

                        //    return this.getLabelForValue(value);
                        //}

                        //callback: function (val, index, ticks) {
                        //    if (ticks.length < 10)
                        //        return this.getLabelForValue(val);
                        //    else if (ticks.length < 100)
                        //        return index % 2 === 0 ? this.getLabelForValue(val) : '';
                        //    else
                        //        return index % 10 === 0 ? this.getLabelForValue(val) : '';
                        //}

                    },
                    title: {
                        display: true,
                        color: '#ffffff',
                        text: 'Zeit'
                    },
                    grid: {                        
                        display: true,
                        drawTicks: true,
                        color: '#666666'
                    }
                },
                y: {
                    title: {
                        display: true,
                        position: 'bottom',
                        color: '#ffffff',
                        text: 'Wert'
                    },
                    //suggestedMax: function (context) {                        
                    //    //const maxDataSet = context.chart.data.datasets.length;
                    //    const maxVal = Math.max(...values.map(t => t.value));
                    //    return maxVal + 1;
                    //},
                    ticks: yTicks,            
                    grid: {
                        display: true,
                        drawTicks: true,
                        color: '#555555'
                    }
                }
                //plugins: {
                //    filler: { propagate: false } // Verhindert das Durchscheinen nach unten
                //}
            },
            plugins: {
                zoom: zoomOptions,
                legend: {
                    labels: {
                        color: '#ffffff'
                    },
                    position: 'bottom',
                    display: true
                }//,
                //decimation: {
                //    enabled: true
                //}
            }
        }
    });

    myCharts.set(chartId, myChart);

    const worker = new Worker('/js/chartworker.js'); 
    worker.onmessage = workermessage;
    workers.set(chartId, worker);
}

//TODO: Darstellung Status-Chart als Linie oder Balken.
/* function toggleStatusChart(chartId, tagNames) {

    const elm = document.getElementById(chartId);
    if (!elm) {
        console.warn(`HTML-Element mit Id ${chartId} existiert nicht.`);
        return;
    }

    const booleanTags = [];

    tagNames.forEach(function (t) {
        let isBool = tagName.includes('X');       
        booleanTags.push(isBool ? t : false);          
    });

    const chart = myCharts.get(chartId);
    

    chart.data.datasets.forEach((dataset) => {    
        //tagNames[]
        const offset = dataset.fill.target.value;
        //dataset.fill = !dataset.fill;
        if (dataset.fill != false) {
            dataset.fill.target.value += (toggle == 1 ? 1 : -1);
            //fill: isBool ? { target: { value: offset }, above: color } : false, // binär Liniendiagramme: Zwischen 'Aus' und 'Ein' füllen

        }
    });

    chart.update();
}  //*/



/**
 * Sendet die Anfrage an den Web Worker, um Daten zu laden und zu verarbeiten.
 */
function loadChart(chartId, startId, endId, LABEL_ALIASES) { 

    document.body.style.cursor = "wait";
    document.body.style.opacity = "0.5";
    document.getElementById('customspinner').style.visibility = 'visible';

    // Setze die Fortschrittsanzeige zurück und zeige sie an
    document.getElementById('progressBar').value = 0;
    document.getElementById('progressText').textContent = '0%';
    document.getElementById('progressContainer').style.display = 'block';

    const startDate = new Date(document.getElementById(startId).value);
    const endDate = new Date(document.getElementById(endId).value);

    if (!startDate || !endDate) {
        alert(`Bitte Start- und Enddatum für ${chartId} auswählen.`);
        return;
    }

    //console.log(`Lade Daten für Zeitraum ${startDate.toISOString()} bis ${endDate.toISOString() }...`);

    //for (const x of LABEL_ALIASES.entries()) {
    //    console.info('Label ' + x);
    //}

    const params = new URLSearchParams();
    const tagnames = Array.from(LABEL_ALIASES.keys());
    params.append("tagnames", tagnames);
    params.append("start", startDate.toISOString());
    params.append("end", endDate.toISOString());
    const link = `/db?${params}`;

    if (!document.getElementById(chartId + 'link')) {
        const aEl = document.createElement('a');
        aEl.setAttribute("id", chartId + 'link');
        aEl.setAttribute("href", link);
        aEl.style.color = 'white';
        aEl.style.textDecoration = 'none';
        aEl.style.display = 'block';
        aEl.innerHTML = 'Rohdaten';
        //<a id="rawDataLink" style="position:absolute; bottom:0.5rem; left:0.5rem;color:white; text-decoration:none;">Rohdaten</a>
        document.getElementById('rawDataLinks').appendChild(aEl);
    }
    
    // Sende alle notwendigen Daten an den Worker
    workers.get(chartId).postMessage({
        chartId: chartId,
        url: link,
        aliases: LABEL_ALIASES,
        colors: CHART_COLORS
    });
}

/*
  Lade laufend aktuelle Werte nach
*/
//function updateChart(chartId, LABEL_ALIASES) {

//    const params = new URLSearchParams();
//    const tagnames = Array.from(LABEL_ALIASES.keys());
//    params.append("tagnames", tagnames);
//    const link = `/ws?${params}`;


//    // Sende alle notwendigen Daten an den Worker
//    workers.get(chartId).postMessage({
//        chartId: chartId,
//        url: link,
//        aliases: LABEL_ALIASES,
//        colors: CHART_COLORS
//    });
//}

/**
 * Empfängt die verarbeiteten Daten vom Web Worker und aktualisiert das Chart.
 */
    function workermessage(e) {

        const message = e.data;

        if (message.type === 'progress') {
            // Fortschritts-Update verarbeiten
            const percentage = message.percentage;
            //     console.log(percentage);
            progressBar.value = percentage;
            progressText.textContent = `${percentage}%,  ${message.processedCount}/${message.totalCount} Datensätze`;

            if (message.processedCount == message.totalCount)
                document.getElementById(message.chartId + 'link').innerHTML = `Rohdaten [${message.totalCount} Datensätze]`;

        } else if (message.type === 'complete') {
            const chartId = message.chartId;
            const newDatasets = e.data.datasets;
            const myChart = myCharts.get(chartId);
            //console.log(`Daten vom Worker für ${chartId} empfangen. ${newDatasets.length} Datensätze.`);

            // Ersetze die existierenden Datensätze durch die neuen
            myChart.data.datasets = newDatasets;

            // Aktualisiere das Diagramm
            myChart.update('none');
            //console.log('Chart aktualisiert.');
            progressContainer.style.display = 'none';
            document.body.style.opacity = "1";
            document.body.style.cursor = "auto";   
            document.getElementById('customspinner').style.visibility = 'hidden';
        } else if (message.type === 'error') {
            // Fehlerbehandlung
            progressContainer.style.display = 'none';
            alert(`Fehler beim Laden/Verarbeiten der Daten für ${message.chartId}: ${message.error}`);
        }
    }

function setDatesHours(startId, endId, hh) {
    var now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    document.getElementById(endId).value = now.toISOString().slice(0, 16);

    var begin = new Date();
    begin.setUTCHours(begin.getHours() - hh);
    document.getElementById(startId).value = begin.toISOString().slice(0, 16);
}

function getAllTags(tags) {
    const allTags = new Map();

    tags.forEach(x);

    function x(tag) {
        tag.forEach(function (value, key) {
            allTags.set(key, value);
        });
    }
    return allTags;
}

function zoom(chartIds, factor) {
    chartIds.forEach(function (value) {
        if (myCharts.has(value)) {
            myCharts.get(value).zoom(factor);
        }
    });
}

function resetZoom(chartIds) {
    chartIds.forEach(function (value) {
        if (myCharts.has(value)) {
            myCharts.get(value).resetZoom();
        }
    });
}

function panX(chartIds, pixel) {
    chartIds.forEach(function (value) {
        if (myCharts.has(value)) {
            myCharts.get(value).pan({ x: pixel }, undefined, 'default')
        }
    });
}