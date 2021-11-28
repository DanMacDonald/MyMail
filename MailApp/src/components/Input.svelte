<script>
	export let value, label, type;
	function typeAction(node) {
		node.type = type;
	}
</script>

<label>
	{label}
	<input use:typeAction bind:value>
</label>