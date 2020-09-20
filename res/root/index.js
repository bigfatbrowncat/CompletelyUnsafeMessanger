(function () {
    // public method for encoding an Uint8Array to base64
    function encodeToBase64(input) {
        var keyStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
        var output = "";
        var chr1, chr2, chr3, enc1, enc2, enc3, enc4;
        var i = 0;

        while (i < input.length) {
            chr1 = input[i++];
            chr2 = i < input.length ? input[i++] : Number.NaN; // Not sure if the index 
            chr3 = i < input.length ? input[i++] : Number.NaN; // checks are needed here

            enc1 = chr1 >> 2;
            enc2 = ((chr1 & 3) << 4) | (chr2 >> 4);
            enc3 = ((chr2 & 15) << 2) | (chr3 >> 6);
            enc4 = chr3 & 63;

            if (isNaN(chr2)) {
                enc3 = enc4 = 64;
            } else if (isNaN(chr3)) {
                enc4 = 64;
            }
            output += keyStr.charAt(enc1) + keyStr.charAt(enc2) +
                keyStr.charAt(enc3) + keyStr.charAt(enc4);
        }
        return output;
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
        var messagesList = document.getElementById("messages");
        var node = document.createElement("li");

        if (event.data instanceof ArrayBuffer)
        {
            var arrayBuffer = event.data;
            var bytes = new Uint8Array(arrayBuffer);

            var image = document.createElement("img");
            image.style.maxWidth = 600;
            image.style.maxHeight = 600;
            image.src = 'data:image/png;base64,' + encodeToBase64(bytes);
            node.appendChild(image);
        }
        else
        {
            var textnode = document.createTextNode(event.data);
            node.appendChild(textnode);
        }
        messagesList.appendChild(node);

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
            socket.send(message.value);
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
                    rawData = e.target.result;
                    socket.send(rawData);
                    console.log("file has been transferred.")
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