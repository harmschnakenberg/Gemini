function addCol() {    
    const i = document.forms['myForm'].getElementsByTagName('li').length;

    var y = document.createElement('LI');
    y.setAttribute('id', 'li' + i);

    y.ondrop = function (event) {
        event.preventDefault();
        const data = event.dataTransfer.getData("Text");
        event.target.appendChild(document.getElementById(data));
    };
    y.ondragover = function (event) { event.preventDefault(); };

    var x = document.createElement('INPUT');   
    x.setAttribute('dragable', 'true');
    x.setAttribute('list', 'comments');
    x.setAttribute('name', 'col' + i);
    x.setAttribute('id', 'col' + i);
    x.classList.add('colForTable');
    x.ondragstart = function (event) {
        event.dataTransfer.setData("Text", event.target.id);
        console.log(event.target);
    };
    document.getElementById('myForm').getElementsByTagName('ol')[0].appendChild(y).appendChild(x);
}

function dragstartHandler(ev) {
    ev.dataTransfer.setData('text', ev.target.id);
}

function dragoverHandler(ev) {
    ev.preventDefault();
}

function dropHandler(ev) {
    ev.preventDefault();
    const data = ev.dataTransfer.getData('text');
    ev.target.appendChild(document.getElementById(data));
}
