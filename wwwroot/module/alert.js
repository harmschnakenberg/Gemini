
function message(adjClass, txt, fadeTime = 5000) {
    let alert = document.getElementById('alert');

    if (!alert) {
        alert = document.createElement('span');
        alert.setAttribute('id', 'alert');
        document.body.insertBefore(alert, document.body.children[0]);
    }

    let msg = document.createElement('div');
    msg.classList.add(adjClass);
    msg.innerHTML = `<b>${txt}</b>`

    setTimeout(function () { msg.remove(); }, fadeTime);

    alert.appendChild(msg);
}

function success(txt) {
    message('success', txt)
}

function warn(txt) {
    message('warn', txt)
}

function error(txt) {
    message('error', txt)
}

export { message, success, warn, error };
