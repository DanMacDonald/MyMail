import * as B64js from "base64-js";

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

export async function encryptMail(
    arweave: any,
    content: string, 
    subject: string,
    pub_key
) : Promise<string> {
    var content_encoder = new TextEncoder();
    var newFormat = JSON.stringify({ 'subject': subject, 'body': content });
    var mail_buf = content_encoder.encode(newFormat);
    var key_buf = await generateRandomBytes(256);

    // Encrypt data segments
    var encrypted_mail = await arweave.crypto.encrypt(mail_buf, key_buf);
    var encrypted_key =
        await window.crypto.subtle.encrypt(
            {
                name: 'RSA-OAEP'
            },
            pub_key,
            key_buf
        );

    // Concatenate and return them
    return arweave.utils.concatBuffers([encrypted_key, encrypted_mail]);
}


async function generateRandomBytes (length) {
    var array = new Uint8Array(length)
    window.crypto.getRandomValues(array)
    return array
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

export async function getPublicKey (arweave, address) {
    var txid = await arweave.wallets.getLastTransactionID(address)

    if (txid == '') {
        return undefined
    }

    var tx = await arweave.transactions.get(txid)

    if (tx == undefined) {
        return undefined
    }

    var keyData = {
        kty: 'RSA',
        e: 'AQAB',
        n: tx.owner,
        alg: 'RSA-OAEP-256',
        ext: true
    }

    var algo = { name: 'RSA-OAEP', hash: { name: 'SHA-256' } }

    return await crypto.subtle.importKey('jwk', keyData, algo, false, ['encrypt'])
}

export async function getWeavemailTransactions(arweave, address: string): Promise<any> {
    
    // TODO: replace with ardb
    const res2 = await arweave.api.post("/graphql", {
        query: `
        {
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
    })
    return res2.data;
}

export async function getWalletName(arweave, address) {
    // TODO: replace with ardb
    const res2 = await arweave.api.post("/graphql", {
        query: `
        {
            transactions(owners:["${address}"],
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
    })

    const response = res2.data;

    if (response.data.transactions.edges.length == 0) {
        return address;
    } else {
        const txid = response.data.transactions.edges[0].node.id;
        const tx = await arweave.transactions.get(txid);
        return tx.get("data", { decode: true, string: true });
    }
}

export async function submitWeavemail(arweave, toAddress, subject, body, amount:number, wallet) {

    let tokens = '0';
    if (amount > 0 ) {
        tokens = arweave.ar.arToWinston(amount.toString())
    }

    var pub_key = await getPublicKey(arweave, toAddress);
    if (pub_key == undefined) {
        alert('Error: Recipient has to send a transaction to the network, first!');
        return
    }

    var content = await encryptMail(arweave, body, subject, pub_key)
    console.log(content)

    var tx = await arweave.createTransaction({
        target: toAddress,
        data: arweave.utils.concatBuffers([content]),
        quantity: tokens
    }, wallet);

    tx.addTag('App-Name', 'permamail'); // Add permamail tag
    tx.addTag('App-Version', '0.0.2'); // Add version tag
    tx.addTag('Unix-Time', Math.round((new Date()).getTime() / 1000)); // Add Unix timestamp

    await arweave.transactions.sign(tx, wallet)
    console.log(tx.id)
    await arweave.transactions.post(tx)
}

export async function getThreadId(subjectLine: string) : Promise<string> {
    // remove anything in square brackets
    var subject = subjectLine.replace(/(\[.*?\])/g, '');

    // remove all words followed by a colon from the beginning of the string
    if (subject.includes(`:`)){
        let wordArray = subject.split(` `);
        while (wordArray[0].endsWith(':')) {
            wordArray = wordArray.slice(1);
        }
        subject = wordArray.join(``);
    }

    // remove any remaining whitespace
    subject = subject.replace(/\s/g, '');

    // create a sha-256 hash of the pruned subject line, and b64 encode it
    const encoder = new TextEncoder();
    const data = encoder.encode(subject);
    const hashBuffer = await crypto.subtle.digest('SHA-256', data);
    const hashArray = new Uint8Array(hashBuffer); // convert buffer to byte array
    const b64UrlHash = bufferTob64Url(hashArray);
    return b64UrlHash;
}

function b64UrlEncode(b64UrlString: string): string {
  return b64UrlString
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/\=/g, "");
}

function bufferTob64(buffer: Uint8Array): string {
  return B64js.fromByteArray(new Uint8Array(buffer));
}

export function bufferTob64Url(buffer: Uint8Array): string {
    return b64UrlEncode(bufferTob64(buffer));
}