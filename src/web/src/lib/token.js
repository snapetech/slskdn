import { tokenKey, tokenPassthroughValue } from '../config';
import {
  getLocalStorageItem,
  getSessionStorageItem,
  removeLocalStorageItem,
  removeSessionStorageItem,
  setStorageItem,
} from './storage';

export const getToken = () =>
  getSessionStorageItem(tokenKey) || getLocalStorageItem(tokenKey);
export const setToken = (storage, token) => setStorageItem(storage, tokenKey, token);
export const clearToken = () => {
  removeLocalStorageItem(tokenKey);
  removeSessionStorageItem(tokenKey);
};

export const isPassthroughEnabled = () => getToken() === tokenPassthroughValue;
