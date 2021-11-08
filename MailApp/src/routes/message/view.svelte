<script context="module" lang="ts">
    export const prerender = true;
    import type { Message } from "$lib/types";

    export async function load() {
        let inboxItem = JSON.parse(localStorage.inboxItem);
        console.log(inboxItem);
        const message = await getMessage(inboxItem);
        return { props: { message, inboxItem } };
    }

    async function markMessageAsSeen(inboxItem: InboxItem) {
        if (inboxItem.contentType == "weaveMail") {
        } else if (inboxItem.isSeen == false) {
            var flags = {
                isSeen: true,
                isRecent: inboxItem.isRecent,
                isFlagged: inboxItem.isFlagged,
            };

            var url = `http://localhost:5000/Mail/message/${inboxItem.id}/flags`;
            const res = await fetch(url, {
                method: "POST", // *GET, POST, PUT, DELETE, etc.
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify(flags),
            });
            // const res = await fetch('inbox.json');
            const result = await res.text();
            console.log(`marked as seen ${result}`);
        }
    }

    async function getMessage(inboxItem: InboxItem): Promise<Message> {
        if (inboxItem.contentType == "weavemail") {
            let msg: Message = {
                id: inboxItem.id,
                body: inboxItem.body,
                fromAddress: inboxItem.fromAddress != inboxItem.from ? inboxItem.fromAddress : "",
                fromName: inboxItem.from,
                subject: inboxItem.subject,
                toAddress: "",
                toName: "",
                fee: 0,
                amount: 0,
                txid: "",
                timestamp: inboxItem.timestamp,
                appVersion: "",
            };
            return msg;
        } else {
            const res = await fetch(
                `http://localhost:5000/Mail/message/${inboxItem.id}`,
                { mode: "cors" }
            );
            const text = await res.text();
            let msg: Message = JSON.parse(text);
            markMessageAsSeen(inboxItem);
            return msg;
        }
    }
</script>

<script lang="ts">
    import { onMount } from "svelte";
    import { getFormattedTime } from "$lib/formattedTime";
    import type { InboxItem } from "$lib/types";
    export let message: Message;
    export let inboxItem: InboxItem;

    let isPermanent = false;
    let promise = Promise.resolve();

    function saveToArweave() {
        isPermanent = true;
        let milliseconds = 2000;
        promise = new Promise(resolve => setTimeout(resolve, milliseconds));
    }

    function syncHeight() {
        // this resize wont work when the static pages are served from a different
        // origin than the webservice -DMac
        try {
            this.style.height = `${this.contentWindow.document.body.offsetHeight}px`;
        } catch {
            console.log(`StyleHeight:${this.style.height}`);
            this.style.height = "2690px";
        }
    }

    onMount(async () => {
        console.log(`contentType:${inboxItem.contentType}`);
        const contentDiv = document.querySelector(".body");
        if (inboxItem.contentType == "multipart/alternative") {
            // const frame = document.querySelector('iframe')
            // frame.addEventListener('load', syncHeight)
            // frame.src = `data:text/html;charset=utf-8,${escape(message.body)}`;
            contentDiv.innerHTML = unescape(message.body);
        } else if (inboxItem.contentType == "weavemail") {
            contentDiv.innerHTML = message.body;
        } else {
            contentDiv.innerHTML = `<pre>${unescape(message.body)}</pre>`;
        }
        markMessageAsSeen(inboxItem);
    });
</script>

<section>
    <div class="title">{inboxItem.subject}</div>
    <div class="container">
        <!-- <div class="optionsContainer">
            <div class="optionsBar">
                {#if isPermanent == false}
                    <span class="optionButton interactive" on:click={saveToArweave}>Not Saved Permanently</span>
                {:else}
                    {#await promise}
                        <span class="savingButton active"><div class="loader"></div>Saving Permanently...</span>
                    {:then}
                        <span class="optionButton active">Saved Permanently</span>
                    {/await}

                {/if}
            </div>
        </div> -->
        <div class="header">
            <div class="left">
                <img
                    src="../img_avatar.png"
                    alt="ProfileImage"
                    class="avatar"
                />
            </div>
            <div class="center">
                <span class="from"> {message.fromName}
                    <span class="to"> {message.fromAddress} </span>
                </span>
                <div class="to">to {inboxItem.to}</div>
            </div>
            <div class="right">
                {getFormattedTime(inboxItem.timestamp)}
            </div>
        </div>
        <div class="content">
            <div class="body">
                <iframe id="iframe1" title="Message Content" />
            </div>
        </div>
    </div>
</section>

<style>
    section {
        border: 0;
        padding: 0;
    }

    .title {
        font-weight: bolder;
        font-size: var(--font-size-xx-large);
        color: var(--color-text);
        word-wrap: break-word;
        text-align: center;
    }

    .container {
        display: flex;
        flex-direction: column;
        align-items: stretch;
        background: var(--color-bg--sheet);
        box-shadow: 0 0 3rem var(--color-almost-black);
        min-height: 35rem;
        margin-top: 1.2em;
        margin-bottom: 0;
        border-radius: 1.5em;
        /* border-bottom-left-radius: 0;
        border-bottom-right-radius: 0; */
        color: var(--color-text);
        padding: 0;
    }

    .header {
        display: flex;
        box-sizing: border-box;
        text-align: left;
        margin-top: 0.2rem;
    }

    .optionsContainer {
        display: flex;
        flex-direction: column;
        align-items: stretch;
        margin: 0.5em 4em 1em 4em;
        color: var(--color-text);
    }
    .optionsBar {
        display: inline-flex;
        flex-flow: wrap;
        align-items: center;
        text-align: center;
        justify-content: center;
        border-radius: 3rem;
        margin: 0;
        font-size: var(--font-size-x-small);
        margin-top: var(--half-space) !important;
        padding: 0.4em;
        background: var(--color-bg--surface);
    }

    .optionButton {
        position: relative;
        display: inline-block;
        box-sizing: border-box;
        margin: 0;
        padding: 0.3em 0.8em;
        padding-left: 2em;
        font-weight: 500;
        text-decoration: none;
        border-radius: 3rem;
        white-space: nowrap;
        background-color: transparent;
        color: var(--color-txt);
    }

    .savingButton {
        position: relative;
        display: inline-block;
        box-sizing: border-box;
        margin: 0;
        padding: 0.3em 0.8em;
        padding-left: 2em;
        font-weight: 500;
        text-decoration: none;
        border-radius: 3rem;
        white-space: nowrap;
        background-color: transparent;
        color: var(--color-txt);
    }

    .interactive {
        cursor:pointer;
    }

    .optionButton::before {
        content: "";
        width: 1em;
        height: 1em;
        position: absolute;
        left: 0.75em;
        top: 50%;
        margin-top: -0.5em;
        background: center / 1em no-repeat;
        background-image: url("/static/clock.svg");
        filter: invert(99%) sepia(70%) saturate(310%) hue-rotate(294deg)
            brightness(103%) contrast(85%);
    }

    .active {
        background: var(--color-almost-black);
        color: var(--color-tertiary);
    }

    .active::before {
        background-image: url("/static/infinity.svg");
        filter: invert(46%) sepia(49%) saturate(835%) hue-rotate(204deg)
            brightness(100%) contrast(114%);
    }

    .header .left {
        flex: 0 auto;
        width: 3.625em;
        height: 3.625em;
        margin-block-end: 1em;
        margin-inline-start: 40px;
        margin-inline-end: 0.5em;
    }

    .header .center {
        justify-content: flex-start;
        width: 100%;
        white-space: nowrap;
        text-overflow: ellipsis;
        overflow: hidden;
        display: block;
        padding: 0.9rem 1em 0.3rem 0;
        max-width: calc(100% - 7rem);
    }

    .header .right {
        padding-top: 1.8rem;
        padding-right: 5rem;
        color: var(--color-text--subtle);
        font-size: var(--font-size-x-small);
        white-space: nowrap;
    }

    .from {
        font-weight: normal;
        font-size: var(--font-size-medium);
        line-height: 1.3em;
    }

    .to {
        color: var(--color-text--subtle);
        font-size: var(--font-size-x-small);
        margin: 0 !important;
        text-overflow: ellipsis;
        overflow: hidden;
    }

    .content {
        --color-bg--message-content: #fff;
        --color-txt--on-message-content: rgb(var(--rgb-almost-black));
        background: var(--color-bg--message-content);
        color: var(--color-txt--on-message-content);
        border-radius: 0 1.5em 1.5em 1.5em;
        padding: 2rem;
        margin: 0em 4em 0 5em;
        font-size: var(--font-size-small);
    }

    .avatar {
        width: 3.625em;
        height: 3.625em;
        border-radius: 100%;
        z-index: 1;
        display: block;
        position: relative;
        color: transparent;
        background-color: var(--color-bg--surface-glint-opaque);
        margin-top: 0.5rem;
    }

    iframe {
        border: none;
        width: 100%;
        height: 100%;
        margin: 0;
    }

   

    .loader,
    .loader:after {
        border-radius: 50%;
        width: 1.2em;
        height: 1.2em;
    }
    .loader {
        display: inline-block;
        margin: 0px 0px;
        font-size: 10px;
        position: absolute;
        left: 0.5em;
        text-indent: -9999em;
        border-top: .2em solid rgba(0, 0, 0, 0.2);
        border-right: .2em solid rgba(0, 0, 0, 0.2);
        border-bottom: .2em solid rgba(0, 0, 0, 0.2);
        border-left: .2em solid #000000;
        -webkit-transform: translateZ(0);
        -ms-transform: translateZ(0);
        transform: translateZ(0);
        -webkit-animation: load8 1.1s infinite linear;
        animation: load8 1.1s infinite linear;
        filter: invert(46%) sepia(49%) saturate(835%) hue-rotate(204deg)
            brightness(100%) contrast(114%);
    }
    @-webkit-keyframes load8 {
        0% {
            -webkit-transform: rotate(0deg);
            transform: rotate(0deg);
        }
        100% {
            -webkit-transform: rotate(360deg);
            transform: rotate(360deg);
        }
    }
    @keyframes load8 {
        0% {
            -webkit-transform: rotate(0deg);
            transform: rotate(0deg);
        }
        100% {
            -webkit-transform: rotate(360deg);
            transform: rotate(360deg);
        }
    }
</style>
