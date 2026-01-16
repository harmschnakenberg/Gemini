/**
 * Verarbeitet die JSON-Daten vom Server und bereitet sie für Chart.js vor.
 * @param {Array<Object>} jsonData - Array der JsonTag-Objekte ({ N: 'Label', V: 12.3, T: 1672531200000 })
 * @param {Object} aliases - Map-Objekt zur Ersetzung der Label-Namen
 * @param {Array<string>} colors - Array von Farb-Strings
 * @returns {Array<Object>} Die für Chart.js formatierten Datensätze
 */
function processData(jsonData, aliases, colors) {
    const REPORT_INTERVAL = 10;// Fortschritts-Schwellwert: Alle 1000 Elemente senden wir ein Update
    // Verwende eine Map, um Datensätze nach Label N zu gruppieren
    const datasetsMap = new Map();
    const totalItems = jsonData.length;
    let colorIndex = 0;    
    let i = 0;

    for (const item of jsonData) {
        const label = item.N;
        const value = item.V;
        const timestamp = item.T; // Zeitstempel (z.B. Unix-Zeit in Millisekunden)

        // Ignoriere Einträge ohne gültiges Label oder Wert
        if (!label || value === undefined || value === null) {
            continue;
        }

        // Suche den Datensatz oder erstelle ihn neu
        if (!datasetsMap.has(label)) {
            const alias = aliases.get(label) || label;
            //console.info(`Alias: ${label}=${aliases.get(label)}`);

            const color = colors[colorIndex % colors.length];

            datasetsMap.set(label, {
                label: alias,
                data: [],
                borderColor: color,
                backgroundColor: color,
                fill: false, // Für Liniendiagramme
                // Setze pointRadius auf 0 für viele Datenpunkte
                pointRadius: 0
            });
            colorIndex++;
        }

        // Füge den Datenpunkt hinzu. Chart.js Time Scale erwartet {x: timestamp, y: value}
        datasetsMap.get(label).data.push({
            x: timestamp,
            y: value
        });

        // ******* NEU: Fortschritt melden *******
        i++;
        if ((i + 1) % REPORT_INTERVAL === 0 || (i + 1) === totalItems) {
            const percentage = Math.round(((i + 1) / totalItems) * 100);

            // Sende eine Nachricht vom Typ 'progress'
            self.postMessage({
                type: 'progress',
                percentage: percentage,
                // Optional: Die Anzahl der bereits verarbeiteten Elemente
                processedCount: i + 1,
                totalCount: totalItems
            });
        }
    }

    // Wandle die Map-Werte in ein Array um
    return Array.from(datasetsMap.values());
}

/**
 * Web Worker: Empfängt Nachrichten vom Haupt-Thread.
 */
self.onmessage = async function (e) {
    const { chartId, url, aliases, colors } = e.data;

    try {
        // 1. Daten asynchron laden
        //const response = await fetch(url);
        const response = await fetchWithCookies(url);
        if (!response.ok) {
            throw new Error(`HTTP-Fehler! Status: ${response.status}`);
        }
        const jsonData = await response.json();

        // 2. Daten verarbeiten (kann bei vielen Daten rechenintensiv sein)
        const newDatasets = processData(jsonData, aliases, colors);

        // 3. Verarbeitete Daten zurück an den Haupt-Thread senden
        self.postMessage({
            type: 'complete',
            chartId: chartId,
            datasets: newDatasets
        });

    } catch (error) {
        console.error('Fehler im Web Worker:', error);
        // Sende eine Fehlermeldung oder leere Daten zurück, um den Haupt-Thread zu informieren
        self.postMessage({ error: error.message, chartId: chartId, datasets: [] });
    }
};

async function fetchWithCookies(url, options = {}) {
    options.credentials = 'include';
    return fetch(url, options);
}