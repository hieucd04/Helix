﻿const {remote, ipcRenderer} = require("electron");
const socket = new require("net").Socket();

const btnMain = document.getElementById("btn-main");
const txtStartUri = document.getElementById("txt-start-uri");
const txtDomainName = document.getElementById("txt-domain-name");
const txtHtmlRendererCount = document.getElementById("txt-html-renderer-count");
const ckbVerifyExternalUrls = document.getElementById("ckb-verify-external-urls");
const ckbShowWebBrowsers = document.getElementById("ckb-show-web-browsers");
const configurationPanel = document.getElementById("configuration-panel");
const shutdownOverlay = document.getElementById("shutdown-overlay");
const shutdownOverlaySubtitle = document.getElementById("shutdown-overlay-subtitle");
const shutdownFailureOverlay = document.getElementById("shutdown-failure-overlay");

const lblVerified = document.getElementById("lbl-verified");
const lblValid = document.getElementById("lbl-valid");
const lblBroken = document.getElementById("lbl-broken");
const lblRemaining = document.getElementById("lbl-remaining");
const lblAveragePageLoadTime = document.getElementById("lbl-average-page-load-time");
const lblAveragePageLoadTimeUnitOfMeasure = document.getElementById("lbl-average-page-load-time-unit-of-measure");
const lblElapsedTime = document.getElementById("lbl-elapsed-time");
const lblStatusText = document.getElementById("lbl-status-text");
const lblShutdownOverlayMessage = document.getElementById("shutdown-overlay-message");
const btnStop = document.getElementById("btn-stop");

socket.connect(18880, "127.0.0.1", () => {

    btnMain.addEventListener("click", () => {
        if (!btnMain.hasAttribute("disabled")) btnMain.setAttribute("disabled", "");
        if (!configurationPanel.hasAttribute("disabled")) configurationPanel.setAttribute("disabled", "");
        redraw({
            VerifiedUrlCount: 0,
            ValidUrlCount: 0,
            BrokenUrlCount: 0,
            RemainingWorkload: 0,
            MillisecondsAveragePageLoadTime: 0,
            ElapsedTime: "00 : 00 : 00"
        });
        socket.write(JSON.stringify({
            text: "btn-start-clicked",
            payload: JSON.stringify({
                StartUri: txtStartUri.value,
                DomainName: txtDomainName.value,
                HtmlRendererCount: txtHtmlRendererCount.value,
                VerifyExternalUrls: ckbVerifyExternalUrls.checked,
                UseHeadlessWebBrowsers: !ckbShowWebBrowsers.checked
            })
        }));
    });

    document.getElementById("btn-close").addEventListener("click", () => {
        let waitingTime = 120;
        const getShutdownOverlaySubTitle = (remainingTime) => `(Please allow up to <div style='display:inline-block;color:#FF6347;'>${remainingTime}</div> seconds)`;
        shutdownOverlaySubtitle.innerHTML = getShutdownOverlaySubTitle(waitingTime);
        lblShutdownOverlayMessage.textContent = "Initializing shutdown sequence ...";
        shutdownOverlay.style.display = "block";

        const shutdownCountdown = setInterval(() => {
            waitingTime--;
            shutdownOverlaySubtitle.innerHTML = getShutdownOverlaySubTitle(waitingTime);
            if (waitingTime === 0) {
                shutdownFailureOverlay.style.display = "block";
                shutdownOverlay.style.display = "none";
                clearInterval(shutdownCountdown);
            }
        }, 1000);

        socket.end(JSON.stringify({text: "btn-close-clicked"}));
        socket.on("end", () => { ipcRenderer.send("btn-close-clicked"); });
    });

    document.getElementById("btn-minimize").addEventListener("click", () => { remote.BrowserWindow.getFocusedWindow().minimize(); });

    socket.on("data", ipcMessageJson => {
        const ipcMessage = JSON.parse(ipcMessageJson);
        switch (ipcMessage.Text) {
            case "redraw":
                redraw(JSON.parse(ipcMessage.Payload));
                break;
        }
    });

});

function isNumeric(number) { return !isNaN(number) && typeof (number) === "number"; }

function redraw(frame) {
    if (isNumeric(frame.VerifiedUrlCount)) lblVerified.textContent = frame.VerifiedUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (isNumeric(frame.ValidUrlCount)) lblValid.textContent = frame.ValidUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (isNumeric(frame.BrokenUrlCount)) lblBroken.textContent = frame.BrokenUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (isNumeric(frame.RemainingWorkload)) lblRemaining.textContent = frame.RemainingWorkload.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (isNumeric(frame.MillisecondsAveragePageLoadTime)) {
        lblAveragePageLoadTime.textContent = frame.MillisecondsAveragePageLoadTime.toLocaleString("en-US", {maximumFractionDigits: 0});
        lblAveragePageLoadTimeUnitOfMeasure.style.visibility = "visible";
    }
    if (frame.ElapsedTime) lblElapsedTime.textContent = frame.ElapsedTime;
    if (frame.StatusText) shutdownOverlay.style.display === "block"
        ? lblShutdownOverlayMessage.textContent = frame.StatusText
        : lblStatusText.textContent = frame.StatusText;
    if (frame.RestrictHumanInteraction === true) {
        if (!btnMain.hasAttribute("disabled")) btnMain.setAttribute("disabled", "");
        if (!configurationPanel.hasAttribute("disabled")) configurationPanel.setAttribute("disabled", "");
    } else if (frame.RestrictHumanInteraction === false) {
        if (btnMain.hasAttribute("disabled")) btnMain.removeAttribute("disabled");
        if (configurationPanel.hasAttribute("disabled")) configurationPanel.removeAttribute("disabled");
    }

    const btnMainIsStartButton = btnMain.firstElementChild.className === "controls__play-icon";
    const btnMainIsPauseButton = btnMain.firstElementChild.className === "controls__pause-icon";
    switch (frame.CrawlerState) {
        case "Ready":
            if (btnMainIsStartButton) break;
            btnMain.firstElementChild.className = "controls__play-icon";
            if (btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.remove("controls__main-button--amber");
            if (btnMain.hasAttribute("disabled")) btnMain.removeAttribute("disabled");
            if (configurationPanel.hasAttribute("disabled")) configurationPanel.removeAttribute("disabled");
            // if (!btnStop.hasAttribute("disabled")) btnStop.setAttribute("disabled", "");
            break;
        case "Working":
            if (btnMainIsPauseButton) break;
            btnMain.firstElementChild.className = "controls__pause-icon";
            if (!btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.add("controls__main-button--amber");
            if (btnMain.hasAttribute("disabled")) btnMain.removeAttribute("disabled");
            if (!configurationPanel.hasAttribute("disabled")) configurationPanel.setAttribute("disabled", "");
        // if (btnStop.hasAttribute("disabled")) btnStop.removeAttribute("disabled");
    }
}