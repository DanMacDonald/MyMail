<script lang="ts">
	import { page } from "$app/stores";
</script>

<header>
	<div class="corner left">
		<a class:active={$page.path != "/"} class="inboxButton" sveltekit:prefetch href="/">Inbox</a>
		<a sveltekit:prefetch href="/search" class="search"> Search </a>
	</div>

	<div>
		<a sveltekit:prefetch href="/">MyMail</a>
	</div>
	
	<div class="corner right">
		<a sveltekit:prefetch href="/todos">
			<div alt="ProfileImage" class="downArrow"></div>
			<img src="/img_avatar.png" alt="ProfileImage" class="profileImage">
		</a>
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
		z-index: 9;
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

	.right {
		right: 1em;
		flex-direction: row-reverse;
	}

	.right a {
		display:flex;
		flex-direction: row-reverse;
	}

	.profileImage {
		vertical-align: middle;
		width: 50px;
		height: 50px;
		border-radius: 50%;
		position: relative;
		z-index: 0;
		right: 0.75em;
		color: var(--color-text);
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

	.corner img {
		width: 2em;
		height: 2em;
		object-fit: contain;
	}

</style>
