<script context="module" lang="ts">
    export const prerender = true;
    import type { Message } from '$lib/types';

    export async function load() {
        let inboxItem = JSON.parse(localStorage.inboxItem);
        console.log(inboxItem);
        const message = await getMessage(inboxItem);
        return { props: { message, inboxItem } };
    }

    async function getMessage(inboxItem : InboxItem) : Promise<Message> {
        const res = await fetch(`http://localhost:5000/Mail/message/${inboxItem.id}`, {mode: 'cors'});
		const text = await res.text();
        let msg : Message = JSON.parse(text);
        return msg;
    }
</script>

<script lang="ts">
	import { onMount } from 'svelte';
    import { getFormattedTime } from '$lib/formattedTime';

    import type { InboxItem } from '$lib/types';
    export let message : Message;
    export let inboxItem : InboxItem;

    function syncHeight() {
        // this resize wont work when the static pages are served from a different
        // origin than the webservice -DMac
        try {
            this.style.height = `${this.contentWindow.document.body.offsetHeight}px`
        } catch {
            console.log(`StyleHeight:${this.style.height}`);
            this.style.height = "2690px";
        }
    }

	onMount(async () => {
        console.log(`contentType:${inboxItem.contentType}`);
        if (inboxItem.contentType == "multipart/alternative" ) {
            // const frame = document.querySelector('iframe')
            // frame.addEventListener('load', syncHeight)
            // frame.src = `data:text/html;charset=utf-8,${escape(message.body)}`;
            const contentDiv = document.querySelector('.body');
            contentDiv.innerHTML = unescape(message.body);

        } else {
            const contentDiv = document.querySelector('.body');
            contentDiv.innerHTML = `<pre>${unescape(message.body)}</pre>`;
        }
	});
</script>

<section>
    <div class="title">{inboxItem.subject}</div>
    <div class="container">
        <div class="header">
            <div class="left">
                <img src="/img_avatar.png" alt="ProfileImage" class="avatar">
            </div>
            <div class="center">
                <span class="from">{message.fromName} <span class="to">{message.formAddress}</span></span>
                <div class="to">to {inboxItem.to}</div>
            </div>
            <div class="right">
                {getFormattedTime(inboxItem.timestamp)}
            </div>
        </div>
        <div class="content">
            <div class="body">
                <iframe id="iframe1" title="Message Content" ></iframe>
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
        margin-top: 3rem;
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
		padding-right: 3rem;
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
		margin-top:0.5rem;
	}

    iframe {
        border: none;
        width: 100%;
        height: 100%;
        margin: 0;
    }
</style>
