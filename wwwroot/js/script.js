/* Module laden */
import loadSiteMenu from '../module/sitemenu.js';
import plcUpdate from '../module/plc.js';
import fetchSecure from '../module/fetch.js';
import * as user from '../module/user.js';
import * as data from '../module/data.js';
import * as alert from '../module/alert.js';
import * as exp from '../module/export.js';
import * as dragdrop from '../module/dragdrop.js';
import * as chart from '../js/chart.js';

function openTab(evt, tabName) {
    var i, tabcontent, tablinks, isActive;
    isActive = evt.currentTarget.classList.contains("active");
    tabcontent = document.getElementsByClassName("tabcontent");
    for (i = 0; i < tabcontent.length; i++) {
        tabcontent[i].style.display = "none";
    }
    tablinks = document.getElementsByClassName("tablinks");
    for (i = 0; i < tablinks.length; i++) {
        tablinks[i].className = tablinks[i].className.replace(" active", "");
    }
    if (isActive) return;
    document.getElementById(tabName).style.display = "block";
    evt.currentTarget.className += " active";
}

/* Module in HTML bereitstellen */
window.user = user;
window.data = data;
window.exp = exp;
window.chart = chart;
window.dragdrop = dragdrop;
window.plcUpdate = plcUpdate;
window.openTab = openTab;

/* Initiale Aufrufe */
user.checkLoginStatus();
data.initUnits();
data.initWebsocket(data.initTags());
loadSiteMenu('soll', '/html/soll/sollmenu.json');

document.addEventListener('DOMContentLoaded', (event) => {
    // Initialisierung der Drag-and-Drop-Funktionalität für vorhandene Elemente
    dragdrop.setupDragAndDrop();
});
