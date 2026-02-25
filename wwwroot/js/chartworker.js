const booleanTags = [];

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
        const newDatasets = processData(chartId, jsonData, aliases, colors);

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


/**
 * Verarbeitet die JSON-Daten vom Server und bereitet sie für Chart.js vor.
 * @param {Array<Object>} jsonData - Array der JsonTag-Objekte ({ N: 'Label', V: 12.3, T: 1672531200000 })
 * @param {Object} aliases - Map-Objekt zur Ersetzung der Label-Namen
 * @param {Array<string>} colors - Array von Farb-Strings
 * @returns {Array<Object>} Die für Chart.js formatierten Datensätze
 */
function processData(chartId, jsonData, aliases, colors) {

    //TEST      
    //colors = await import(`https://${window.location.host}/js/chartTheme1.js`).CHART_COLORS; //TEST
    const REPORT_INTERVAL = 100;// Fortschritts-Schwellwert: Alle 1000 Elemente senden wir ein Update
    // Verwende eine Map, um Datensätze nach Label N zu gruppieren
    const datasetsMap = new Map();
    const totalItems = jsonData.length;
    let colorIndex = 0;    
    let i = 0;

    for (const item of jsonData) {
        const tagName = item.N;
        const tagValue = item.V;
        const timestamp = item.T; // Zeitstempel (z.B. Unix-Zeit in Millisekunden)

        // Ignoriere Einträge ohne gültiges Label oder Wert
        if (!tagName || tagValue === undefined || tagValue === null) {
            continue;
        }

        const isBool = tagName.includes('X') ? true : false;

        // Suche den Datensatz oder erstelle ihn neu
        if (!datasetsMap.has(tagName)) {
            const alias = aliases.get(tagName) || tagName;
            //console.info(`Alias: ${tagName}=${aliases.get(tagName)}`);

            const color = colors[colorIndex % colors.length];
            
            if (isBool && !booleanTags.includes(tagName)) 
                booleanTags.push(tagName);

            const offset = booleanTags.indexOf(tagName);
            
            datasetsMap.set(tagName, {
                label: alias,
                data: [],
                borderColor: color,
                backgroundColor: color,
                stepped: isBool ? true : false, // Macht aus der Linie eine Treppenfunktion
                fill: isBool ? { target: { value: offset }, above: color } : false, // binär Liniendiagramme: Zwischen 'Aus' und 'Ein' füllen               
                pointRadius: 0  // Setze pointRadius auf 0 für viele Datenpunkte
            });
            colorIndex++;
        }

        // Füge den Datenpunkt hinzu. Chart.js Time Scale erwartet {x: timestamp, y: tagValue}
        // Boolsche Werte werden "übereinandergestapelt".
        datasetsMap.get(tagName).data.push({
            x: timestamp,
            y: isBool ? tagValue + booleanTags.indexOf(tagName) : tagValue
        });

        // ******* Fortschritt melden *******
        i++;
        if ((i + 1) % REPORT_INTERVAL === 0 || (i + 1) === totalItems) {
            const percentage = Math.round(((i + 1) / totalItems) * 100);

            // Sende eine Nachricht vom Typ 'progress'
            self.postMessage({
                type: 'progress',
                chartId: chartId,
                percentage: percentage,               
                processedCount: i + 1, // Die Anzahl der bereits verarbeiteten Elemente
                totalCount: totalItems
            });
        }
    }

    // Wandle die Map-Werte in ein Array um
    return Array.from(datasetsMap.values());
}


async function fetchWithCookies(url, options = {}) {
    options.credentials = 'include';
    return fetch(url, options);
}