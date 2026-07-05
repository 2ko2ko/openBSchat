const chat = document.getElementById("chat");

const source = new EventSource("/events");

source.addEventListener("delete", e => {
    const id = JSON.parse(e.data);

    document
        .querySelector(`[data-id="${id}"]`)
        ?.remove();
});

function enforceMessageCutoff() {
    while (chat.children.length > settings.messageCutoff) {
        chat.removeChild(chat.firstElementChild);
    }
}

function scheduleFade(element) {
    if (!settings.messageFade)
        return;

    setTimeout(() => {
        if (!element.isConnected)
            return;

        element.style.transition = `opacity ${settings.messageFadeAnimTime}s linear`;
        element.classList.add("fade");

        setTimeout(() => {
            element.remove();
        }, settings.messageFadeAnimTime * 1000);

    }, settings.messageFadeTime * 1000);
}

source.addEventListener("message", e => {
    const msg = JSON.parse(e.data);

    chat.insertAdjacentHTML("beforeend", msg.Message);

    const element = chat.lastElementChild;

    element.dataset.timestamp = msg.MessageTime;

    enforceMessageCutoff();
    scheduleFade(element);

    // IF !CustomChatAnim && ChatAnim true
    //     Chat animation
});