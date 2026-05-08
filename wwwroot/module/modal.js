import fetchSecure from '../module/fetch.js';

/*
 * @param {any} btnObj           // btnObj: button that opens the modal
 * @param {any} link             // Link zum Sollwertfenster
 */

export default async function openModal(btnObj, link) {
    // Modal dynamisch im DOM erzeugen, aber Werte erst bai Aufruf vom Server abrufen

    // Get the modal
    const modalId = link.replaceAll("/", "_");;    
    var modal = document.getElementById(modalId);

    if (modal) return;

    modal = document.createElement("div");
    modal.id = modalId;
    modal.className = "modal";
    modal.classList.add("modal-empty");
    modal.addEventListener('contextmenu', (e) => {
        e.preventDefault(); // Rechtsklick-Menü deaktivieren
    });

    const modalContent = document.createElement("div");
    modalContent.className = "modal-content";
    
    const closeSpan = document.createElement("span");
    closeSpan.className = "close";
    closeSpan.innerHTML = "&#10761;";
    closeSpan.onclick = function () {
        modal.style.display = "none";
    }

    const rightClickSpan = document.createElement("span");
    rightClickSpan.id = "popupPan";
    rightClickSpan.className = "close";
    rightClickSpan.innerHTML = "&target;";

    modalContent.addEventListener('mousedown', (e) => { 
        // RECHTE Maustaste (button 2) oder Verschiebe-Symbol im Popup
        if (e.button !== 2 && e.target.id !== "popupPan") return;

        // Verhindert, dass der Text im Element markiert wird
        e.preventDefault();
        const offsetX = e.clientX - modalContent.getBoundingClientRect().left;
        const offsetY = e.clientY - modalContent.getBoundingClientRect().top;
        modalContent.style.position = 'absolute'; //setzt Element auf x:0 y:0
        move(e); 

        function move(e) {
            modalContent.style.left = (e.clientX - offsetX) + 'px';
            modalContent.style.top = (e.clientY - offsetY) + 'px';
        }

        document.addEventListener('mousemove', move);

        document.addEventListener('mouseup', () => {
            document.removeEventListener('mousemove', move);
        }, { once: true });
    });

    modalContent.appendChild(closeSpan);
    modalContent.appendChild(rightClickSpan);
    modal.appendChild(modalContent);
    document.body.appendChild(modal);

    // When the user clicks anywhere outside of the modal, close it
    window.onclick = function (event) {
        if (event.target == modal) {
            modal.style.display = "none";
        }
    }

    // When the user clicks the btnObj, open the modal
    btnObj.onclick = function () {
        modal.style.display = "block";

        //Inhalt nur beim ersten Aufruf des Modals laden
        if (modal.classList.contains("modal-empty")) {
            modal.classList.remove("modal-empty")
            populateModal(modalId, link);             
        }
    }

    /*
    <!-- The Modal -->
    <div id="myModal" class="modal">

        <!-- Modal content -->
        <div class="modal-content">
            <span class="close">&times;</span>
            <p>Some text in the Modal..</p>
        </div>

    </div>
    */
}

/* Inhalt des Modals vom Server laden */
async function populateModal(modalId, link) {

    console.log(`Modal ${link}`);
    let html = '?';
    try {
        const res = await fetchSecure(link);
        if (!res.ok)
            throw new Error(`Modal Response status: ${res.status}`);

        html = await res.text();
        //console.log(html);

    } catch (error) {
        console.error('Fehler beim Laden des Modals:', error);
    }
  
    const p = document.createElement("p");
    //p.innerHTML = new Date();
    const start = html.search("<body>") + 6;
    const end = html.search("</body>");
    p.innerHTML = html.substring(start, end);

    //Get Modal
    var modal = document.getElementById(modalId);
    modal
        .querySelector(".modal-content")
        .appendChild(p);

    data.initUnits();
    data.initWebsocket(data.initTags());
}