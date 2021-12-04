<script lang="ts">
	import { page } from "$app/stores";
	import { keyStore } from '$lib/keyStore';
	import Modal from '/src/components/Modal.svelte';
	import ModalItem from '/src/components/ModalItem.svelte'
	import { onMount } from 'svelte';

	import * as B64js from "base64-js";
	import { getWeavemailTransactions, decryptMail, getPrivateKey, getWalletName } from "$lib/myMail";
	import Arweave from "arweave";
	import { goto } from "$app/navigation";
	import config from "$lib/arweaveConfig";

	var arweave: any = Arweave.init(config);

	let wallet: any = null;

	let isOpenAvatarPopup: boolean = false;
	$: keys = $keyStore.keys;
	let gatewayUrl = $keyStore.gatewayUrl;

	function openAvatarPopup() {
		isOpenAvatarPopup = true;
	}

	function logout() {
		console.log("Logout happened");
		gatewayUrl = undefined;
		keys = null;
		$keyStore.keys = null;
		$keyStore.gatewayUrl = undefined;
		$keyStore.weaveMailInboxThreads = [];
		$keyStore.inboxThreads = [];
		$keyStore.isLoggedIn = false;
		isOpenAvatarPopup = false;
		backToInbox();
	}

	function authenticateWithGateway() {
		console.log(`Auth with gateway ${gatewayUrl}`);
		if (gatewayUrl == undefined) {
			gatewayUrl = "mail.pixelsamurai.com"
		} else {
			gatewayUrl = undefined;
		}
		$keyStore.gatewayUrl = gatewayUrl;
	}

	export function b64UrlDecode(b64UrlString: string): string {
		b64UrlString = b64UrlString.replace(/\-/g, "+").replace(/\_/g, "/");
		let padding;
		b64UrlString.length % 4 == 0
			? (padding = 0)
			: (padding = 4 - (b64UrlString.length % 4));
	return b64UrlString.concat("=".repeat(padding));
		}

	function b64UrlToBuffer(b64UrlString: string): Uint8Array {
		return new Uint8Array(B64js.toByteArray(b64UrlDecode(b64UrlString)));
	}

	async function doTransaction() {
		wallet = JSON.parse($keyStore.keys);
		let transaction = await arweave.createTransaction({ data: "asdf" }, wallet);
		await arweave.transactions.sign(transaction, wallet);
		console.log(transaction);
		let uploader = await arweave.transactions.getUploader(transaction);
		console.log(uploader);

		while (!uploader.isComplete) {
			await uploader.uploadChunk();
			console.log(`chunkUploaded: ${uploader.uploadedChunks}/${uploader.totalChunks} %${uploader.pctComplete}`);
   			console.log(` lastResponseStats: ${uploader.lastResponseStatus}`);
		}
	}

	async function testAuth () {
		wallet = JSON.parse($keyStore.keys);
		var address = await arweave.wallets.jwkToAddress(wallet);
		var result = await fetch(`http://localhost:5000/Mail/authToken?address=${address}`, {
			method: "GET", // *GET, POST, PUT, DELETE, etc.
			headers: {
				"accept": "*/*",
			}
		});
		
		let data = await result.text();
		console.log(data);
		let pk = await getPrivateKey(wallet);
		var byteData = b64UrlToBuffer(data);
		var decryptedResult = await window.crypto.subtle.decrypt(
		    {
		        name: 'RSA-OAEP'
		    },
		    pk,
		    byteData
		).catch(error => {
			console.log(error)
		}
		);

		console.log(decryptedResult);
	}

	function backToInbox() {
		console.log("headed back to Inbox");
		goto("../");
	}
</script>

<header>
	<div class="container">
		<div class="corner left">
			<div class="inboxButton" class:active={ $page.path != "/" } on:click={backToInbox} >Inbox</div>
			<!-- <a sveltekit:prefetch href="./search" class="search"> Search </a> -->
			<!-- <a href="." class="search"> Search </a> -->
		</div>

		<div class="center">
			<!-- <a sveltekit:prefetch href="./weave">MyMail</a> -->
			Weavemail
			<div class="demo">(alpha test)</div>
		</div>
	
	{#if keys != null || $keyStore.isLoggedIn}
		<div class="corner right">
			<button on:click={openAvatarPopup}>
				<div alt="ProfileImage" class="downArrow"></div>
				<div alt="ProfileImage" class="profileImage">
			</button>
		</div>
	{/if}
	<!-- Avatar pooup -->
	<Modal bind:isOpen={isOpenAvatarPopup}>
		<div slot="header">
			MENU
		</div>
		<div slot="content">
			<!-- <ModalItem imageUrl="{$page.path == "/" ? "" : "../"}gateway.svg" onClick={authenticateWithGateway}>
				{#if gatewayUrl }
				{gatewayUrl}
				{:else}
				Email gateway
				{/if}
			</ModalItem>  -->
			<ModalItem imageUrl="{$page.path == "/" ? "" : "../"}plus.svg" onClick={doTransaction}>Test Auth</ModalItem>
			<ModalItem imageUrl="{$page.path == "/" ? "" : "../"}logout.svg" onClick={logout}>Log out</ModalItem>
		</div>
	</Modal>
	</div>
</header>


<style>
	header {
		display: flex;
		justify-content: space-between;
		position: sticky;
		top: 0;
		left: 0;
		right: 0;
		z-index: 20;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		background: var(--rgb-background);
		height:3em;
		color: var(--color-text);
		font-size: var(--font-size-medium);

		--color-bg--surface-glint: rgba(var(--rgb-gray), 0.1);
	}

	header a {
		color: var(--color-text);
	}

	.container {
		position: relative;
		display: flex;
		align-items: center;
		width:100%;
		max-width: 1100px;
		flex-direction: column;
		justify-content: space-between;
	}


	.left {
		left: 0.5em;
		height :50%;
	}

	.inboxButton {
		--base-space: 1.25em;
		--quarter-space: calc(var(--base-space) / 4);
		margin-right: var(--quarter-space) !important;
		padding: 0.3em 0.8em;
		padding-left: 1.7em;
		width: 5.5em;
		max-width: 5em;
		text-align: center;
		background: var(--color-primary);
		border-color: var(--color-primary);
		color: var(--color-almost-black);
		display: none;
		margin: 0;
		font-weight: 500;
		text-decoration: none;
		border-radius: 3rem;
		white-space: nowrap;
		box-sizing: border-box;
	}

	.inboxButton::before {
		content: "";
		width: 1em;
		height: 1em;
		position: absolute;
		left: 0.5em;
		top: 50%;
		margin-top: -0.5em;
		background: center / 1em no-repeat;
		background-image: url("/src/lib/header/back-icon.svg");
	}

	.active {
		display: inline-block;
	}

	.center {
		text-align: center;
	}

	.demo {
		color: var(--color-text--subtle);
		font-size: var(--font-size-xx-small);
	}

	.right {
		right: 1em;
		flex-direction: row-reverse;
	}

	.right button {
		display:flex;
		flex-direction: row-reverse;
		background-color: transparent;
		border:0;
	}

	.profileImage {
		vertical-align: middle;
		width:2em;
		height:2em;
		border-radius: 50%;
		position: relative;
		z-index: 0;
		right: 0.75em;
		color: var(--color-text);
		background-image: url("/static/img_avatar.png");
		background-size: 2em 2em;
	}

	.downArrow {
		content: "";
		width: 0.5em;
		height: 0.5em;
		position: absolute;
		top: 60%;
		margin-top: -0.5em;
		background: center / 0.5em no-repeat;
		background-image: url("/src/lib/header/down-arrow.svg");
		filter: invert(99%) sepia(70%) saturate(310%) hue-rotate(294deg)
			brightness(103%) contrast(85%);
	}

	.search {
		position: relative;
		z-index: 0;
		padding-top: 0.4em;
		padding-bottom: 0.4em;
		padding-right: 0.8em;
		background: var(--color-bg--surface-glint);
		padding-left: 2.2em;
		color: var(--color-text);
		margin: 0;
		font-weight: 500;
		text-decoration: none;
		border-radius: 3rem;
		white-space: nowrap;
	}

	.search::before {
		content: "";
		width: 1em;
		height: 1em;
		position: absolute;
		left: 0.75em;
		top: 50%;
		margin-top: -0.5em;
		background: center / 1em no-repeat;
		background-image: url("/src/lib/header/search-icon.svg");
		filter: invert(99%) sepia(70%) saturate(310%) hue-rotate(294deg)
			brightness(103%) contrast(85%);
	}

	.corner {
		width: 20%;
		height: 3em;
		display: flex;
		align-items: center;
		height: 100%;
		position: absolute;
		top: 0;
	}

	.corner a {
		align-items: center;
		justify-content: left;
		width: 100%;
	}

</style>
