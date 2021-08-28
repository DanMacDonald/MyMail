import type { GraphqlQuery } from "$lib/types";

// Simple arweave graphql API client
export async function query({ query, variables = null }: GraphqlQuery) {
  const graphql = JSON.stringify({
    query,
    variables,
  });
  const requestOptions = {
    method: "POST",
    headers: {
      "content-type": "application/json",
    },
    body: graphql,
  };
  const res = await fetch("https://arweave.net/graphql", requestOptions)
  return await res.clone().json();
}