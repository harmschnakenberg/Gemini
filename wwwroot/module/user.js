import fetchSecure from '../module/fetch.js';

const API_URL = 'https://' + window.location.host;
const LAST_USER = 'lastUser';
const TOKEN_NAME = 'RequestVerificationToken';
let timer;

async function login() {
    const body = document.getElementsByTagName('BODY')[0];
    const el = document.getElementById('loginMessage');
    el.textContent = 'Logge ein...';

    const userName = document.getElementById("username").value;
    const userToken = document.getElementById("password").value;

    try {
        body.style.cursor = 'wait';
        const response = await fetchSecure(`${API_URL}/login`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ userName, userToken })
        });

        if (response.ok) {
            const data = await response.json();         
            const csrfToken = data.userToken;
            const userInfo = {
                lastUser: userName,
                expiry: new Date().getTime() + 60 * 60 * 1000, // Token läuft in 60 Min. ab
            }

            sessionStorage.setItem(TOKEN_NAME, csrfToken);            
            localStorage.setItem(LAST_USER, JSON.stringify(userInfo));
            el.textContent = userName;
            el.style.color = 'green';     
            clearTimer();   
            setTimer(userInfo.expiry);
        } else {
            el.textContent = 'Login fehlgeschlagen. Überprüfe Anmeldedaten.';
            el.style.color = 'red';            
        }
    } catch (e) { console.error("Fehler beim Login: " + e); }
    finally {
        body.style.cursor = 'auto';        
    }
               
}

async function logout() {
    const body = document.getElementsByTagName('BODY')[0];
    const el = document.getElementById('loginMessage');
    el.textContent = 'Logge aus...';
    body.style.cursor = 'wait';

    const userName = JSON.parse(localStorage.getItem(LAST_USER))?.lastUser;
    const userToken = sessionStorage.getItem(TOKEN_NAME);
    const response = await fetchSecure(`${API_URL}/logout`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ userName, userToken })
    });

    if (response.ok) {        
        if (userName) {            
            deleteUserStorage(el);
            el.textContent = userName + ' ausgeloggt';
            el.style.color = 'red';
        }
    }
    else {
        el.textContent = `'${userName}' ausloggen fehlgeschlagen. Status ${response.status}`;
        el.style.color = 'orange';
    }

    body.style.cursor = 'auto';
}

 // Funktion zum Überprüfen des Login-Status beim Laden der Seite
function checkLoginStatus() {
    let isLoggedIn = false;
    let span = document.getElementById('loginMessage');

    if (!span) {
        span = document.createElement('span');
        span.setAttribute('id', 'loginMessage');
        span.style.padding = '0.2rem 0.5rem';
        span.style.border = '1px solid grey';
        span.style.borderRadius = '0.5rem';
        span.textContent = 'Kein Benutzer';

        let t = document.createElement('span');
        t.setAttribute('id', 'userTimeout');

        const a = document.createElement('a');
        a.setAttribute('href', '/');
        a.appendChild(span);
        a.appendChild(t);

        document.body.appendChild(a);
    }

    const lastUserInfo = JSON.parse(localStorage.getItem(LAST_USER));
    const expiry = lastUserInfo?.expiry ?? null;
    const userName = lastUserInfo?.lastUser ?? null;
    const urlParams = new URLSearchParams(window.location.search);

    if (urlParams.has('auth') || urlParams.get('auth') == 'failed') {
        logout();
        console.log("SessionStorage bereinigt.");
    }

    if (!expiry || expiry < new Date().getTime())
        deleteUserStorage(span);
    else {
        span.textContent = lastUserInfo.lastUser;
        setTimer(expiry);
    }
}


function deleteUserStorage(span) {
    localStorage.removeItem(LAST_USER);
    sessionStorage.removeItem(TOKEN_NAME);
    span.textContent = 'Kein Benutzer';
    span.style.color = 'grey';
    if (timer) {
        clearTimer();
    }
}

function setTimer(expiry) {
    if (!expiry) return;
    let timeout = document.getElementById('userTimeout');
    timer = window.setInterval(function () {
        let time = new Date(expiry - new Date().getTime());
        timeout.innerHTML = time.getMinutes() + ":" + ('0' + time.getSeconds()).slice(-2);
        if (time < 1000)
            clearTimer();
    }, 1000);
}
  
function clearTimer() {
    clearInterval(timer);   
    timer = null;
    document.getElementById('userTimeout').innerHTML = '00:00';
    checkLoginStatus();
}

function getUserDataFromTableRow(row)
{
    const username = row.children[0].children[0].value;
    const userrole = row.children[1].children[0].value;
    const userid = row.children[2].children[0].value;

    document.getElementById('userid').value = userid;
    document.getElementById('username').value = username;
    document.getElementById('role').value = userrole;
}

async function updateUser(verb)
{
    const userid = document.getElementById('userid').value
    const username = document.getElementById('username').value;
    const userrole = document.getElementById('role').value;
    const userpwd = document.getElementById('pwd').value;

    try
    {        
        const res = await fetchSecure('/user/' + verb, {
          method: 'POST',
          headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
          body: new URLSearchParams({ id: userid, name: username, role: userrole, pwd: userpwd })
        });

        if (res.ok) {
            location.reload();
        } else {
            alert('Benuterverwaltung - Nicht erlaubte Operation - Status ' + res.status);
        }
    } catch (error) {
        console.error(error.message);
    }
}

export { TOKEN_NAME, login, logout, checkLoginStatus, getUserDataFromTableRow, updateUser };

