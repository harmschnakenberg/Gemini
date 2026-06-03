/// <reference path="../module/export.js" />

/* Module laden */
import loadSiteMenu from '../module/sitemenu.js';
import plcUpdate from '../module/plc.js';
import fetchSecure from '../module/fetch.js';
import openTab from '../module/tab.js';
import createModal from '../module/modal.js';
import * as user from '../module/user.js';
import * as data from '../module/data.js';
import * as alert from '../module/alert.js';
import * as exp from '../module/export.js';
import * as dragdrop from '../module/dragdrop.js';
import * as chart from '../js/chart.js';
import * as svg from '../module/svg.js';

/* Module in HTML bereitstellen */
window.user = user;
window.data = data;
window.exp = exp;
window.chart = chart;
window.dragdrop = dragdrop;
window.plcUpdate = plcUpdate;
window.openTab = openTab;
window.svg = svg;
window.openTab = openTab;
window.createModal = createModal;

/* Initiale Aufrufe */
// in Production Kommentar entfernen // window.addEventListener("contextmenu", (e) => { e.preventDefault() });
user.checkLoginStatus();
let isSvg = await svg.initSvg();
data.initUnits();
data.initWebsocket(data.initTags());

if (isSvg)
    loadSiteMenu('soll', '/html/soll/menu.json');
else
    loadSiteMenu('bild', '/html/bild/menu.json');

document.addEventListener('DOMContentLoaded', (event) => {
    // Initialisierung der Drag-and-Drop-Funktionalität für vorhandene Elemente
    dragdrop.setupDragAndDrop();
});
