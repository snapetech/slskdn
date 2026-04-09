// <copyright file="registerServiceWorker.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import { urlBase } from './config';

const getServiceWorkerUrl = () => {
  const normalizedBase = urlBase && urlBase !== '/' ? urlBase : '';
  return `${normalizedBase}/service-worker.js`;
};

const getServiceWorkerScope = () => {
  const normalizedBase = urlBase && urlBase !== '/' ? urlBase : '';
  return normalizedBase ? `${normalizedBase}/` : '/';
};

export const registerServiceWorker = () => {
  if (
    typeof window === 'undefined' ||
    typeof navigator === 'undefined' ||
    !('serviceWorker' in navigator)
  ) {
    return;
  }

  const register = async () => {
    try {
      await navigator.serviceWorker.register(getServiceWorkerUrl(), {
        scope: getServiceWorkerScope(),
      });
    } catch (error) {
      console.debug('Service worker registration failed:', error);
    }
  };

  if (document.readyState === 'complete') {
    register();
    return;
  }

  window.addEventListener('load', register, { once: true });
};

export { getServiceWorkerScope, getServiceWorkerUrl };
