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
    if (x && y)
        instance.setAttribute('transform', `translate(${x}, ${y})`);

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

function animateIstwert(svgObj, val) {
	svgObj.querySelector('.valuedisplay').textContent = val;
}

function animatePumpe(svgObj, val) {
    if (val == 'true')
        svgObj.querySelector('.valuedisplay').style.fill = 'var(--activeBgColor)';
    else
        svgObj.querySelector('.valuedisplay').style.fill = 'var(--passiveBgColor)';
}

function animateVessel(svgObj, val) {
    val = 1 - val / 100;
    svgObj.querySelectorAll('stop').forEach(function (stop) {
        stop.setAttribute("offset", val);
    });
}
