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
    import { submitWeavemail, getThreadId } from "$lib/myMail";
    import { getFormattedTime, getFormattedDate } from "$lib/formattedTime";
    import { scrollToBottom } from '$lib/scrollTo/svelteScrollTo.js';
    import { sineInOut } from "svelte/easing";
    import type { InboxItem } from "$lib/types";
    import SubmitRow from '/src/components/SubmitRow.svelte';
    import ComposeRow from '/src/components/ComposeRow.svelte';
    import { sentMessage } from '$lib/routedEventStore';
    import { goto } from '$app/navigation';
    import Arweave from "arweave";
	import config from "$lib/arweaveConfig";
    import { keyStore } from "$lib/keyStore";
    var arweave: any = Arweave.init(config);

    export let message: Message;
    export let inboxItem: InboxItem;


    let isPermanent = false;
    let promise = Promise.resolve();
    let isReplying = false;
    const focus = node => node.focus();
    let innerHeight;
    $: options = {
        easing: sineInOut,
		offset: -innerHeight,
		duration: 400,
        delay: 200
    }

    $: toAddress = isAddressValid(message.fromAddress) ? message.fromAddress : message.fromName;
    $: replaySubject =  message.subject ? makeReplySubject(message.subject) : "";
    $: isValid = toAddress ? isAddressValid(toAddress) : false;
    let arAmount = null;

    function makeReplySubject(subject: string): string {
        var upper = subject.toUpperCase();
        if (upper.startsWith("RE:"))
            return subject;
        else 
            return `Re: ${subject}`;
    }

    async function onSubmit() {
        console.log("submit requested");

        // Tidy up the response message of client side styles
        // so it's suitable for sending
        const bodyElement = document.querySelector("#replyBody");
        var doc = document.implementation.createHTMLDocument("");
        var imported = document.importNode(bodyElement, true);
        doc.body.append(imported);
        var replyText = doc.querySelector("#replyText");
        replyText.removeAttribute("contenteditable");
        replyText.removeAttribute("style");
        const messageBody = doc.querySelector("#replyBody").innerHTML;
        console.log(messageBody);
        console.log(`toAddress:${toAddress} replySubject:${replaySubject} arAmount:${parseFloat(arAmount)}`);
    
        let address = "";
		let wallet = null;
		if ($keyStore.keys != null) {
			wallet = JSON.parse($keyStore.keys);
			address = await arweave.wallets.jwkToAddress(wallet);
		} else {
			address = await window.arweaveWallet.getActiveAddress();
		}

		if (address == message.toAddress) {
			alert('"Error: Cannot send mail to yourself"');
			return;
		}

		await submitWeavemail(arweave, toAddress, replaySubject, messageBody, parseInt(arAmount), wallet)
		.then(() => {
			$sentMessage = true;
			goto("../");
		})
    }

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
            contentDiv.innerHTML = message.body;;
        } else {
            contentDiv.innerHTML = `<pre>${unescape(message.body)}</pre>`;
        }
        markMessageAsSeen(inboxItem);
    });

    async function startReplying() {
        isReplying = true;
        scrollToBottom(options);
        console.log(message);
        var replyElement = document.getElementById("replyBody");
        replyElement.innerHTML = `<div id="replyText" style="outline: none;" contenteditable >
    <br/>
    <br/>
</div>
On ${getFormattedDate(message.timestamp)}, ${message.fromName} wrote:
<blockquote style="padding-inline-start: 1.5rem;
margin: 0;
padding-inline-end: 0;
border-inline-start: 1px solid rgba( 27, 39, 51, 0.15);">${message.body}</blockquote>`;

        const div = <HTMLElement>replyElement.querySelector("#replyText");
        setTimeout(function() {
            div.focus();
        }, 0);

        getThreadId(message.subject)
            .then(d => console.log(d));
    }

    function isAddressValid(address:string):boolean {
		let re = /^[a-zA-Z0-9_\-]{43}$/;
		return re.test(address);
	}
</script>
<svelte:window bind:innerHeight />
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
                <img src="../img_avatar.png" alt="ProfileImage" class="avatar"/>
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
    {#if !isReplying}
        <div class="page-toolbar">
            <div class="content1">
                <span class="item">
                    <div class="action" on:click={startReplying}>
                        Reply Now
                    </div>
                </span>
            </div>
        </div>
    {/if}
    <div class="container hiding" class:visible={isReplying}>
        <article>
            <div class="inputRow">
                <ComposeRow bind:toAddress={toAddress} bind:subject={replaySubject} />
            </div>
            <div id="replyBody" class="content reply">
                <!-- message reply body is injected to make sure it's not picking up svelt markup -->
            </div>
            <br/>
            <div class="inputRow"><SubmitRow on:submit={onSubmit} bind:isMessageReady={isValid} bind:amount={arAmount} /></div>
        </article>
    </div>
  
    
</section>

<style>
    section {
        border: 0;
        padding: 0;
        min-height: calc(100vh);
        padding-bottom: 10rem;
        margin-bottom: 280px;
    }

    article {
		display: block;
		width: 100%;
        font-size: var(--font-size-small);
	}

    /* blockquote {
        border-left: solid 1px gray;
        margin: 0;
        padding-block: 0;
        padding-inline-start: 1.5rem;
        padding-inline-end: 0;
        border-inline-start: 1px solid var(--color-border--popup);
    } */

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
        color: var(--color-text);
        padding:1.5rem;
        padding-bottom: 5em;
    }

    .hiding {
        display: none;
    }
    .visible { 
        display: flex;
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
        margin-inline-start: 10px;
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
        padding-right: 3.5rem;
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
        margin: 0em 2em 0 3.5em;
        font-size: var(--font-size-medium);
        line-height: 1.4em;
    }

    .content div {
        outline: none;
    }

    .reply {
        min-height: 13em;
    }

    .inputRow {
        margin: 0em 2em 0 3.5em;
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

    .page-toolbar {
        position: sticky;
        max-width: 12rem;
        margin-left: auto;
        margin-right: auto;
        bottom: 0;
        padding: 1rem 0;
        z-index: 8;
        border-radius: 3rem 3rem 0 0;
    }

    .content1 {
        padding: 0.5rem;
        background: linear-gradient(135deg, var(--color-bg--secondary-glint) 0%, var(--color-bg--secondary-glint) 80%) var(--color-bg--main-thick);
        border-radius: 1.6rem;
        display: flex;
        justify-content: space-around;
    }

    .page-toolbar .item {
        height:5rem;
        width:8rem;
        font-size: var(--font-size-x-small);
        color: var(--color-text);
    }

    .item .action {
        display: flex;
        flex-direction: column;
        align-items: center;
        text-align: center;
        width: 100%;
        background: none;
        border: 0;
        padding: 0.5rem;
        text-decoration: none;
        color: inherit;
        cursor: pointer;
        position: relative;
        font-size: var(--font-size-x-small);
        font-weight: 500;
        border-radius: 1.2rem;
    }

    .item .action::before {
        content: "";
        filter: invert(99%) sepia(70%) saturate(310%) hue-rotate(294deg) brightness(103%) contrast(85%);
        background: center / 2.2rem no-repeat;
        background-image: url("/static/reply.svg");
        width: 4rem;
        height: 2.5rem;
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
