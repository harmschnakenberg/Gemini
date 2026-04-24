export default function openTab(evt, tabName) {
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
