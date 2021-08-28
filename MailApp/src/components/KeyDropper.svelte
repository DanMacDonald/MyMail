<script lang="ts">
    import { keyStore } from '$lib/keyStore';

    function onChangedHandler(event: Event) {
        let jwk = (<HTMLInputElement>event.target).files[0];
        let reader = new FileReader();
        reader.readAsText(jwk);
        reader.onload = () => {
            $keyStore.keys = reader.result.toString();;
        };
    }
</script>

<section>
    <div class="file-input">
        <input
            type="file"
            id="file"
            on:change={(e) => onChangedHandler(e)}
        />
        <div id="desc">Drop a keyfile to login.</div>
    </div>
</section>

<style>
     section {
        font-size: var(--font-size-small);
        color: var(--color-text);
    }

    .file-input {
        height: 200px;
        border: 2px dashed #62666f;
        text-align: center;
        display: flex;
        align-items: center;
        justify-content: center;
        position: relative;
        margin: auto;
        max-width: 300px;
        font-size: var(--font-size-medium);
        color: var(--color-text);
    }

    .file-input input[type="file"] {
        opacity: 0;
        position: absolute;
        background: none;
        width: 100%;
        height: 100%;
    }

    .file-input:hover {
        background-color: var(--color-bg--sheet);
    }
</style>