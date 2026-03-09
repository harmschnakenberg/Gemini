
let currentCsrfToken = null; // Hier speichern wir das Token global

// Ein Wrapper um fetch, der CSRF automatisch handhabt
export default async function fetchSecure(url, options = {}) {
    // Sicherstellen, dass wir überhaupt ein Token haben (z.B. beim ersten Start)
    if (!currentCsrfToken) {
        await refreshCsrfToken();
    }

    // Standard-Header vorbereiten
    if (!options.headers) options.headers = {};

    // Content-Type setzen, falls JSON gesendet wird
    if (!(options.body instanceof FormData) && !options.headers['Content-Type']) {
        options.headers['Content-Type'] = 'application/json';
    }

    // CSRF Token in Header packen
    options.headers['RequestVerificationToken'] = currentCsrfToken;

    // 1. Versuch: Request senden
    let response = await fetch(url, options);

    // Wenn der Server 400 Bad Request meldet, könnte das Token abgelaufen sein.
    // (ASP.NET Core Antiforgery gibt 400 zurück, wenn das Token nicht stimmt)
    if (response.status === 400) {
        console.warn("400 Fehler erhalten - Versuche Token Refresh...");

        // Versuchen, Token zu erneuern
        const refreshed = await refreshCsrfToken();

        if (refreshed) {
            // Token im Header aktualisieren
            options.headers['RequestVerificationToken'] = currentCsrfToken;

            // 2. Versuch: Request wiederholen
            response = await fetch(url, options);
        }
    }
    /*else
        console.log(`${url} => ${response.status}`) */

    return response;
}

/* Holt ein frisches Token vom Server */
async function refreshCsrfToken() {
    try {
        const response = await fetch('/antiforgery/token', { method: 'GET' });
        if (response.ok) {     
            console.log("🔐 Token erneuert."); //, currentCsrfToken
            return true;
        }
    } catch (e) {
        console.error("🔓 Konnte Token nicht erneuern.", e);
    }
    return false;
}

