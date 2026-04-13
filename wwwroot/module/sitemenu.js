
export default async function loadSiteMenu(endpoint, path) {
    const logo = "<svg id='logo'><style> #logo { width:35px; height:35px; background-color: #ddd; position:absolute; right:2px; bottom:2px; margin:2px;}</style>" +
        "<line x1='0' y1='0' x2='0' y2='35' style='stroke:darkcyan;stroke-width:2'></line>" +
        "<polygon points='10,0 10,15 25,0' style='fill:#00004d;'></polygon>" +
        "<polygon points='10,20 10,35 25,35' style='fill:#00004d;'></polygon>" +
        "<polygon points='20,17 37,0 37,35' style='fill:darkcyan;'></polygon>" +
        "</svg>"

    const nav = document.createElement("ul");
    nav.id = "sidemenu";
    nav.innerHTML = logo;
    document.body.insertBefore(nav, document.body.children[0]);

    let file = await fetch(path);
    let text = await file.text();
    //console.info(`Menü JSON: ${text}`);
    const json = JSON.parse(text);

    const li = document.createElement("li");
    const a = createLink('/', 'Hauptmenü');
    document.getElementById("sidemenu").appendChild(li).appendChild(a);

    //console.info(`Menü JSON: ${json.Sollwerte}`);
    for (var item of json.Sollwerte) {
        //console.info(`${item}, ${item.Id}`)
        const li = document.createElement("li");
        const a = createLink(`/${endpoint}/${item.Id}`, item.Name)           
        document.getElementById("sidemenu").appendChild(li).appendChild(a);
    }
}

function createLink(href, display) {
    const a = document.createElement("a");
    a.setAttribute("href", href)
    a.classList.add("menuitem");
    a.innerHTML = display;
    return a;
}
