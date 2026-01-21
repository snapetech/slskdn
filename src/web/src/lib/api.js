import { apiBaseUrl } from '../config';
import { clearToken, getToken, isPassthroughEnabled } from './token';
import axios from 'axios';

axios.defaults.baseURL = apiBaseUrl;

const api = axios.create({
  withCredentials: true,
});

// Helper function to get CSRF token from cookie
const getCsrfToken = () => {
  const name = 'XSRF-TOKEN=';
  const cookies = document.cookie.split(';');
  for (let cookie of cookies) {
    cookie = cookie.trim();
    if (cookie.indexOf(name) === 0) {
      return cookie.slice(name.length);
    }
  }
  return null;
};

api.interceptors.request.use((config) => {
  const token = getToken();

  config.headers['Content-Type'] = 'application/json';

  if (!isPassthroughEnabled() && token) {
    config.headers.Authorization = 'Bearer ' + token;
  }

  // Add CSRF token for state-changing requests (POST/PUT/DELETE/PATCH)
  // Only needed if we're using cookie-based auth (no JWT token)
  const needsCsrf = ['post', 'put', 'delete', 'patch'].includes(
    (config.method || '').toLowerCase(),
  );

  if (needsCsrf) {
    const csrfToken = getCsrfToken();
    if (csrfToken) {
      config.headers['X-CSRF-TOKEN'] = csrfToken;
    }
  }

  return config;
});

api.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    if (
      error.response.status === 401 &&
      !['/session', '/server', '/application'].includes(
        error.response.config.url,
      )
    ) {
      console.debug('received 401 from api route, logging out');
      clearToken();
      window.location.reload(true);

      return Promise.reject(error);
    } else {
      return Promise.reject(error);
    }
  },
);

export default api;
