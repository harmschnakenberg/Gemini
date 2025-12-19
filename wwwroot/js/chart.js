//let myChart;
//const worker = new Worker('/js/chartworker.js'); // Initialisiert den Web Worker

const myCharts = new Map();
const workers = new Map();

// Standardfarben für Datensätze (Chart.js vergibt auch automatisch Farben)
const CHART_COLORS = [
    'rgba(75, 192, 192, 1)',
    'rgba(255, 99, 132, 1)',
    'rgba(54, 162, 235, 1)',
    'rgba(255, 206, 86, 1)',
    'rgba(153, 102, 255, 1)',
    'rgba(255, 159, 64, 1)'
];


 //Initialisiert das Chart.js Liniendiagramm.
function initChart(chartId) {
    const elm = document.getElementById(chartId);

    if (!elm) {
        console.warn(`HTML-Element mit Id ${chartId} existiert nicht.`);
        return;
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
            scales: {
                x: {
                    type: 'time',
                    time: {
                        unit: 'minute',
                        displayFormats: {
                            minute: 'HH:mm '
                        },
                        tooltipFormat: 'dd.MM.yyyy HH:mm'   
                    },
                    title: {
                        display: true,
                        color: '#ffffff',
                        text: 'Zeit'
                    },
                    //min: startDate,
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
                    title: {
                        display: true,
                        position: 'bottom',
                        color: '#ffffff',
                        text: 'Wert'
                    }
                }
            },
            plugins: {
                legend: {
                    labels: {
                        color: '#ffffff'
                    },
                    position: 'bottom',
                    display: true
                }
            }
        }
    });

    myCharts.set(chartId, myChart);

    const worker = new Worker('/js/chartworker.js'); 
    worker.onmessage = workermessage;
    workers.set(chartId, worker);
}


/**
 * Sendet die Anfrage an den Web Worker, um Daten zu laden und zu verarbeiten.
 */
function loadChart(chartId, startId, endId, LABEL_ALIASES) { 

    document.body.style.cursor = "wait";
    document.body.style.opacity = "0.5";

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
    document.getElementById('rawDataLink').setAttribute("href", link);

    
    // Sende alle notwendigen Daten an den Worker
    workers.get(chartId).postMessage({
        chartId: chartId,
        url: link,
        aliases: LABEL_ALIASES,
        colors: CHART_COLORS
    });
}

/**
 * Empfängt die verarbeiteten Daten vom Web Worker und aktualisiert das Chart.
 */

//worker.onmessage =

    function workermessage(e) {

        const message = e.data;

        if (message.type === 'progress') {
            // Fortschritts-Update verarbeiten
            const percentage = message.percentage;
            //     console.log(percentage);
            progressBar.value = percentage;
            progressText.textContent = `${percentage}%,  ${message.processedCount}/${message.totalCount} Datensätze`;

        } else if (message.type === 'complete') {
            const chartId = message.chartId;
            const newDatasets = e.data.datasets;
            const myChart = myCharts.get(chartId);
            console.log(`Daten vom Worker für ${chartId} empfangen. ${newDatasets.length} Datensätze.`);

            // Ersetze die existierenden Datensätze durch die neuen
            myChart.data.datasets = newDatasets;

            // Aktualisiere das Diagramm
            myChart.update();
            //console.log('Chart aktualisiert.');
            progressContainer.style.display = 'none';
            document.body.style.opacity = "1";
            document.body.style.cursor = "auto";
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