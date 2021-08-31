<script lang="ts">
	export const prerender = true;

	import { onMount } from "svelte";
	import { fade } from "svelte/transition";
	import { goto } from "$app/navigation";
	import type { InboxItem } from "$lib/types";
	import { getFormattedTime } from "$lib/formattedTime";
	import { sentMessage } from "$lib/routedEventStore";
	import { keyStore } from "$lib/keyStore";
	import KeyDropper from "/src/components/KeyDropper.svelte";
	import Arweave from "arweave";
	import { getWeavemailTransactions, decryptMail, getPrivateKey, getWalletName } from "$lib/myMail";

	let promise = Promise.resolve($keyStore.inboxItems);
    let isLoadingMessages:boolean = false;

	let wallet: any = null;
	var arweave: any = Arweave.init({
		host: "arweave.net",
		port: 443,
		protocol: "https",
	});

    let keys = $keyStore.keys;
	
	keyStore.subscribe((store) => {
        if (keys != store.keys) {
            keys = store.keys;
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
        }
    });

	onMount(async () => {
		if ($sentMessage) fadeOutFlash();
        pageStartupLogic()
	});

    function pageStartupLogic() {
        if ($keyStore.keys == null) return;

        // Make sure we have a wallet initialized
        console.log("Wallet loaded");
        wallet = JSON.parse($keyStore.keys);
        
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
                    return null;
                });
        } else {
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
            }
        } else {
            let inboxItems: InboxItem[] = $keyStore.inboxItems;
            let itemsToAdd: InboxItem[] = [];

			let isSortNeeded = false;
            for(let j=0; j<newItems.length; j++) {
                let newItem = newItems[j];
                let foundExistingItem = false;
                for(let i =0; i < inboxItems.length; i++) {
                    let item = inboxItems[i];
                    foundExistingItem = newItem.id == item.id || newItem.txid == item.txid;
                    if (foundExistingItem) {
						if (newItem.isSeen != item.isSeen) {
							item.isSeen = newItem.isSeen;
							isSortNeeded = true;
						}
					} 
                }

                if (foundExistingItem == false) itemsToAdd.push(newItem);
            }

			if (isSortNeeded || itemsToAdd.length > 0) {
				if (itemsToAdd.length > 0)
					inboxItems = inboxItems.concat(itemsToAdd);
					
				inboxItems.sort((item1, item2) => item2.timestamp - item1.timestamp);
				$keyStore.inboxItems = inboxItems;
			}
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

    async function getWeavemailItems() {
		console.log($keyStore.weaveMailInboxItems);
		
		var address = await arweave.wallets.jwkToAddress(wallet);
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

				var key = await getPrivateKey(wallet);
                let mailParse;

                var data = await arweave.transactions.getData(txid);
                mailParse = JSON.parse(
                    await arweave.utils.bufferToString(
                        await decryptMail(arweave,
                            arweave.utils.b64UrlToBuffer(data),
                            key
                        )
                    )
                );

				let inboxItem: InboxItem = {
					to: address,
					from: `${fromName}`,
					date: "",
					subject: mailParse.subject || "null",
					id: 0,
					isFlagged: false,
					isRecent: false,
					isSeen: false,
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


	function handleInboxItemClick(inboxItem: InboxItem) {
		localStorage.inboxItem = JSON.stringify(inboxItem);
		goto("message/view");
	}

	function fadeOutFlash() {
		$sentMessage = false;
	}
</script>

<svelte:head>
	<title>Inbox</title>
</svelte:head>
<section>
	{#if keys == null}
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
						<a sveltekit:prefetch href="/message/write"
							>New Message</a
						>
					</div>
				</div>
				{#each $keyStore.inboxItems as item, i}
					<article>
						<div
							class="inboxItem"
							on:click={() => handleInboxItemClick(item)}
						>
							<div class="left">
								<img
									src="img_avatar.png"
									alt="ProfileImage"
									class="avatar"
								/>
							</div>
							<div class="center">
								<span class="subject">{item.subject}</span>
								<div class="byline">{item.from}</div>
							</div>
							<div class="right">
								{getFormattedTime(item.timestamp)}
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
	}
	.flashRow {
		width: 100%;
		text-align: center;
		align-content: center;
		font-size: var(--font-size-medium);
		position: absolute;
		top: 5rem;
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
		padding: 1.5rem;
	}

	article {
		width: 100%;
		--sheet-padding: 2em;
		padding-left: var(--sheet-padding);
		padding-right: var(--sheet-padding);
		margin-left: calc(var(--sheet-padding) * -1);
		margin-right: calc(var(--sheet-padding) * -1);
		display: block;
		position: relative;
		box-sizing: border-box;
		order: 1;
	}

	.header {
		position: relative;
		color: var(--color-text);
		width: 100%;
		height: 5em;
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
		top: -2rem !important;
		right: 0rem !important;
	}

	.actions a {
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

	.actions a::before {
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
		justify-content: flex-start;
		max-width: 1200px;
		height: 3.25em;
		vertical-align: middle;
		cursor: pointer;
	}

	.inboxItem:hover {
		--color-bg--secondary-glint: rgba(var(--rgb-blue), 0.05);
		background: var(--color-bg--secondary-glint) !important;
	}

	.inboxItem .left {
		flex: 0 auto;
		padding: 0 0.5rem 0;
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
		padding-top: 1.8rem;
		padding-right: 1rem;
		color: var(--color-text--subtle);
		font-size: var(--font-size-x-small);
		white-space: nowrap;
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
	}
</style>
