import { writable } from 'svelte-local-storage-store'
import type { InboxItem } from "$lib/types";

// First param `keyStore` is the local storage key.
// Second param is the initial value.
export const keyStore = writable('keyStore', {
    keys: "",
    gatewayUrl: "",
    weaveMailInboxItems: [],
    emailInboxItems: [],
    inboxItems: []
});