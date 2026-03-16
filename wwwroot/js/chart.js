const myCharts = new Map();
const workers = new Map();

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
                            return ['Aus', value];
                        case maxTick:
                            return ['Ein', value];
                        default:
                            return ['Aus', value, 'Ein'];
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
            //animation: {
            //    onComplete: function (animation) {
            //        if (animation.initial) { //nur einmal ausführen
            //            console.log(`Diagramm  ${chartId} ist vollständig gerendert.`);
            //            // Hier Code ausführen, der nach dem Rendern erfolgen soll
            //            // z.B. Chart als Bild exportieren
            //            elm.classList.remove('is-loading');
            //        }
            //    }
            //},
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

    const worker = new Worker('/module/chartworker.js', { type: "module" });     
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

/* Initialisiert die Fortschrittsanzeige für das Laden und Verarbeiten der Daten. Erstellt die notwendigen HTML-Elemente, wenn sie noch nicht existieren, und setzt die Anzeige zurück. */
function setupProgressbar(chartId) {

    let allProgressContainer = document.getElementById('progressContainer'); 
    let myProgressContainer = document.getElementById('progressContainer' + chartId);
    if (!myProgressContainer) {
        myProgressContainer = document.createElement('div');
        myProgressContainer.setAttribute("id", 'progressContainer' + chartId);           
        allProgressContainer.appendChild(myProgressContainer);

        const label = document.createElement('label');
        label.setAttribute("for", 'progressBar' + chartId);
        label.textContent = 'Verarbeitung: ';
        myProgressContainer.appendChild(label);

        const progressBar = document.createElement('progress');
        progressBar.setAttribute("id", 'progressBar' + chartId);
        progressBar.setAttribute("value", "0");
        progressBar.setAttribute("max", "100");
        progressBar.style.width = '300px';
        myProgressContainer.appendChild(progressBar);

        const progressText = document.createElement('span');
        progressText.setAttribute("id", 'progressText' + chartId);
        progressText.textContent = '0%';
        myProgressContainer.appendChild(progressText);
    }

    // Setze die Fortschrittsanzeige zurück und zeige sie an
    document.getElementById('progressBar' + chartId).value = 0;
    document.getElementById('progressText' + chartId).textContent = '0%';
    myProgressContainer.style.display = 'block';
}

/* Sendet die Anfrage an den Web Worker, um Daten zu laden und zu verarbeiten. */
function loadChart(chartId, startId, endId, intervalId, LABEL_ALIASES) { 

    document.getElementById(chartId).classList.add('is-loading');    
    document.getElementById('waitforserver').showModal();
    setupProgressbar(chartId);

    const startDate = new Date(document.getElementById(startId).value);
    const endDate = new Date(document.getElementById(endId).value);
    const interval = parseInt(document.getElementById(intervalId).value);

    const params = new URLSearchParams();

    if (!startDate || !endDate || isNaN(startDate) || isNaN(endDate) ) {
        alert(`Bitte Start- und Enddatum für ${chartId} auswählen.`);
        params.delete('start');
        params.delete('end');
        return;
    }
  
    const tagnames = Array.from(LABEL_ALIASES.keys());
    params.set("tagnames", tagnames);

    if (!params.has('start'))
        params.append("start", startDate.toISOString());
    if (!params.has('end'))
        params.append("end", endDate.toISOString());

    //if (TimeSpanInDays(startDate, endDate) > 7)
    //    if (confirm(`Der ausgewählte Zeitraum von ${TimeSpanInDays(startDate, endDate)} Tagen kann zu einer großen Datenmenge führen.\r\nDer Browser könnte längere Zeit zum Berechnen der Darstellung benötigen. Die Daten können auf dem Server komprimiert werden, um die Anzahl der Datenpunkte zu verringern. Daten auf dem Server komprimieren? Abbrechen lädt alle Datenpunkte unkomprimiert.`))
    if(!isNaN(interval))
        params.append('interval', interval);

    const link = `/db?${params}`;

    if (!document.getElementById(chartId + 'link')) {
        const aEl = document.createElement('a');
        aEl.setAttribute("id", chartId + 'link');
        aEl.setAttribute("href", link);
        aEl.style.color = 'white';
        aEl.style.textDecoration = 'none';
        aEl.style.display = 'block';
        aEl.innerHTML = 'Rohdaten';        
        document.getElementById('rawDataLinks').appendChild(aEl);
    }

    
   
    //let colors = theme.CHART_COLORS;
    
    // Sende alle notwendigen Daten an den Worker
    workers.get(chartId).postMessage({
        chartId: chartId,
        url: link,
        aliases: LABEL_ALIASES
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

/* Empfängt die verarbeiteten Daten vom Web Worker und aktualisiert das Chart. */
function workermessage(e) {

        const message = e.data;
        const chartId = message.chartId;

        if (message.type === 'progress') {
            // Fortschritts-Update verarbeiten
            const percentage = message.percentage;       
            document.getElementById('progressBar' + chartId).value = percentage;
            document.getElementById('progressText' + chartId).textContent = `${percentage}%,  ${message.processedCount}/${message.totalCount} Datensätze`;

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
             
            setTimeout(() => {
                document.getElementById('progressContainer' + chartId).style.display = 'none';
                document.getElementById(chartId).classList.remove('is-loading');
            }, 50); 
            
            document.getElementById('waitforserver').close();
        } else if (message.type === 'error') {
            // Fehlerbehandlung
            progressContainer.style.display = 'none';
            alert(`Fehler beim Laden/Verarbeiten der Daten für ${message.chartId}: ${message.error}`);
        }
    }

/* Setzt die Start- und Endzeit basierend auf der aktuellen Uhrzeit und der angegebenen Anzahl von Stunden zurück. Die Zeiten werden im ISO-Format (YYYY-MM-DDTHH:mm) in die entsprechenden Eingabefelder eingefügt. */
function setDatesHours(startId, endId, hh) {
    var now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    document.getElementById(endId).value = now.toISOString().slice(0, 16);

    var begin = new Date();
    begin.setUTCHours(begin.getHours() - hh);
    document.getElementById(startId).value = begin.toISOString().slice(0, 16);
}

/* Konsolidiert alle Tags aus den verschiedenen Diagrammen in einer einzigen Map, um Duplikate zu vermeiden. Nimmt ein Map-Array an.*/
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


/* START DOM-Manipulation */

/* Überprüft die Dauer zwischen Start- und Endzeitpunkt und gibt die Anzahl der Tage aus. Wenn die Dauer größer als 91 Tage oder negativ ist, wird die Ausgabe rot gefärbt. */
function checkDuration(startId, endId, outputId) {
    const start = new Date(document.getElementById(startId).value);
    const end = new Date(document.getElementById(endId).value);
    const duration = (end - start) / 86400000;
    const obj = document.getElementById(outputId)
    obj.innerHTML = duration.toFixed(0) + ' Tage';

    if (duration > 91 || duration < 0) {
        obj.style.color = 'red';
        return false;
    } else {
        obj.style.color = 'inherit';
        return true;
    }
}

/* Setze Zeitparameter in URL Query-String */
function setTimeParams() {
    const url = new URL(window.location.href);
    url.searchParams.set('start', document.getElementById('start').value);
    url.searchParams.set('end', document.getElementById('end').value);
    window.history.pushState(null, '', url.toString())
}

/* Lese Zeitparameter aus URL*/
function getTimeParams() {
    const url = new URL(window.location.href);
    const s = url.searchParams.get('start');
    const e = url.searchParams.get('end');
    console.log(`start ${s}; end ${e}`);
    if (!s || !e || isNaN(new Date(s)) || isNaN(new Date(e))) {
        //console.warn(`URL-Parameter fehlerhaft: start ${s}, end ${e}`);
        return false;
    }
    document.getElementById('start').value = s;
    document.getElementById('end').value = e;
    return true;
}

/* ENDE DOM-Manipulation */

export { initChart, loadChart, setDatesHours, getAllTags, zoom, resetZoom, panX };
export { checkDuration, setTimeParams, getTimeParams };
