import fetchSecure from '../module/fetch.js';
import * as alert from '../module/alert.js';

export default async function plcUpdate(verb, obj) {
    const plcName = obj.parentNode.parentNode.children[0].children[0].value;
    const plcType = obj.parentNode.parentNode.children[1].children[0].value;
    const plcIp = obj.parentNode.parentNode.children[2].children[0].value;
    const plcRack = obj.parentNode.parentNode.children[3].children[0].value;
    const plcSlot = obj.parentNode.parentNode.children[4].children[0].value;
    const plcIsActive = obj.parentNode.parentNode.children[5].children[0].checked;
    const plcComm = obj.parentNode.parentNode.children[6].children[0].value;
    const plcId = obj.parentNode.parentNode.children[7].children[0].value;

    //document.getElementsByTagName('h1')[0].innerHTML = `Id ${plcId}, ${plcName}, ${plcType}, ${plcIp}, ${plcRack}, ${plcSlot}, ${plcIsActive}, ${plcComm}|`;
    try {
        const alert = import('../module/alert.js');
                               
        const res = await fetchSecure('/source/' + verb, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({
                plcId: plcId,
                plcName: plcName,
                plcType: plcType,
                plcIp: plcIp,
                plcRack: plcRack,
                plcSlot: plcSlot,
                plcIsActive: plcIsActive,
                plcComm: plcComm
            })
        });

        if (!res.ok)
            (await alert).error('Datenquellenverwaltung - Nicht erlaubte Operation - Status ' + res.status);
        else {
            const data = await res.json();

            console.log(data);
            if (data.Type == 'reload') {
                (await alert).success(`Operation ${verb} erfolgreich. ${data.Text}`);
                setTimeout(location.reload(), 5000);
            }
            else
                (await alert).message(data.Type, data.Text);
        }

    } catch (error) {
        console.error('Fehler beim Abrufen: ', error);
    }

}