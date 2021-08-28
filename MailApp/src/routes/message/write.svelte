<script lang="ts">
	import { goto } from '$app/navigation';
	import type { Message } from '$lib/types';
	import { sentMessage } from '$lib/routedEventStore';

	let message : Message = {
		toAddress: "",
		toName: "",
		fromAddress: "admin@pixelsamurai.com",
		fromName: "Admin",
		subject: "",
		body: "", id:0, fee:0, amount:0, txid:"", appVersion:"", timestamp:0
	}
	
	function handleSubmit() {
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
			goto("/");
		})
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
				<div class="inputField"><input bind:value={message.toAddress}></div>
            </div>
            <div class="inputRow">
                <div class="label">Subject</div>
				<div class="inputField"><input bind:value={message.subject}></div>
            </div>
            <div class="messageRow">
                <textarea class="message" bind:value={message.body} placeholder="Type your message..."></textarea>
            </div>
			<div class="submitRow">
				<input class="submitButton" type="submit" value="Send email">
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
		display: inline-block;
		line-height: 2rem;
    	font-family: inherit;
		margin: 0;
		padding: 0.3em 0.8em;
		font-weight: 500;
		text-decoration: none;
		background: var(--color-secondary);
    	border-color: var(--color-secondary);
		color: var(--color-txt--reversed);
		border-radius: 3rem;
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
</style>