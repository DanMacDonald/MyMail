<script lang="ts">
    import type { Message } from "$lib/types";
    import { getFormattedTime } from "$lib/formattedTime";
    import { keyStore } from "$lib/keyStore";
    import KeyDropper from "/src/components/KeyDropper.svelte";
    import Arweave from "arweave";

    let keys = $keyStore.keys;
    let wallet: any = null;
    var arweave: any = Arweave.init({
        host: "arweave.net",
        port: 443,
        protocol: "https",
    });

    let promise: Promise<any[]> = new Promise((resolve) => {
        if (keys == null) {
            return [];
        } else {
            console.log("Wallet loaded");
            wallet = JSON.parse(keys);
            if (wallet != null) return getWeavemailMessages();
        }
    });

    keyStore.subscribe((store) => {
        if (store.keys != null) {
            keys = store.keys;
            wallet = JSON.parse(keys);
            if (wallet != null && arweave.wallets != null) {
                promise = getWeavemailMessages();
            }
        }
    });

    /**
     * Queries arweave for the weavemail transactions sent to me
     */
    async function getWeavemailMessages(): Promise<Message[]> {
        var address = await arweave.wallets.jwkToAddress(wallet);

        // TODO: replace with ardb
        let query = {
            query: `query {
            transactions(recipients: ["${address}"],
                tags: [
                    {
                        name: "App-Name",
                    values: ["permamail"]
                    }
                ]
            ) {
                edges {
                    node {
                        id
                    }
                }
            }
        }`,
        };

        const requestOptions = {
            method: "POST",
            headers: {
                "content-type": "application/json",
            },
            body: JSON.stringify(query),
        };
        const res1 = await fetch("https://arweave.net/graphql", requestOptions);
        let json = await res1.clone().json();

        // Process the transactions returned by the ARQL query
        let inboxItems = await Promise.all(
            json.data.transactions.edges.map(async function (edge, i) {
                let inboxItem: any = {};
                let id = edge.node.id;
                var transaction = await arweave.transactions.get(id);

                // Initialize the unixTime from the transaction tags if possible
                inboxItem["unixTime"] = "0";
                transaction.get("tags").forEach((tag) => {
                    let key = tag.get("name", { decode: true, string: true });
                    let value = tag.get("value", {
                        decode: true,
                        string: true,
                    });
                    if (key === "Unix-Time")
                        inboxItem["unixTime"] = parseInt(value) * 1000;
                    if (key === "App-Version") inboxItem["appVersion"] = value;
                });

                inboxItem["txid"] = id;
                inboxItem["status"] = await arweave.transactions.getStatus(id);
                var from_address = await arweave.wallets.ownerToAddress(
                    transaction.owner
                );
                inboxItem["from"] = await getWalletName(from_address);
                inboxItem["fee"] = arweave.ar.winstonToAr(transaction.reward);
                inboxItem["amount"] = arweave.ar.winstonToAr(
                    transaction.quantity
                );
                inboxItem["data"] = transaction.data;
                inboxItem["format"] = transaction.format;

                return inboxItem;
            })
        );

        // For each of the messages, decrypt their contents
        let messages = await Promise.all(
            inboxItems.map(async function (inboxItem: any, i) {
                let message: Message = null;
                var key = await getPrivateKey(wallet);
                let mailParse;

                var data = await arweave.transactions.getData(inboxItem.txid);
                mailParse = JSON.parse(
                    await arweave.utils.bufferToString(
                        await decryptMail(
                            arweave.utils.b64UrlToBuffer(data),
                            key
                        )
                    )
                );

                message = {
                    id: 0,
                    body: mailParse.body,
                    fromAddress: inboxItem.from,
                    fromName: "",
                    toAddress: "",
                    toName: "",
                    subject: mailParse.subject,
                    fee: inboxItem.fee,
                    amount: inboxItem.amount,
                    txid: inboxItem.txid,
                    appVersion: inboxItem.appVersion,
                    timestamp: inboxItem.unixTime,
                };
                return message;
            })
        );

        return messages;
    }

    async function decryptMail(
        encryptedData: Uint8Array,
        privateKey: CryptoKey
    ): Promise<string> {
        // Split up the transaction data into the correct parts
        var symmetricKeyBytes = new Uint8Array(encryptedData.slice(0, 512));
        var mailBytes = new Uint8Array(encryptedData.slice(512));

        // Decrypt the symmetric key from the first part
        var symmetricKey = await window.crypto.subtle.decrypt(
            { name: "RSA-OAEP" },
            privateKey,
            symmetricKeyBytes
        );

        // Use the symmetric key to decrypt the mail from the last part
        return arweave.crypto.decrypt(mailBytes, symmetricKey);
    }

    async function getPrivateKey(wallet) {
        var walletCopy = Object.create(wallet);
        walletCopy.alg = "RSA-OAEP-256";
        walletCopy.ext = true;

        var algo = { name: "RSA-OAEP", hash: { name: "SHA-256" } };

        return await crypto.subtle.importKey("jwk", walletCopy, algo, false, [
            "decrypt",
        ]);
    }

    async function getWalletName(addr) {
        // TODO: replace with ardb
        let query = {
            query: `query {
            transactions(owners:["${addr}"],
                tags: [
                    {
                        name: "App-Name",
                        values: ["arweave-id"]
                    },
                    {
                    name: "Type",
                    values: ["name"]
                    }
                ]
            ) {
                edges {
                    node {
                        id
                    }
                }
            }
        }`,
        };

        const requestOptions = {
            method: "POST",
            headers: {
                "content-type": "application/json",
            },
            body: JSON.stringify(query),
        };
        const res1 = await fetch("https://arweave.net/graphql", requestOptions);
        let json = await res1.clone().json();

        if (json.data.transactions.edges.length == 0) {
            return addr;
        } else {
            const txid = json.data.transactions.edges[0].node.id;
            const tx = await arweave.transactions.get(txid);
            return tx.get("data", { decode: true, string: true });
        }
    }
</script>

<svelte:head />

<section>
    {#if keys != null}
        {#await promise}
            Retreiving messages...
        {:then messages}
            {#each messages as msg, i}
                from: {msg.fromAddress} <br />
                txid: {msg.txid}<br />
                fee: {msg.fee}<br />
                AR: {msg.amount}<br />
                appVersion: {msg.appVersion} <br />
                sent: {getFormattedTime(msg.timestamp)} <br />
                subject: {msg.subject} <br />
                body: {msg.body}<br />
                <br />
            {/each}
        {/await}
    {:else}
        <KeyDropper />
    {/if}
</section>

<style>
    section {
        font-size: var(--font-size-small);
        color: var(--color-text);
    }
</style>
