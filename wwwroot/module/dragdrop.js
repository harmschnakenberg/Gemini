let draggingItem = null;
let list = document.getElementById('sortable-list');

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
    inputField.setAttribute('id', 'c' + document.getElementsByTagName('li').length);
    inputField.value = text;
    inputField.classList.add('myButton');
    inputField.setAttribute('readonly', 'readonly');
    inputField.style.width = '42rem';
    inputField.addEventListener('blur', function () { isValid(this) });

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

function isValid(input) {
    const ops = document.querySelectorAll('#comments > OPTION');
    input.style.backgroundColor = 'white';

    for (let i = 0; i < ops.length; i++) {
        if (ops[i].value == input.value)
            return;
    }

    input.style.backgroundColor = 'rgba(255, 99, 71, 0.5)';
}


export { addItem, setupDragAndDrop }