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
	date: string;
	subject: string;
	id: number;
	isFlagged: boolean;
	isRecent: boolean;
	isSeen: boolean;
	contentType: string;
	timestamp: number;
}

export interface Message {
	body: string;
	formAddress: string;
	fromName: string;
	subject: string;
	toAddress: string;
	toName: string;
}