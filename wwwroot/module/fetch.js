
let currentCsrfToken = null; // Hier speichern wir das Token global

// Hilfsfunktion: Header kompatibel setzen (Plain object oder Headers)
function setHeader(headers, name, value) {
    if (!headers) return;
    if (headers instanceof Headers) {
        headers.set(name, value);
    } else {
        headers[name] = value;
    }
}

// Ein Wrapper um fetch, der CSRF automatisch handhabt
export default async function fetchSecure(url, options = {}) {
    // Default options
    options.method = options.method || 'GET';
    options.credentials = options.credentials || 'include'; // WICHTIG: Cookies mitschicken

    // Sicherstellen, dass wir überhaupt ein Token haben (z.B. beim ersten Start)
    if (!currentCsrfToken) {
        const ok = await refreshCsrfToken();
        if (!ok) {
            // Kein Token verfügbar — optional: Request abbrechen oder ohne Token versuchen
            console.warn('Kein CSRF-Token verfügbar, sende Request trotzdem (erfolglos möglich).');
        }
    }

    // Standard-Header vorbereiten
    if (!options.headers) options.headers = {};

    // Content-Type setzen, falls JSON gesendet wird
    if (!(options.body instanceof FormData) && !options.headers['Content-Type']) {
        options.headers['Content-Type'] = 'application/json';
    }

    // CSRF Token in Header packen nur für unsichere Methoden
    if (currentCsrfToken && isUnsafeMethod(options.method)) {
        setHeader(options.headers, 'RequestVerificationToken', currentCsrfToken);
    }

    // 1. Versuch: Request senden
    let response = await fetch(url, options);

    // Wenn der Server 400 Bad Request meldet, könnte das Token abgelaufen sein.
    // (ASP.NET Core Antiforgery gibt 400 zurück, wenn das Token nicht stimmt)
    if (response.status === 400 && isUnsafeMethod(options.method)) {
        console.warn("400 Fehler erhalten - Versuche Token Refresh...");

        // Versuchen, Token zu erneuern
        const refreshed = await refreshCsrfToken();

        if (refreshed) {
            // Token im Header aktualisieren
            setHeader(options.headers, 'RequestVerificationToken', currentCsrfToken);

            // 2. Versuch: Request wiederholen
            response = await fetch(url, options);
        }
    }

    return response;
}


function getHeader(headers, name) {
    if (!headers) return undefined;
    if (headers instanceof Headers) return headers.get(name);
    // normalize key lookup
    const key = Object.keys(headers).find(k => k.toLowerCase() === name.toLowerCase());
    return key ? headers[key] : undefined;
}

function isUnsafeMethod(method) {
    const m = (method || '').toUpperCase();
    return !(m === 'GET' || m === 'HEAD' || m === 'OPTIONS' || m === 'TRACE');
}


/* Holt ein frisches Token vom Server */
async function refreshCsrfToken() {
    try {
        const response = await fetch('/antiforgery/token', { method: 'GET', credentials: 'include' });
        if (response.ok) {
            // Erwartet JSON: { requestToken: "..."} oder { RequestToken: "..." }
            const data = await response.json().catch(() => null);
            if (data) {
                // flexible Feldnamen prüfen
                currentCsrfToken = data.requestToken ?? data.RequestToken ?? data.token ?? null;
                if (currentCsrfToken) {
                    console.log("🔐 Token erneuert.");
                    return true;
                }
            }
            console.warn('Token-Antwort ohne gültiges Feld erhalten.');
        } else {
            console.warn('Token-Refresh nicht ok, Status:', response.status);
        }
    } catch (e) {
        console.error("🔓 Konnte Token nicht erneuern.", e);
    }
    return false;
}

