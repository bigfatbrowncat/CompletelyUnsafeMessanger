(function () {
    // Using these tags is safe for the text inside a card
    var safeTags = [
        "B", "I", "U", "S", "EM", "STRONG", "P",
        "SPAN", "DIV",
        "H1", "H2", "H3", "H4", "H5", "H6"
    ];

    // Make th HTML safe for inserting to a card.
    // This function removes every tag except the ones listed in 'safeTags'
    function safifyHTML(str) {
        var parser = new DOMParser();
        var htmlDoc = parser.parseFromString(str, 'text/html');
        var done;
        do {
            var all = htmlDoc.body.getElementsByTagName("*");

            done = true;
            for (var i = 0, max = all.length; i < max; i++) {
                var node = all[i];
                console.log("node: " + node.tagName);

                if (safeTags.indexOf(node.tagName.toUpperCase()) === -1) {
                    //var newDiv = document.createElement('div');
                    //newDiv.innerHTML = node.innerHTML;
                    //node.replaceWith(newDiv);
                    node.outerHTML = node.innerHTML;
                    done = false;
                    break;
                }
            }
        } while (!done);

        return htmlDoc.body.innerHTML;
    }

    var webSocketProtocol = location.protocol == "https:" ? "wss:" : "ws:";
    var webSocketURI = webSocketProtocol + "//" + location.host + "";

    socket = new WebSocket(webSocketURI);
    socket.binaryType = 'arraybuffer';

    socket.onopen = function () {
        console.log("Connected");
    };

    socket.onclose = function (event) {
        if (event.wasClean) {
            console.log('Disconnected');
        } else {
            console.log('Connection lost'); // for example if server processes is killed
        }
        console.log('Code: ' + event.code + '. Reason: ' + event.reason);
    };

    socket.onmessage = function (event) {
        var cardsList = document.getElementById("cards");
        var node = document.createElement("li");
        node.classList.add("fadein");

        if (event.data instanceof ArrayBuffer)
        {
            // Disabled by the current protocol (sending images
            // from server to client thru WebSocket is ineffective)
            console.log("Sending binary from server to client is unsupported in this protocol");
        }
        else
        {
            var command = JSON.parse(event.data);
            if (command.type == "add_card") {
                if (command.card.type == "text") {
                    // Text card
                    node.classList.add("text");
                    //var textnode = document.createTextNode(command.card.text);
                    //node.appendChild(textnode);
                    var innerElement = document.createElement("div");
                    innerElement.innerHTML = safifyHTML(command.card.text);
                    node.appendChild(innerElement);
                }
                else if (command.card.type == "image") {
                    // File name
                    node.classList.add("image");
                    var filenameDiv = document.createElement("div");
                    filenameDiv.className = "filename";
                    var textnode = document.createTextNode(command.card.filename);
                    filenameDiv.appendChild(textnode);
                    node.appendChild(filenameDiv);

                    // Image
                    var image = document.createElement("img");
                    image.style.maxWidth = 600;
                    image.style.maxHeight = 600;
                    image.src = command.card.link;
                    node.appendChild(image);
                }
            }
        }
        cardsList.appendChild(node);

        console.log("Data received: " + event.data);
    };

    socket.onerror = function (error) {
        console.log("Error: " + error.message);
    };

    var textForm = document.getElementById('textForm');
    var message = document.getElementById('message');

    var imageForm = document.getElementById('imageForm');
    var filesElement = document.getElementById('filename');
    var preview = document.getElementById('preview');
    var browseButton = document.getElementById('browse');

    textForm.onsubmit = function () {
        if (message.value != '') {
            var addTextCardCommand = {
                type: "add_card",
                card: {
                    type: "text",
                    text: message.value
                }
            };
            socket.send(JSON.stringify(addTextCardCommand));
            message.value = '';
        }
        return false;   // Don't submit the form in the regular way
    };

    imageForm.onsubmit = function () {
        var files = filesElement.files;
        if (files.length > 0) {
            for (var index = 0; index < files.length; index++) {
                var file = files[index];
                var reader = new FileReader();
                var rawData = new ArrayBuffer();
                reader.loadend = function () {
                }
                reader.onload = function (e) {
                    // Sending the upload_image_card command
                    var uploadImageCardCommand = {
                        type: "upload_image_card",
                        card: {
                            type: "image",
                            filename: file.name,
                            size: file.size,
                            mimeType: file.type
                        }
                    };
                    socket.send(JSON.stringify(uploadImageCardCommand));

                    // Sending the image data
                    rawData = e.target.result;
                    socket.send(rawData);

                    // Clearing the form
                    imageForm.reset();
                    preview.src = "";
                }
                if (file instanceof Blob) {
                    reader.readAsArrayBuffer(file);
                }
            }
        }
        return false;   // Don't submit the form in the regular way
    };

    var browseClick = function (event) {
        filesElement.click();
    }
    browseButton.onclick = browseClick;

    var fileChanged = function (event) {
        if (event.target.files.length > 0) {
            preview.src = URL.createObjectURL(event.target.files[0]);
        } else {
            preview.src = "";
        }
        preview.onload = function () {
            URL.revokeObjectURL(preview.src) // free memory
        }
    };
    filesElement.onchange = fileChanged;

})();