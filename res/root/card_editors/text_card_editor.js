(function () {
    // Checking dependencies
    if (nanowiki === undefined) {
        console.error("nanowiki not found in the global NS. Include nanowiki.js before text_card_editor.js");
    }
    if (protocol === undefined) {
        console.error("protocol not found in the global NS. Include protocol.js before text_card_editor.js");
    }

    // *** Globals ***

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
                    node.outerHTML = node.innerHTML;
                    done = false;
                    break;
                }
            }
        } while (!done);

        return htmlDoc.body.innerHTML;
    }


    // *** Page-globals ***

    //var webSocketProtocol = location.protocol == "https:" ? "wss:" : "ws:";
    //var webSocketURI = webSocketProtocol + "//" + location.host + "";

    var remarkupAll = function () {
        // Updating all the pages markup
        var pages = document.body.querySelectorAll("page");
        for (var i = 0; i < pages.length; i++) {
            var content = pages[i].querySelector("content");
            var content_source = pages[i].querySelector("content-source");
            content.innerHTML = document.nanowiki.convert(content_source.innerHTML);
        }
    }


    // *** Locals ***

    Document.prototype.addTextCardEditor = function (newPageId) {
        var newPage = document.getElementById(newPageId);
        var editor = newPage.querySelector("editor");
        var textForm = editor.querySelector("form");

        var edit_menu = newPage.querySelector("edit-menu");
        var edit_button = edit_menu.querySelector('[name="edit"]');
        var cancel_button = newPage.querySelector('[name="cancel"]');
        var submit_button = newPage.querySelector('input[type="submit"]');

        var idTag = textForm.querySelector('[name="id"]');
        var message = textForm.querySelector('[name="message"]');

        // Creating CodeMirror instance for the "message" field
        var editorCodeMirror = null; 
        var codeMirrorFromTextArea = function () {
            editorCodeMirror = CodeMirror.fromTextArea(message, {
                mode: 'markdown',
                lineNumbers: true,
                theme: "default",
                extraKeys: { "Enter": "newlineAndIndentContinueMarkdownList" }
            });
        };
        var codeMirrorToTextArea = function () {
            if (editorCodeMirror != null) {
                editorCodeMirror.toTextArea();
                editorCodeMirror = null;
            }
        };

        // Hide the editor in the beginning
        editor.style.display = "none";

        // Setting up the "Edit" button

        edit_button.addEventListener("click", function () {
            editor.style.display = "block";
            editorCodeMirror.refresh();
        });
        cancel_button.addEventListener("click", function () {
            editor.style.display = "none";

            // Resetting the edited text
            codeMirrorToTextArea();
            message.value = content_source.innerHTML;
            codeMirrorFromTextArea();
        });

        idTag.setAttribute("value", newPageId);

        var content = newPage.querySelector("content");
        var content_source = newPage.querySelector("content-source");
        var remarkup = function () {
            // Updating the markup
            content.innerHTML = document.nanowiki.convert(content_source.innerHTML);
        }

        // Creating a websocket connection for the editor
        //var socket = new WebSocket(webSocketURI);
        //socket.binaryType = 'arraybuffer';

        /*var sendListIdsRequest = function () {
            var sendListIdsRequestCommand = {
                type: "list_card_ids",
            };
            socket.send(JSON.stringify(sendListIdsRequestCommand));
        }*/

        /*socket.onopen = function () {
            console.log("Connected");
            sendListIdsRequest();
        };*/

        // Request IDs list
        protocol.sendListCardIdsRequest();

        // Initializing the editor fir the case that the page doesn't exist
        codeMirrorFromTextArea();

        document.addEventListener("protocol.connected", function () {
            // Request IDs list on reconnect
            protocol.sendListCardIdsRequest();
            submit_button.disabled = false;
        });

        document.addEventListener("protocol.disconnected", function () {
            submit_button.disabled = true;
        });

        /*socket.onclose = function (event) {
            if (event.wasClean) {
                console.log('Disconnected');
            } else {
                console.log('Connection lost'); // for example if server processes is killed
            }
            console.log('Code: ' + event.code + '. Reason: ' + event.reason);
        };*/

        document.addEventListener("protocol.message.update_card", function (evt) {
            var command = evt.detail;

            if (command.id == newPageId) {

                codeMirrorToTextArea();
                message.value = command.value.text;
                codeMirrorFromTextArea();

                // Updating the page
                content_source.innerHTML = command.value.text;

                remarkup();
            }
        });

        document.addEventListener("protocol.message.list_card_ids", function (evt) {
            var command = evt.detail;

            // Updating links based on the currently loaded pages
            document.nanowiki.update_links(command.ids);
            remarkupAll();
        });

        /*socket.onmessage = function (event) {
            if (event.data instanceof ArrayBuffer) {
                // Disabled by the current protocol (sending images
                // from server to client thru WebSocket is ineffective)
                console.log("Sending binary from server to client is unsupported in this protocol");
            }
            else {
                var command = JSON.parse(event.data);
                if (command.type == "update_card") {
                    if (command.id == newPageId) {

                        codeMirrorToTextArea();
                        message.value = command.value.text;
                        codeMirrorFromTextArea();

                        // Updating the page
                        content_source.innerHTML = command.value.text;

                        remarkup();
                    }
                } else if (command.type == "list_card_ids") {
                    // Updating links based on the currently loaded pages
                    document.nanowiki.update_links(command.ids);
                    remarkupAll();
                }
            }

            console.log("Data received: " + event.data);
        };

        socket.onerror = function (error) {
            console.log("Error: " + error.message);
            socket.close();
        };*/

        // "Confirm" button
        textForm.onsubmit = function () {
            codeMirrorToTextArea();
            editor.style.display = "none";

           /* var updateTextCardCommand = {
                type: "update_card",
                id: idTag.value,
                value: {
                    type: "text",
                    text: message.value
                }
            };
            socket.send(JSON.stringify(updateTextCardCommand));*/

            protocol.sendUpdateTextCardCommand(idTag.value, message.value);

            return false;   // Don't submit the form in the regular way
        };
    }

})();