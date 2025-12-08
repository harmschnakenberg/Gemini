// Importieren der Chart.js Bibliothek im Worker
importScripts('https://cdn.jsdelivr.net/npm/chart.js@4.5.1');

let chart = null; // Variable, um die Chart-Instanz zu speichern

onmessage = function (event) {
    const messageType = event.data.type;

    if (messageType === 'INIT') {
        console.info("Worker initialisiert");
        const canvas = event.data.canvas;
        const initialConfig = {
            type: 'line',
            data: {
                labels: [], // Startet leer
                datasets: [{
                    label: 'Geladene Daten',
                    data: [], // Startet leer
                    backgroundColor: 'rgba(75, 192, 192, 0.2)',
                    borderColor: 'rgba(75, 192, 192, 1)',
                    borderWidth: 1
                }]
            },
            options: {
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        };

        chart = new Chart(canvas, initialConfig);

    } else if (messageType === 'UPDATE_DATA') {
        // Die empfangenen Daten aus dem Haupt-Thread
        const newData = event.data.data;
        console.info("Worker empfangen: " + newData);

        if (chart && newData && newData.labels && newData.values) {

            newData.forEach((item) => {
                const dsIdx = ensureDataset(chartId, item.N, tags);
                const ds = chartsMap[chartId].data.datasets[dsIdx];
                ds.data.push({ x: item.T, y: item.V });
            });

            // Aktualisiere die Daten des Diagramms im Worker
            chart.data.labels = newData.labels;
            chart.data.datasets.data = newData.values;

            // Diagramm im Worker neu rendern
            chart.update();
            console.log('Chart im Worker aktualisiert mit Fetch-Daten.');
        }
    }
};

// neuen Stift erstellen, wenn Dataset nicht existiert 
function ensureDataset(chartId, name, tags) {
    if (datasetsMap.hasOwnProperty(name)) {
        return datasetsMap[name];
    }

    if (!chartsMap.hasOwnProperty(chartId)) {
        //console.info(`ensureDataset(${chartId}, name) initiiert Chart.`);
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