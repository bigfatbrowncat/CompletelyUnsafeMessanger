(function () {

    var socket = null;

    var open_socket = function () {
        if (socket !== null) {
            console.warn("Socket is already connected");
            return;
        }

        var webSocketProtocol = location.protocol == "https:" ? "wss:" : "ws:";
        var webSocketURI = webSocketProtocol + "//" + location.host + "";

        socket = new WebSocket(webSocketURI);
        socket.binaryType = 'arraybuffer';

        socket.onopen = function () {
            console.log("Connected");

            var event = new CustomEvent("protocol.connected", { detail: { } });
            document.dispatchEvent(event);
        };

        socket.onclose = function (event) {
            if (event.wasClean) {
                console.log('Disconnected');
            } else {
                console.log('Connection lost'); // for example if server processes is killed
            }
            console.log('Code: ' + event.code + '. Reason: ' + event.reason);

            var event = new CustomEvent("protocol.disconnected", {
                detail: {
                    wasClean: event.wasClean,
                    code: event.code,
                    reason: event.reason
                }
            });
            document.dispatchEvent(event);

            // Removing the socket object
            socket = null;
        };

        socket.onmessage = function (event) {
            if (event.data instanceof ArrayBuffer) {
                // Disabled by the current protocol (sending images
                // from server to client thru WebSocket is ineffective)
                console.log("Sending binary from server to client is unsupported in this protocol");
            }
            else {
                var command = JSON.parse(event.data);
                if (command.type == "update_card") {
                    var evt = new CustomEvent("protocol.message.update_card", {
                        detail: command
                    });
                    document.dispatchEvent(evt);
                } else if (command.type == "list_card_ids") {
                    var evt = new CustomEvent("protocol.message.list_card_ids", {
                        detail: command
                    });
                    document.dispatchEvent(evt);
                }
            }

            console.log("Data received: " + event.data);
        };

        socket.onerror = function (error) {
            console.log("Error: " + error.message);
            socket.close();
        };
    }

    var send_list_card_ids_request = function () {
        var sendListIdsRequestCommand = {
            type: "list_card_ids",
        };
        socket.send(JSON.stringify(sendListIdsRequestCommand));
    };

    var send_update_text_card_command = function (id, text) {
        var updateTextCardCommand = {
            type: "update_card",
            id: id,
            value: {
                type: "text",
                text: text
            }
        };
        socket.send(JSON.stringify(updateTextCardCommand));
    };

    var protocol = {
        openSocket: open_socket,
        sendListCardIdsRequest: send_list_card_ids_request,
        sendUpdateTextCardCommand: send_update_text_card_command
    };

    Document.prototype.protocol = protocol;
})();

// Global scope object
this.protocol = Document.prototype.protocol;
