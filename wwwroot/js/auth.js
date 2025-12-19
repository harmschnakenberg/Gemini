let jwtToken = null;

// Nachrichten vom Client empfangen
self.addEventListener('message', (event) => {
    if (event.data.type === 'SET_TOKEN') {
        jwtToken = event.data.token;
        console.log(`WebWorker Token gesetzt ${jwtToken}`);
    }
});

self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);
    console.log(`WebWorker link nachgehen ${url}`);

    // Nur Anfragen an die eigene API/Domain modifizieren
    if (url.origin === self.location.origin && jwtToken) {
        const modifiedRequest = new Request(event.request, {
            headers: {
                ...Object.fromEntries(event.request.headers.entries()),
                'Authorization': `Bearer ${jwtToken}`
            },
            mode: 'same-origin' // Wichtig für Navigationen
        });

        event.respondWith(fetch(modifiedRequest));
    }
});