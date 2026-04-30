
/*
 * @param {any} btnObj           // btnObj: button that opens the modal
 * @param {any} link             // Link zum Sollwertfenster
 */

export default function openModal(btnObj, link) {
    // ToDo: Modal dynamisch im DOM erzeugen, aber Werte erst bai Aufruf vom Server abrufen

    // Get the modal
    var modal = document.getElementById("myModal");

    // Get the <span> element that closes the modal
    var span = modal.getElementsByClassName("close")[0];

    // When the user clicks the btnObj, open the modal
    btnObj.onclick = function () {
        modal.style.display = "block";
    }

    // When the user clicks on <span> (x), close the modal
    span.onclick = function () {
        modal.style.display = "none";
    }

    // When the user clicks anywhere outside of the modal, close it
    window.onclick = function (event) {
        if (event.target == modal) {
            modal.style.display = "none";
        }
    }

}