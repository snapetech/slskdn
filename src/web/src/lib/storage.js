export const getLocalStorageItem = (key, fallback = null) => {
  if (typeof window === 'undefined') return fallback;

  try {
    const value = window.localStorage.getItem(key);
    return value === null ? fallback : value;
  } catch {
    return fallback;
  }
};

export const setLocalStorageItem = (key, value) => {
  if (typeof window === 'undefined') return false;

  try {
    window.localStorage.setItem(key, value);
    return true;
  } catch {
    return false;
  }
};

export const removeLocalStorageItem = (key) => {
  if (typeof window === 'undefined') return false;

  try {
    window.localStorage.removeItem(key);
    return true;
  } catch {
    return false;
  }
};

export const getLocalStorageKeys = () => {
  if (typeof window === 'undefined') return [];

  try {
    return Array.from(
      { length: window.localStorage.length },
      (_, index) => window.localStorage.key(index),
    ).filter(Boolean);
  } catch {
    return [];
  }
};

export const getSessionStorageItem = (key, fallback = null) => {
  if (typeof window === 'undefined') return fallback;

  try {
    const value = window.sessionStorage.getItem(key);
    return value === null ? fallback : value;
  } catch {
    return fallback;
  }
};

export const setSessionStorageItem = (key, value) => {
  if (typeof window === 'undefined') return false;

  try {
    window.sessionStorage.setItem(key, value);
    return true;
  } catch {
    return false;
  }
};

export const setStorageItem = (storage, key, value) => {
  try {
    storage.setItem(key, value);
    return true;
  } catch {
    return false;
  }
};

export const removeSessionStorageItem = (key) => {
  if (typeof window === 'undefined') return false;

  try {
    window.sessionStorage.removeItem(key);
    return true;
  } catch {
    return false;
  }
};
