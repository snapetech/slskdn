import { urlBase } from '../config';
import * as session from './session';

const baseUrl = `${urlBase}/api/v0/port-forwarding`;

export const startForwarding = async (config) => {
  const response = await fetch(`${baseUrl}/start`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(config),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({ error: response.statusText }));
    throw new Error(errorData.error || `Failed to start port forwarding: ${response.statusText}`);
  }

  return response.json();
};

export const stopForwarding = async (localPort) => {
  const response = await fetch(`${baseUrl}/stop/${localPort}`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({ error: response.statusText }));
    throw new Error(errorData.error || `Failed to stop port forwarding: ${response.statusText}`);
  }

  return response.json();
};

export const getForwardingStatus = async () => {
  const response = await fetch(`${baseUrl}/status`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get forwarding status: ${response.statusText}`);
  }

  return response.json();
};

export const getForwardingStatusByPort = async (localPort) => {
  const response = await fetch(`${baseUrl}/status/${localPort}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    if (response.status === 404) {
      return null;
    }
    const errorData = await response.json().catch(() => ({ error: response.statusText }));
    throw new Error(errorData.error || `Failed to get forwarding status: ${response.statusText}`);
  }

  return response.json();
};

export const getAvailablePorts = async (startPort = 1024, endPort = 65535) => {
  const response = await fetch(`${baseUrl}/available-ports?startPort=${startPort}&endPort=${endPort}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get available ports: ${response.statusText}`);
  }

  return response.json();
};

