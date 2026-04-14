export default function createSvgInstance(obj) {
    //  <g class="wertanzeige" x="1220" y="410" data-svg='' data-name='A01_DB10_DBW6' data-unit='s'></g>
    const x = obj.getAttribute('x');
    const y = obj.getAttribute('y');
    const svgName = obj.getAttribute('data-svg');
    const tagUnit = obj.getAttribute('data-unit');
    const tagName = obj.getAttribute('data-name');
    const svg = document.getElementById('svg-canvas');
    const template = document.getElementById(svgName);

    if (!template) {
        console.warn(`Vorlage '${svgName}'' nicht gefunden`);
        return;
    }

    console.info(`Erzeuge Instanz ${svgName} mit ${tagName} ${tagUnit}`);

    // 1. Echte Kopie erstellen (deep clone)
    const instance = template.cloneNode(true);
    instance.removeAttribute('id'); // ID entfernen, damit sie nicht mehrfach vorkommt
    instance.classList = obj.classList;

    // 2. Individuelle Texte setzen
    if (tagName) {
        let obj = instance.querySelector('.valuedisplay');
        if (obj) {
            obj.setAttribute('data-name', tagName);
            obj.setAttribute('data-value', 0);
            svgAnimate(obj);
        }
    }

    if (tagUnit)
        instance.querySelector('.unitdisplay').textContent = tagUnit;

    // 3. Positionieren (via transform)
    if (x && y)
        instance.setAttribute('transform', `translate(${x}, ${y})`);

    // 4. In das SVG einfügen
    svg.appendChild(instance);
    obj.remove();
}

function svgAnimate(obj) {
    //Auf Wertänderungen
    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            // Prüfen, ob genau unser Daten-Attribut geändert wurde
            if (mutation.attributeName === 'data-value') {
                let val = obj.getAttribute('data-value');               
                //console.info(`data-value ${val} ${typeof (val)}`);

                if (obj.tagName == 'text' || obj.tagName == 'tspan')
                    obj.textContent = val;

                if (obj.tagName == 'circle')
                    if (val == 'true')
                        obj.style.fill = 'var(--activeBgColor)';
                    else
                        obj.style.fill = 'var(--passiveBgColor)';

                //const boolValue = isNaN(newValue) && (newValue.toLowerCase() === 'true');
                //// Logik für den Farbumschlag
                //if (boolValue) {
                //    obj.style.fill = 'green';    // Kritisch
                //} else {
                //    obj.style.fill = 'red';  // OK
                //}

                // console.log(`Wert geändert auf: ${newValue}`);
            }
        });
    });

    // 3. Den Observer starten (wir beobachten nur Attributänderungen)
    observer.observe(obj, { attributes: true });
}
