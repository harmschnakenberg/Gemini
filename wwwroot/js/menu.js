async function loadMenu(path) {
    let file = await fetch(path);
    let text = await file.text();
    console.info(`Menü JSON: ${text}`);
    const json = JSON.parse(text);
    

    const nav = document.createElement("ul");
    nav.id = "sidemenu";
    nav.style.position = "fixed";
    nav.style.top = "-95vh";
    nav.style.left = "-29vw";
    nav.style.padding = "1vh";
    nav.style.zIndex = "1000";
    nav.style.width = "30vw";
    nav.style.height = "95vh";
    nav.style.backgroundColor = "rgb(70, 70, 70)"
    nav.style.opacity = "0.98";
    //nav.style.overflowY = "auto";
    nav.style.listStyleType = "none";
    nav.style.transition = "transform 0.1s"
    nav.addEventListener("mouseover",
        function () {
            const obj = document.getElementById('sidemenu');
            obj.style.transform = "translate(29vw, 93vh)";
            obj.style.backgroundColor = "grey";
        });
    nav.addEventListener("mouseout",
        function () {
            const obj = document.getElementById('sidemenu');
            obj.style.transform = "translate(0vw, 0vh)";
            obj.style.backgroundColor = "rgb(70, 70, 70)"
        });

    const logo = "<svg><style> svg { width:2vw; height:3vh; background-color: #ddd; position:absolute; right:2px; bottom:2px; margin:2px;}</style>" +
                 "<line x1='0' y1='0' x2='0' y2='35' style='stroke:darkcyan;stroke-width:2'></line>" +
                 "<polygon points='10,0 10,15 25,0' style='fill:#00004d;'></polygon>"+
                 "<polygon points='10,20 10,35 25,35' style='fill:#00004d;'></polygon>"+
                 "<polygon points='20,17 37,0 37,35' style='fill:darkcyan;'></polygon>" +
        "</svg>"

    nav.innerHTML = logo;

    document.body.insertBefore(nav, document.body.children[0]);

    for (var item of json.Sollwerte) {
        const li = document.createElement("li");
        const a = document.createElement("a");
        a.setAttribute("href", `/html/soll/${item.link}`);
        a.style.width = "80%";
        a.innerHTML = item.name;
     
        document.getElementById("sidemenu").appendChild(li).appendChild(a);
    }

    //json.forEach(draw); 
}

//function draw(item, index) {
//    const li = document.createElement("li");
//    li.innerHTML = `<a href="${item.url}">${item.name}</a>`;

//    document.getElementById("sideMenu").appendChild(li);

//}