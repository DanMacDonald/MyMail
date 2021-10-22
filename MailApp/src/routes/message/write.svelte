<script lang="ts">
	import { goto } from '$app/navigation';
	import type { Message } from '$lib/types';
	import { sentMessage } from '$lib/routedEventStore';
	import { keyStore } from "$lib/keyStore";
	import Arweave from "arweave";
	import {getPublicKey, encryptMail } from "$lib/myMail";

	var arweave: any = Arweave.init({
		host: "arweave.net",
		port: 443,
		protocol: "https",
	});


	let message : Message = {
		toAddress: "",
		toName: "",
		fromAddress: "admin@pixelsamurai.com",
		fromName: "Admin",
		subject: "",
		body: "", id:0, fee:0, amount:null, txid:"", appVersion:"", timestamp:0
	}

	let isEmail = true;
	
	function handleSubmit() {
		if (isEmail) {
			submitEmail();
		} else {
			submitWeavemail();
		}
	}

	function submitEmail() {
		console.log(message);
		fetch(`http://localhost:5000/Mail`, 
		{
    		method: 'POST', // *GET, POST, PUT, DELETE, etc.
			headers: {
      			'Content-Type': 'application/json'
			},
			body: JSON.stringify(message)
		})
		.then(response => response.json())
		.then(data => console.log(data))
		.then(() => {
			$sentMessage = true;
			goto("../");
		});
	}

	async function submitWeavemail(){

		let address = "";
		let wallet = null;
		if ($keyStore.keys != null) {
			wallet = JSON.parse($keyStore.keys);
			address = await arweave.wallets.jwkToAddress(wallet);
		} else {
			address = await window.arweaveWallet.getActiveAddress();
		}

		let tokens = '0';
		if (message.amount > 0 ) {
            tokens = arweave.ar.arToWinston(message.amount.toString())
        }

		var pub_key = await getPublicKey(arweave, message.toAddress);
		if (pub_key == undefined) {
			alert('Error: Recipient has to send a transaction to the network, first!');
			return
		}

		if (address == message.toAddress) {
			alert('"Error: Cannot send mail to yourself"');
			return;
		}

        var content = await encryptMail(arweave, message.body, message.subject, pub_key)
        console.log(content)

		
		var tx = await arweave.createTransaction({
			target: message.toAddress,
			data: arweave.utils.concatBuffers([content]),
			quantity: tokens
		}, wallet);

		tx.addTag('App-Name', 'permamail'); // Add permamail tag
		tx.addTag('App-Version', '0.0.2'); // Add version tag
		tx.addTag('Unix-Time', Math.round((new Date()).getTime() / 1000)); // Add Unix timestamp

		await arweave.transactions.sign(tx, wallet)
		console.log(tx.id)
		await arweave.transactions.post(tx)
		.then(() => {
			$sentMessage = true;
			goto("../");
		})
	
	}

	function parseToAddress() {
		if (message.toAddress.includes("@")) {
			isEmail = true;
		} else {
			isEmail = message.toAddress.length < 40;
		}
	}


</script>
<svelte:head>
	<title>Write</title>
</svelte:head>
<section>
    <div class="container">
    <div class="header">
        New Message
    </div>
    <article>
		<form on:submit|preventDefault={() => handleSubmit()}>
            <div class="inputRow">
                <div class="label">To</div>
				<div class="inputField"><input bind:value={message.toAddress} on:input={parseToAddress}></div>
            </div>
            <div class="inputRow">
                <div class="label">Subject</div>
				<div class="inputField"><input bind:value={message.subject}></div>
            </div>
            <div class="messageRow">
                <textarea class="message" bind:value={message.body} placeholder="Type your message..."></textarea>
            </div>
			<div class="submitRow">
				<div class="footer">
				{#if isEmail}
					<input class="submitButton" type="submit" value="Send email">
				{:else}
					<input class="submitButton myMail" type="submit" value="Send MyMail">
					<div class="amount"><input bind:value={message.amount} placeholder="0 AR" /></div>
				{/if}
				</div>
			</div>
        </form>
    </article>
    </div>
</section>

<style>
	
	section {
		border: 0;
		padding:0;
	}

	.container {
		display: flex;
		font-size: var(--font-size-medium);
		flex-direction: column;
		align-items: center;
		background: var(--color-bg--sheet);
		box-shadow: 0 0 3rem var(--color-almost-black);
		min-height: calc(100vh);
		margin-bottom: 0;
		border-radius: 1.5em;
		border-bottom-left-radius: 0;
		border-bottom-right-radius: 0;
		color: var(--color-text);
		padding: 1.5rem;
	}

	article {
		display: block;
		width: 100%;
	}

	.inputRow {
		display: flex;
		border-bottom: 1px solid var(--color-border);
		padding: 0.2rem 0 0 0;
	}

	.label {
		line-height: 3rem;
	}

	.submitRow {
		color: var(--color-txt--reversed);
	}

	input {
		max-width: 100%;
		background-color: transparent;
		border-color: transparent;
		border-radius: 0.6rem;
		line-height: inherit;
		font-family: inherit;
		font-weight: inherit;
		font-size: inherit;
		color: inherit;
		-webkit-appearance: none;
		-moz-appearance: none;
		box-sizing: border-box;
	}

	.message {
		word-wrap: break-word;
		word-break: break-word;
		line-height: 1.4;
		font-family: inherit;
		font-size: var(--font-size-medium);
		width: 100%;
		border: 0;
		outline: none;
		resize: none;
		padding: 1rem 0;
		min-height: 13em;
		background-color: transparent;
		color: var(--color-text);
	}

	.submitButton {
		cursor:pointer;
		border: 0;
		line-height: 2rem;
    	font-family: inherit;
		margin: 0;
		margin-left: 0.5em;
		padding: 0.3em 0.8em;
		font-weight: 500;
		text-decoration: none;
		background: var(--color-secondary);
    	border-color: var(--color-secondary);
		color: var(--color-txt--reversed);
		border-radius: 3rem;
		right: 2em;
		flex-direction: row-reverse;
	}

	.myMail {
		background: var(--color-tertiary);
    	border-color: var(--color-tertiary);
	}

	.inputField {
		position: relative;
		display: flex;
		flex-wrap: wrap;
		border-radius: 0.6rem;
		padding: 0 0 0 0.5em;
		border: 2px solid transparent;
		flex:1;
	}

	.inputField input {
		width:100%;
	}

	.footer {
		display: flex;
		flex-direction: row-reverse;
		height: 1.9em;
		align-items:flex-end;
		width:100%;
	}

	.amount {
		position: relative;
		z-index: 0;
		background-color: var(--rgb-background);
		color: var(--color-text);
		margin: 0;
		font-weight: 500;
		text-decoration: none;
		border-radius: 3rem;
		white-space: nowrap;
		line-height: 1.8rem;
    	font-family: inherit;
		padding: 0.3em 0.8em;
		padding-left: 2.2em;
	}

	.amount::before {
		content: "";
		width: 2em;
		height: 2em;
		position: absolute;
		left: 0.3em;
		top: 50%;
		margin-top: -1em;
		background: center / 1em no-repeat;
		background-image: url("/static/ar_coin.svg");
		filter: invert(99%) sepia(70%) saturate(310%) hue-rotate(294deg)
			brightness(103%) contrast(85%);
	}

	.amount input {
		width: 5em;
		line-height: 0rem;
		height: 1.2em;
		text-align: right;
	}
</style>