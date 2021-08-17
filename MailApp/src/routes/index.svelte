<script lang="ts">

	import type { InboxItem } from '$lib/types';
	import { getFormattedTime } from '$lib/formattedTime';

	let inboxItems: InboxItem[] = [];

	/**
	 *  Retreives a list of inbox items for the current user
	 */
	async function getInbox() {

		let creds = {
			emailAddress: "admin@pixelsamurai.com",
			password: "asdfd"
		};

		//console.log(JSON.stringify(creds));

		const res = await fetch(`http://localhost:5000/Mail/inbox`, 
		{
    		method: 'POST', // *GET, POST, PUT, DELETE, etc.
			headers: {
      			'Content-Type': 'application/json'
			},
			body: JSON.stringify(creds)
		});
		
		// const res = await fetch('inbox.json');
		const text = await res.text();

		inboxItems = <InboxItem[]>JSON.parse(text);
		for (var item of inboxItems) {
			item.timestamp = Date.parse(item.date);
		}

		inboxItems.sort((item1, item2) =>  item2.timestamp - item1.timestamp );

		console.log(inboxItems);

		if (res.ok) {
			return inboxItems;
		} else {
			throw new Error(text);
		}
	}

	let promise = getInbox();

	function handleInboxItemClick(inboxItem: InboxItem) {
		localStorage.inboxItem = JSON.stringify(inboxItem);
		window.location.href = "message/view";
	}
</script>

<svelte:head>
	<title>Inbox</title>
</svelte:head>
<section>
	{#await promise}
		<p>...waiting</p>
	{:then number}
		<div class="container">
		<div class="title">Inbox</div>	
		{#each inboxItems as item, i}
			<article>
				<div class="inboxItem"  on:click={() => handleInboxItemClick(item)}>
					<div class="left">
						<img src="img_avatar.png" alt="ProfileImage" class="avatar">
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
</section>

<style>
	section {
		border: 0;
		padding:0;
	}

	.container {
		display: flex;
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
		padding: 0;
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

	.title {
		font-weight:bolder;
		font-size: var(--font-size-xx-large);
		color: var(--color-text);
		width: 100%;
		height: 7rem;
		line-height: 7rem;
		text-align: center;
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

	.inboxItem {
		display: flex;
		justify-content: flex-start;
		max-width: 1200px;
		height: 5rem;
		vertical-align: middle;
		cursor: pointer;
	}

	.inboxItem:hover {
		--color-bg--secondary-glint: rgba(var(--rgb-blue), 0.05);
		background:var(--color-bg--secondary-glint) !important
	}

	.inboxItem .left {
    	flex: 0 auto;
		width: 3.625em;
		height: 3.625em;
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
