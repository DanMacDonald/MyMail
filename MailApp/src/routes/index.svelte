<script lang="ts">
	export const prerender = true;

	import { onMount } from "svelte";
	import { fade } from "svelte/transition";
	import { goto } from "$app/navigation";
	import type { InboxItem } from "$lib/types";
	import { getFormattedTime, getExpireLabel } from "$lib/formattedTime";
	import { sentMessage } from "$lib/routedEventStore";
	import { keyStore } from "$lib/keyStore";
	import KeyDropper from "/src/components/KeyDropper.svelte";
	import Arweave from "arweave";
	import { getWeavemailTransactions, decryptMail, getPrivateKey, getWalletName } from "$lib/myMail";

	// Used for testing a cold start
	// $keyStore.keys = null;
    // $keyStore.gatewayUrl = "";
    // $keyStore.weaveMailInboxItems = [];
    // $keyStore.emailInboxItems = [];
    // $keyStore.inboxItems = [];
	// $keyStore.isLoggedIn = false;

	let promise = Promise.resolve($keyStore.inboxItems);
    let isLoadingMessages:boolean = false;
	let gatewayUrl = "";

	let wallet: any = null;
	var arweave: any = Arweave.init({
		host: "arweave.net",
		port: 443,
		protocol: "https",
	});

    let keys = $keyStore.keys;
	let isLoggedIn = $keyStore.isLoggedIn;
	let _inboxItems:InboxItem[] = [];
	
	keyStore.subscribe((store) => {
		console.log(`subscribe changed ${isLoggedIn} ${store.isLoggedIn}`);
		if (isLoggedIn !== store.isLoggedIn) {
			isLoggedIn = store.isLoggedIn;
			if ($keyStore.weaveMailInboxItems.length == 0 && !isLoadingMessages) {
				pageStartupLogic();
			}
		} else if (keys != store.keys) {
            keys = store.keys;
			isLoggedIn = true;
			$keyStore.isLoggedIn = true;
            if (keys == null) {
                $keyStore.inboxItems = [];
            } else {
                console.log("isLoadingMessages:" + isLoadingMessages)
                if ($keyStore.weaveMailInboxItems.length == 0 && !isLoadingMessages) {
                    pageStartupLogic();
                } else {
                    $keyStore.inboxItems = $keyStore.weaveMailInboxItems;
                }
            }
        } else if (gatewayUrl != store.gatewayUrl) {
			gatewayUrl = store.gatewayUrl;
			if (!store.gatewayUrl) {
				if ($keyStore.emailInboxItems.length > 0) {
					$keyStore.emailInboxItems = [];
					$keyStore.inboxItems = $keyStore.weaveMailInboxItems;
				}
			} else {
				getEmailInboxItems()
					.then(emailInboxItems => {
						$keyStore.emailInboxItems = emailInboxItems;
						mergeInboxItems(<InboxItem[]>emailInboxItems);
						console.log("email items loaded async");
					})
			}
		}
    });

	onMount(async () => {
		if ($sentMessage) fadeOutFlash();
        pageStartupLogic()
	});

    function pageStartupLogic() {
        if ($keyStore.keys != null) {
			wallet = JSON.parse($keyStore.keys);
		} else if ($keyStore.isLoggedIn) {
			wallet = null;
		} else {
			return;
		}

        // Make sure we have a wallet initialized
        console.log("Wallet loaded");

		promise = Promise.resolve($keyStore.inboxItems);
        
        if (!$keyStore.inboxItems || $keyStore.inboxItems.length == 0) {
            isLoadingMessages = true;
            console.log("start loading messages")
            // We don't have any mailItems, use the loading promise to show "Waiting..."
            promise = getWeavemailItems()
                .then(weaveMailItems => {
                    $keyStore.weaveMailInboxItems = weaveMailItems;
                    mergeInboxItems(<InboxItem[]>weaveMailItems);
                    console.log("weavemail items loaded async");
                    isLoadingMessages = false;
                    return weaveMailItems;
                });
        } else {
			_inboxItems = $keyStore.inboxItems;
            // Don't use the loading promise here, we want to load these in the background.
            getWeavemailItems()
                .then(weaveMailItems => {
                    $keyStore.weaveMailInboxItems = weaveMailItems;
                    mergeInboxItems(<InboxItem[]>weaveMailItems);
                    console.log("weavemail items loaded async");
                })
        }

        if ($keyStore.gatewayUrl && $keyStore.gatewayUrl != null) {
            getEmailInboxItems()
                .then(emailInboxItems => {
                    $keyStore.emailInboxItems = emailInboxItems;
                    mergeInboxItems(<InboxItem[]>emailInboxItems);
                    console.log("emails loaded async");
                });
        } else {
            console.log("Skipping emails")
        }
    }

    function mergeInboxItems(newItems: InboxItem[] ) {
        if (!$keyStore.inboxItems || $keyStore.inboxItems.length == 0) {
            if (newItems.length > 0) {
                $keyStore.inboxItems = newItems;
				_inboxItems = newItems;
            }
        } else {
            let inboxItems: InboxItem[] = $keyStore.inboxItems;
            let itemsToAdd: InboxItem[] = [];
			let isSortNeeded = false;

			// O(n^2) lets do it! (Open to better ideas)
            for(let j=0; j<newItems.length; j++) {
                let newItem = newItems[j];
                let foundExistingItem = false;
                for(let i =0; i < inboxItems.length; i++) {
                    let item = inboxItems[i];
                    foundExistingItem = newItem.id == item.id;
					// use txid to compare on-chain messages when available
					if (foundExistingItem && newItem.txid != null) {
						foundExistingItem = newItem.txid == item.txid;
					}
                    if (foundExistingItem) {
						// Update the status of the existing item
						if (item.isSeen != newItem.isSeen) {
							isSortNeeded = true;
							item.isSeen = newItem.isSeen;
						}
						break;
					}
                }
                if (foundExistingItem == false) itemsToAdd.push(newItem);
            }

            if (itemsToAdd.length > 0) {
                inboxItems = inboxItems.concat(itemsToAdd);
                isSortNeeded = true;
            }

			if (isSortNeeded) {
				inboxItems.sort((item1, item2) => {
					if (item1.isSeen != item2.isSeen) {
						if (item1.isSeen == false) 
							return -1;
						else
							return 1;
					}
					return item2.timestamp - item1.timestamp;
				});
                $keyStore.inboxItems = inboxItems;
			}
			_inboxItems = inboxItems;
        }
    }

    async function getEmailInboxItems() {
        let creds = {
			emailAddress: "admin@pixelsamurai.com", 
			password: "asdfd",
		};

		//console.log(JSON.stringify(creds));

		const res = await fetch(`http://localhost:5000/Mail/inbox`, {
			method: "POST", // *GET, POST, PUT, DELETE, etc.
			headers: {
				"Content-Type": "application/json",
			},
			body: JSON.stringify(creds),
		});

		// const res = await fetch('inbox.json');
		const text = await res.text();

		let parsedItems = <InboxItem[]> JSON.parse(text);
		let emailInboxItems = parsedItems;
		for (var item of emailInboxItems) {
			item.timestamp = Date.parse(item.date);
		}

		if (res.ok) {
			return emailInboxItems;
		} else {
			throw new Error(text);
		}
    }

	async function getActiveAddress(wallet?) : Promise<string> {
		if (wallet != null) {
			return await arweave.wallets.jwkToAddress(wallet);
		} else if ($keyStore.isLoggedIn) {
			return await window.arweaveWallet.getActiveAddress();
		} 
	}

    async function getWeavemailItems() {		
		var address = await getActiveAddress(wallet);
		let json = await getWeavemailTransactions(address);
		var weaveMailInboxItems = await Promise.all(
            json.data.transactions.edges.map(async function (edge, i) {
				let txid = edge.node.id;
                var transaction = await arweave.transactions.get(txid);

				let timestamp = 0;
				let appVersion = "";

				// Parse timestamp info from the transaction
				transaction.get("tags").forEach((tag) => {
                    let key = tag.get("name", { decode: true, string: true });
                    let value = tag.get("value", {
                        decode: true,
                        string: true,
                    });
                    if (key === "Unix-Time") timestamp = parseInt(value) * 1000;
                    if (key === "App-Version") appVersion = value;
                });

				var fromAddress = await arweave.wallets.ownerToAddress(transaction.owner);
				var fromName =  await getWalletName(arweave, fromAddress);
				var fee = arweave.ar.winstonToAr(transaction.reward);
				var amount = arweave.ar.winstonToAr(transaction.quantity);
				
// TODO: Extract this out into a decryptMail that uses arweave or arconnect
				// var key = await getPrivateKey(wallet);
                // let mailParse;

                // var data = await arweave.transactions.getData(txid);
                // mailParse = JSON.parse(
                //     await arweave.utils.bufferToString(
                //         await decryptMail(arweave,
                //             arweave.utils.b64UrlToBuffer(data),
                //             key
                //         )
                //     )
                // );
				let mailParse =  <any> await getMessageJSON(txid, wallet);

				let inboxItem: InboxItem = {
					to: "You",
					from: `${fromName}`,
					fromAddress: `${fromAddress}`,
					date: "",
					subject: mailParse.subject || "null",
					id: 0,
					isFlagged: false,
					isRecent: false,
					isSeen: true,
					contentType: "weavemail",
					timestamp: timestamp,
					body: mailParse.body,
                    txid: txid
				};

				return inboxItem;
			}
		));

        return weaveMailInboxItems;
    }

	async function getMessageJSON(txid : string, wallet) : Promise<any> {

		let data = await arweave.transactions.getData(txid);
		if (wallet != null) {
			let key = await getPrivateKey(wallet);
			console.log(key);
			let mailParse = JSON.parse(
				await arweave.utils.bufferToString( 
					await decryptMail(arweave, arweave.utils.b64UrlToBuffer(data), key)
				)
			);
			return mailParse;
		} else {
			let decryptString = await window.arweaveWallet.decrypt(arweave.utils.b64UrlToBuffer(data),{ algorithm: "RSA-OAEP", hash: "SHA-256" });
			let mailParse = JSON.parse(decryptString);
			return mailParse;
		}
	}

	function handleInboxItemClick(inboxItem: InboxItem) {
		localStorage.inboxItem = JSON.stringify(inboxItem);
		goto("message/view");
	}

	function handleNewMessageClick() {
		goto("message/write");
	}

	function fadeOutFlash() {
		$sentMessage = false;
	}
</script>

<svelte:head>
	<title>Inbox</title>
</svelte:head>
<section>
	{#if $keyStore.isLoggedIn == false}
		<KeyDropper />
	{:else}
		{#if $sentMessage}
			<div out:fade={{ delay: 1300, duration: 400 }} class="flashRow">
				<span class="flash">Message Sent</span>
			</div>
		{/if}
		{#await promise}
			<p>...waiting</p>
		{:then number}
			<div class="container">
				<div class="header">
					<h1>Inbox</h1>
					<div class="actions">
						<div on:click={handleNewMessageClick}>New Message</div>
					</div>
				</div>
				{#each _inboxItems as item, i}
					{#if i == 0 && !item.isSeen}
						<article><div class="unseen"><span>NEW FOR YOU</span></div></article>
					{/if}
					{#if gatewayUrl != null && (item.isSeen && (i == 0 || !_inboxItems[i-1].isSeen))}
						<article><div class="previous" >PREVIOUSLY SEEN</div></article>
					{/if}
					<article>
						<div class="inboxItem" on:click={() => handleInboxItemClick(item)} class:seen={item.isSeen}>
							<div class="itemContainer">
								<div class="left">
									<span class:status={item.isSeen == false}></span>
									<img
										src="img_avatar.png"
										alt="ProfileImage"
										class="avatar"
									/>
								</div>
								<div class="center">
									<span class="subject">{item.subject}</span>
									<div class="byline" class:myMail={item.contentType=="weavemail"}>
										{item.from}
									</div>
								</div>
								<div class="right">
									{getFormattedTime(item.timestamp)}
									<div class="expires">
										{#if item.contentType == "weavemail"}
											<img class="infinity" alt="infinity" src ="infinity3.svg" />
										{:else}
											expires in {getExpireLabel(item.timestamp,90)}
										{/if}
									</div>
								</div>
							</div>
						</div>
					</article>
				{/each}
			</div>
		{:catch error}
			<p style="color: red">{error.message}</p>
		{/await}
	{/if}
</section>

<style>
	section {
		border: 0;
		padding: 0;
		position: relative;
	}
	.flashRow {
		width: 100%;
		text-align: center;
		align-content: center;
		font-size: var(--font-size-medium);
		position: absolute;
		top: -1rem;
		z-index: 25;
	}
	.flash {
		background: rgba(var(--rgb-almost-white), 0.5);
		border-radius: 3rem;
		padding: 0.5rem 1rem;
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
		margin-left: 3rem;
		margin-right: 3rem;
	}

	article {
		width: 100%;
		/* --sheet-padding: 2em;
		padding-left: var(--sheet-padding);
		padding-right: var(--sheet-padding);
		margin-left: calc(var(--sheet-padding) * -1);
		margin-right: calc(var(--sheet-padding) * -1); */
		display: block;
		position: relative;
		box-sizing: border-box;
		order: 1;
	}

	.previous {
		background-color: var(--color-bg--main-thin);
		font-weight: bold;
		font-size: var(--font-size-x-small);
		line-height: 1.3em;
		padding: 1.5rem 1rem 1rem 4rem
	}

	.unseen {
		position: relative;
		z-index: 2;
		margin-right: 4rem;
		margin-bottom: 1rem;
	}

	.unseen:before {
		content: '';
		position: absolute;
		left: 0;
		right: 0;
		top: 50%;
		height: 1px;
		z-index: -1;
		background: linear-gradient(135deg, var(--color-secondary) 0%, var(--color-tertiary) 100%);
	}

	.unseen span {
		background: var(--color-bg--sheet);
		font-weight: bold;
		font-size: var(--font-size-x-small);
		line-height: 1.3em;
		padding: 0rem 1rem 0rem 4rem
	}

	.header {
		position: relative;
		color: var(--color-text);
		width: 100%;
		height: 3em;
		line-height: 5em;
		text-align: center;
		margin-top: 2rem;
	}

	.header h1 {
		font-weight: bolder;
		margin: 0;
		font-size: var(--font-size-xx-large);
		line-height: 1;
		box-sizing: border-box;
	}

	.header .actions {
		font-weight: normal;
		position: absolute;
		display: flex;
		font-size: var(--font-size-small);
		line-height: 1.4;
		top: -1rem !important;
		right: 1rem !important;
	}

	.actions div {
		border: 1px solid;
		margin: 0;
		padding: 0.3em 0.8em;
		font-weight: 500;
		text-decoration: none;
		border-radius: 3rem;
		white-space: nowrap;
		background-color: transparent;
		border-color: var(--color-tertiary);
		color: var(--color-tertiary);
		padding-left: 2.2em;
		position: relative;
	}

	.actions div::before {
		content: " ";
		width: 1em;
		height: 1em;
		position: absolute;
		left: 0.75em;
		top: 50%;
		color: var(--color-tertiary);
		z-index: 10;
		margin-top: -0.5em;
		background: center / 1em no-repeat;
		background-image: url("/src/lib/header/plus.svg");
		filter: invert(46%) sepia(49%) saturate(835%) hue-rotate(204deg)
			brightness(100%) contrast(114%);
	}

	.avatar {
		width: 2.25em;
		height: 2.25em;
		border-radius: 100%;
		z-index: 1;
		display: block;
		position: relative;
		color: transparent;
		background-color: var(--color-bg--surface-glint-opaque);
		margin-top: 0.5rem;
	}

	.inboxItem {
		display: flex;
		/* justify-content: flex-start; */
		max-width: 1200px;
		height: 3.25em;
		vertical-align: middle;
		cursor: pointer;
		padding-left:2em;
		padding-right:2em;
	}

	.seen {
		background-color: var(--color-bg--main-thin);
	}

	.itemContainer {
		width:100%;
		display: flex;
		justify-content: flex-start;
		max-width: 1200px;
		height: 3.25em;
		vertical-align: middle;
		cursor: pointer;
		/* margin-left: 2em; */
	}

	.itemContainer:hover {
		--color-bg--secondary-glint: rgba(var(--rgb-blue), 0.05);
		background: var(--color-bg--secondary-glint) !important;
	}

	.inboxItem .left {
		flex: 0 auto;
		padding: 0 0.5rem 0 1rem;
	}

	.status {
		position: relative;
		height: 1px;
		width: 1px;
		clip: rect(1px, 1px, 1px, 1px);
		overflow: hidden;
		text-transform: none;
		white-space: nowrap;
		--sheet-padding: 2em;
	}

	.status:before {
		display: block;
		filter: invert(59%) sepia(94%) saturate(2285%) hue-rotate(358deg) brightness(100%) contrast(103%);
		content: ' ';
		position: absolute;
		top: 1.3em;
		left: -0.7em;
		width: 0.5em;
		height: 0.5em;
		background: url("/static/unread.svg") center/100% no-repeat;
		/* background-image: url("/src/lib/header/back-icon.svg"); */
	}

	.inboxItem .center {
		justify-content: flex-start;
		width: 100%;
		white-space: nowrap;
		text-overflow: ellipsis;
		overflow: hidden;
		display: block;
		padding: 0.9rem 1em 0.3rem 0;
		max-width: calc(100% - 7rem);
	}

	.inboxItem .right {
		padding-top: 1.5rem;
		padding-right: 1rem;
		color: var(--color-text--subtle);
		font-size: var(--font-size-x-small);
		white-space: nowrap;
		text-align: right;
	}

	.inboxItem .expires {
		color: rgb(var(--rgb-dark-gray));
		font-size: var(--font-size-xx-small);
	}

	.subject {
		font-weight: normal;
		font-size: var(--font-size-medium);
		line-height: 1.3em;
	}

	.byline {
		color: var(--color-text--subtle);
		font-size: var(--font-size-x-small);
		margin: 0 !important;
		text-overflow: ellipsis;
		overflow: hidden;
		padding-left: 1.5em
	}

	.byline:before {
		content: " ";
		width: 1em;
		height: 1em;
		position: absolute;
		color: var(--color-tertiary);
		top: 2.55em;
		left: 7em;
		z-index: 10;
		background: center / 1em no-repeat;
		background-image: url("/static/email.svg");
		/* filter: invert(39%) sepia(91%) saturate(809%) hue-rotate(190deg) brightness(101%) contrast(105%); */
		filter: invert(99%) sepia(70%) saturate(310%) hue-rotate(294deg)
			brightness(65%) contrast(85%);
	}

	.myMail:before {
		content: " ";
		width: 1em;
		height: 1em;
		position: absolute;
		color: var(--color-tertiary);
		top: 2.55em;
		left: 7em;
		z-index: 10;
		background: center / 1em no-repeat;
		background-image: url("/static/identity2.svg");
		filter: invert(46%) sepia(49%) saturate(835%) hue-rotate(204deg)
			brightness(100%) contrast(114%);
		/* filter: invert(99%) sepia(70%) saturate(310%) hue-rotate(294deg)
			brightness(75%) contrast(85%); */
	}

	.infinity {
		margin-top:0.3em;
		filter: invert(31%) sepia(2%) saturate(3162%) hue-rotate(197deg) brightness(88%) contrast(83%);
		height: 0.7em;
	}
</style>
