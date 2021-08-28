export async function decryptMail(
    arweave: any,
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

export async function getPrivateKey(wallet) {
    var walletCopy = Object.create(wallet);
    walletCopy.alg = "RSA-OAEP-256";
    walletCopy.ext = true;

    var algo = { name: "RSA-OAEP", hash: { name: "SHA-256" } };

    return await crypto.subtle.importKey("jwk", walletCopy, algo, false, [
        "decrypt",
    ]);
}

export async function getWeavemailTransactions(address: string): Promise<any> {
    
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
    return json;
}

export async function getWalletName(arweave, addr) {
    // TODO: replace with ardb
    let query = { "query" : `query {
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
    }`};

    const requestOptions = {
        method: "POST",
        headers: {
        "content-type": "application/json",
        },
        body: JSON.stringify(query),
    };
    const res1 = await fetch("https://arweave.net/graphql", requestOptions)
    let json = await res1.clone().json();

    if (json.data.transactions.edges.length == 0) {
        return addr;
    } else {
        const txid = json.data.transactions.edges[0].node.id;
        const tx = await arweave.transactions.get(txid);
        return tx.get("data", { decode: true, string: true });
    }
}