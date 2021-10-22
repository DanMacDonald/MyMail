/**
 * Can be made globally available by placing this
 * inside `global.d.ts` and removing `export` keyword
 */
export interface Locals {
	userid: string;
}

export interface InboxItem {
	to: string;
	from: string;
	fromAddress: string;
	date: string;
	subject: string;
	id: number;
	isFlagged: boolean;
	isRecent: boolean;
	isSeen: boolean;
	contentType: string;
	timestamp: number;
	body: string;
	txid: string;
}

export interface Message {
	id: number;
	body: string;
	fromAddress: string;
	fromName: string;
	subject: string;
	toAddress: string;
	toName: string;
	fee: number;
	amount: number;
	txid: string;
	appVersion: string;
	timestamp: number;
}

export interface GraphqlQuery {
	query: DocumentNode | string;
	variables: Record<string, any> | null;
}