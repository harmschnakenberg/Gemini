import fetchSecure from '../module/fetch.js';

async function initSvg() {
    const objs = document.querySelectorAll('[data-svg]')
    if (objs.length == 0)
        return;

    //Templates aus externer Datei laden
    await fetchSecure('/html/bild/magazin.svg', {
        method: 'GET',
        headers: { "Content-Type": "image/svg+xml" }
    })
        .then(response => response.text())
        .then(svgText => {
            // Text in ein DOM-Dokument umwandeln
            const parser = new DOMParser();
            const externalDoc = parser.parseFromString(svgText, "image/svg+xml");
            const newSvg = externalDoc.querySelector("svg");
            const svgObj = document.getElementById('svg-canvas');

            if (newSvg && svgObj)
                svgObj.insertBefore(newSvg.children[0], svgObj.children[0]);
        });

    for (let i = 0; i < objs.length; i++) {
        createSvgInstance(objs[i])
    }

    hideSmallDisplays(); // Initialer Check, falls Start-Zoom zu klein ist
}

const svg = document.getElementById('svg-canvas');
//const anim = document.getElementById('viewbox-anim');
const INITIAL_VB = { x: 0, y: 0, w: 1920, h: 1080 };

const CONFIG = {
    minWidth: 200, maxWidth: 4000,
    sensitivity: 0.15, // Stärke für Mausrad
    stepSize: 0.2,     // Stärke pro Button-Klick (20%)
    limitX: [-INITIAL_VB.w , INITIAL_VB.w ], limitY: [-INITIAL_VB.h / 2, INITIAL_VB.h / 2]
};

let viewBox = { ...INITIAL_VB };
let isPanning = false;
let startPoint = { x: 0, y: 0 };
let zoomFactor = 1;

const clamp = (val, range) => Math.max(range[0], Math.min(range[1], val));

function getSVGPoint(e) {
    const p = svg.createSVGPoint();
    p.x = e.clientX; p.y = e.clientY;
    return p.matrixTransform(svg.getScreenCTM().inverse());
}

function updateViewBox() {
    svg.setAttribute('viewBox', `${viewBox.x} ${viewBox.y} ${viewBox.w} ${viewBox.h}`);
}

// Gemeinsame Zoom-Logik
function performZoom(delta, centerX, centerY) {
    const newWidth = viewBox.w * delta;
    if (newWidth > CONFIG.minWidth && newWidth < CONFIG.maxWidth) {
        viewBox.w = newWidth;
        viewBox.h = newWidth * (INITIAL_VB.h / INITIAL_VB.w);
        viewBox.x = clamp(centerX - (centerX - viewBox.x) * delta, CONFIG.limitX);
        viewBox.y = clamp(centerY - (centerY - viewBox.y) * delta, CONFIG.limitY);
        updateViewBox();

        hideSmallDisplays();
    }
}


if (svg) {
    // BUTTON EVENTS

    document.getElementById('fullscreen').addEventListener('click', () => {
        // Vollbild aktivieren
        enterFullscreen();
    });

    document.getElementById('zoom-in').addEventListener('click', () => {
        // Zoom zur Mitte der aktuellen ViewBox
        performZoom(1 - CONFIG.stepSize, viewBox.x + viewBox.w / 2, viewBox.y + viewBox.h / 2);
    });

    document.getElementById('zoom-out').addEventListener('click', () => {
        performZoom(1 + CONFIG.stepSize, viewBox.x + viewBox.w / 2, viewBox.y + viewBox.h / 2);
    });

    // MAUSRAD ZOOM
    svg.addEventListener('wheel', (e) => {
        e.preventDefault();
        const mousePos = getSVGPoint(e);
        const delta = e.deltaY > 0 ? (1 + CONFIG.sensitivity) : (1 - CONFIG.sensitivity);
        performZoom(delta, mousePos.x, mousePos.y);
    }, { passive: false });

    // PANNING (Ziehen)
    svg.addEventListener('pointerdown', (e) => {
        isPanning = true;
        startPoint = getSVGPoint(e);
        svg.style.cursor = 'grabbing';
    });

    window.addEventListener('pointermove', (e) => {
        if (!isPanning) return;
        const currentPoint = getSVGPoint(e);
        viewBox.x = clamp(viewBox.x - (currentPoint.x - startPoint.x), CONFIG.limitX);
        viewBox.y = clamp(viewBox.y - (currentPoint.y - startPoint.y), CONFIG.limitY);
        updateViewBox();
    });

    window.addEventListener('pointerup', () => {
        isPanning = false;
        svg.style.cursor = 'grab';
    });

    // RESET (Stabile Version ohne <animate>-Konflikt)
    document.getElementById('reset-btn').addEventListener('click', () => {
        // 1. Interne Daten zurücksetzen
        viewBox = { ...INITIAL_VB };

        // 2. ViewBox direkt setzen
        updateViewBox();

        // Optional: Falls du die Animation behalten willst,
        // müsstest du das <animate>-Element nach Ablauf entfernen.
        // Einfacher ist es jedoch, direkt zuzuweisen.
    });
}

function hideSmallDisplays() {
    const el = document.querySelectorAll('.wertanzeige');
    //console.info(`${el.length} Wertanzeigen gefunden`);
    for (let i = 0; i < el.length; i++) {
        const { width } = el[i].getBoundingClientRect();
        //console.info(el[i].getBoundingClientRect());

        if (width < 50) {
            el[i].style.visibility = "hidden";
        } else {
            el[i].style.visibility = "initial";
        }
    }
}

function enterFullscreen() {
    const button = document.getElementById('fullscreen')
    var doc = document.documentElement;
    if (!document.fullscreenElement) {
        if (doc.requestFullscreen) {
            doc.requestFullscreen();
        } else if (doc.webkitRequestFullscreen) {
            doc.webkitRequestFullscreen(); // Für iOS/Safari
        }

        button.innerHTML = '◱';
    } else {
        // Vollbildmodus verlassen
        document.exitFullscreen();
        button.innerHTML = '⛶';
    }
    
}



/*  ENDE Zoom */

function createSvgInstance(obj) {
    //  <g class="wertanzeige" x="1220" y="410" data-svg='' data-name='A01_DB10_DBW6' data-unit='s'></g>
    const x = obj.getAttribute('x');
    const y = obj.getAttribute('y');
    const svgName = obj.getAttribute('data-svg');
    const tagUnit = obj.getAttribute('data-unit');
    const tagName = obj.getAttribute('data-name');
    const svgRotate = obj.getAttribute('data-rotate');
    const svg = document.getElementById('svg-canvas');
    const template = document.getElementById(svgName);

    if (!template) {
        console.warn(`Vorlage '${svgName}'' nicht gefunden`);
        return;
    }

    // console.info(`Erzeuge Instanz ${svgName} mit ${tagName} ${tagUnit}`);

    // 1. Echte Kopie erstellen (deep clone)
    const instance = template.cloneNode(true);
    instance.setAttribute('data-svg', template.id);    
    instance.removeAttribute('id'); // ID entfernen, damit sie nicht mehrfach vorkommt
    instance.setAttribute('data-name', tagName);    
    instance.classList = obj.classList;

    // 2. Einmalige Änderungen vor der Animation
    switch(svgName){
        case 'vessel':
            const gradId = 'grad' + Math.floor(Math.random() * (999 - 99 + 1) + 99);
            instance.querySelector('linearGradient').id = gradId;
            instance.querySelector('rect').setAttribute("fill", 'url(#' + gradId + ')');
            break;
        case 'istwert':
            if (tagUnit)
                instance.querySelector('.unitdisplay').textContent = tagUnit;
            break;
    }

	// 3. Animation hinzufügen
    svgAnimate(instance);

    // 4. Positionieren (via transform)
    let rotate = '';
    if (svgRotate)
        rotate = `rotate(${svgRotate})`;

    if (x && y)
        instance.setAttribute('transform', `translate(${x}, ${y}) ${rotate}`);
        
    // 5. In das SVG einfügen
    svg.appendChild(instance);
    obj.remove();
}

function svgAnimate(svgObj) {
    //Auf Wertänderungen
    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            // Prüfen, ob genau unser Daten-Attribut geändert wurde            
                let val = svgObj.getAttribute('data-value');   
                let svgName = svgObj.getAttribute('data-svg');                 
                //console.info(`data-svg  ${svgName}, data-value ${val} `);

                switch (svgName) {
                    case 'istwert':
						animateIstwert(svgObj, val);                        
                        break;
                    case 'pumpe':
                    case 'ventilator':
                    case 'verdichter':
                    case 'ventil':    //Ventilkörper Durchfluss links-rechts
                    case 'mv':        //Magnetventil über Ventilkörper
                    case 'antrieb':   //Motorantrieb über Ventilkörper, Klappe, ...
                    case 'antrieb-r':   //Motorantrieb rechts von Ventilkörper, Klappe, ...
                        animatePumpe(svgObj, val)               
                        break;
                    case 'vessel':                        
						animateVessel(svgObj, val)
                        break;                  
                }            
        });
    });

    // 3. Den Observer starten (wir beobachten nur Attributänderungen)
    observer.observe(svgObj, { attributeFilter: ["data-value"] });
}

/* Istwert-Anzaige (Zahl + Einheit) */
function animateIstwert(svgObj, val) {
	svgObj.querySelector('.valuedisplay').textContent = val;
}

/* Farbumschlag Hintergrundfarbe grün/grau */
function animatePumpe(svgObj, val) {
    if (val == 'true')
        svgObj.querySelector('.valuedisplay').style.fill = 'var(--activeBgColor)';
    else
        svgObj.querySelector('.valuedisplay').style.fill = 'var(--passiveBgColor)';
}

/* Füllstandsanimation */
function animateVessel(svgObj, val) {
    val = 1 - val / 100;
    svgObj.querySelectorAll('stop').forEach(function (stop) {
        stop.setAttribute("offset", val);
    });
}

export { initSvg, createSvgInstance, enterFullscreen }