import { urlBase } from '../config';
import * as session from './session';

const baseUrl = `${urlBase}/api/v0/mesh`;

export const getStats = async () => {
  const response = await fetch(`${baseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get mesh stats: ${response.statusText}`);
  }

  return response.json();
};
