function addCol() {    
    const i = document.forms['myForm'].getElementsByTagName('li').length;



    var y = document.createElement('LI');
    y.setAttribute('id', 'li' + i);

    var u = document.createElement('SPAN');
    u.classList.add('upArrow');
    u.innerHTML = "&#8593;";
    u.addEventListener('click', function () { this.parentNode.nextSibling.addChild(this.parentNod); });
    y.appendChild(u)

    var d = document.createElement('SPAN');
    d.classList.add('upArrow');
    d.innerHTML = "&#8595;";
    d.addEventListener('click', function () { this.parentNode.nextSibling.insertBefore(this.parentNode, this.parentNode.nextSibling) });
    y.appendChild(d)

    var x = document.createElement('INPUT');      
    x.setAttribute('list', 'comments');
    x.setAttribute('name', 'col' + i);
    x.setAttribute('id', 'col' + i);
    x.classList.add('colForTable');
    y.appendChild(x)
    
    document.getElementById('myForm').getElementsByTagName('ol')[0].appendChild(y);
}

document.addEventListener('DOMContentLoaded', (event) => {
    // Initialisierung der Drag-and-Drop-Funktionalität für vorhandene Elemente
    setupDragAndDrop();
});

const list = document.getElementById('sortable-list');
let draggingItem = null;

function setupDragAndDrop() {
    // Event Listener für alle sortierbaren Elemente hinzufügen
    document.querySelectorAll('.sortable-item').forEach(item => {
        item.addEventListener('dragstart', handleDragStart);
        item.addEventListener('dragend', handleDragEnd);
        item.addEventListener('dragover', handleDragOver);
        // dragenter und dragleave sind nützlich für visuelles Feedback,
        // aber dragover reicht für die Sortierung.
    });
}

function handleDragStart(e) {
    draggingItem = e.target;
    e.dataTransfer.effectAllowed = 'move';
    // Verzögert das Hinzufügen der dragging Klasse, damit ein "Ghost"-Bild entsteht
    setTimeout(() => {
        e.target.classList.add('dragging');
    }, 0);
}

function handleDragEnd(e) {
    e.target.classList.remove('dragging');
    draggingItem = null;
    // Entfernt alle 'over' Klassen von allen Elementen am Ende des drags
    document.querySelectorAll('.sortable-item').forEach(item => {
        item.classList.remove('over');
    });
}

function handleDragOver(e) {
    e.preventDefault(); // Notwendig, damit das drop Event funktioniert
    const afterElement = getDragAfterElement(list, e.clientY);

    // Visuelles Feedback hinzufügen
    document.querySelectorAll('.sortable-item').forEach(item => {
        item.classList.remove('over');
    });
    if (afterElement) {
        afterElement.classList.add('over');
    }

    // Element an der neuen Position einfügen
    if (afterElement == null) {
        list.appendChild(draggingItem);
    } else {
        list.insertBefore(draggingItem, afterElement);
    }
}

// Hilfsfunktion zur Ermittlung des Elements, nach dem das gezogene Element platziert werden soll
function getDragAfterElement(container, y) {
    const draggableElements = [...container.querySelectorAll('.sortable-item:not(.dragging)')];

    return draggableElements.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offset = y - box.top - box.height / 2;
        // Wenn der Offset kleiner 0 ist und näher an der Mitte als das bisherige "closest" Element...
        if (offset < 0 && offset > closest.offset) {
            return { offset: offset, element: child };
        } else {
            return closest;
        }
    }, { offset: Number.NEGATIVE_INFINITY }).element;
}


// --- Funktionen zum Hinzufügen und Löschen von Elementen ---

function addItem() {
    const input = document.getElementById('newItemInput');
    const value = input.value.trim();

    if (value !== '') {
        createListItem(value);
        input.value = '';
    }
}

function createListItem(text) {
    const li = document.createElement('li');
    li.classList.add('sortable-item');
    li.setAttribute('draggable', true); // Macht das Element drag-fähig

    const inputField = document.createElement('input');
    inputField.setAttribute('type', 'text');
    inputField.setAttribute('list', 'comments');
    inputField.setAttribute('id', 'c'+ document.getElementsByTagName('li').length);
    inputField.value = text;
    inputField.classList.add('item-content');
    inputField.addEventListener('blur', function () { isValid(this) } );

    const deleteBtn = document.createElement('button');
    deleteBtn.textContent = 'Löschen';
    deleteBtn.classList.add('delete-btn');
    deleteBtn.onclick = function () {
        li.remove(); // Löscht das Listenelement direkt
    };

    li.appendChild(inputField);
    li.appendChild(deleteBtn);
    list.appendChild(li);

    // Wichtig: Die Drag-and-Drop-Listener müssen für das neue Element registriert werden
    li.addEventListener('dragstart', handleDragStart);
    li.addEventListener('dragend', handleDragEnd);
    li.addEventListener('dragover', handleDragOver);

    isValid(inputField);
}

function setDatesToStratOfMonth(startId, endId) {
    var now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    document.getElementById(endId).value = now.toISOString().slice(0, 16);

    var begin = new Date();
    begin.setUTCMonth(begin.getUTCMonth() - 1, 1)
    begin.setUTCHours(0, 0, 0);
    document.getElementById(startId).value = begin.toISOString().slice(0, 16);
}

function isValid(input) {    
    const ops = document.querySelectorAll('#comments > OPTION');
    input.style.backgroundColor = 'white';

    for (let i = 0; i < ops.length; i++) {
        if (ops[i].value == input.value) 
            return;        
    }

    input.style.backgroundColor = 'rgba(255, 99, 71, 0.5)';
}

const allTagComments = new Map();

async function loadComments() {
    const link = `/tagcomments`;
    const response = await fetch(link, {
        method: 'POST',
        headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json'
        },
        body: "?"
    });

    if (!response.ok) {
        throw new Error(`Response status: ${response.status}`);
        return;
    }

    const json = await response.json();
  
    json.forEach((t) => {        
        allTagComments.set(t.V?.length > 3 ? t.V : t.N, t.N);
        const para = document.createElement("OPTION");
        //console.info(`# ${t.V}=${t.N}`);       
        para.setAttribute("value", t.V?.length > 3 ? t.V : t.N);        
        document.getElementById('comments').appendChild(para);
    });

}

function getExcelFromForm() {
    const inputs = document.querySelectorAll('.sortable-item > input');
    const interval = document.getElementById('interval').value;
    const tags = new Map();

    for (let i = 0; i < inputs.length; i++) {
        const comment = inputs[i].value;
        const tagName = allTagComments.get(comment);
        //console.info(`${allTagComments.size} hinzufügen: ${tagName}=${comment}`)
        tags.set(tagName, comment);       
    }

    //for (const x of tags.keys()) {
    //    console.info(`Tag zur Übergabe: ${x} = ${tags.get(x)}`);
    //}

    //console.info(`Übergebene Tags: ${tags.size}`);

    if (tags.size > 0)
        excelExport('start', 'end', interval, tags);
    else
        console.warn("Keine Tags für Excel-Export ausgewählt.");
}
