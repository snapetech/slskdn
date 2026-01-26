import { apiBaseUrl } from '../config';
import { clearToken, getToken, isPassthroughEnabled } from './token';
import axios from 'axios';

axios.defaults.baseURL = apiBaseUrl;

const api = axios.create({
  withCredentials: true,
});

/**
 * Builds an API URL from a relative path, ensuring no double-prefixing.
 * Throws in development if path includes /api or /api/v0 prefix.
 * @param {string} path - Relative path like "/profile/invite" or "profile/invite"
 * @returns {string} Full URL
 */
export function buildApiUrl(path) {
  // Guard against double-prefixing in development
  if (process.env.NODE_ENV !== 'production') {
    if (path.startsWith('/api/') || path.startsWith('api/')) {
      throw new Error(`[api.js] Do not include /api prefix in paths. Got: ${path}`);
    }
    if (path.startsWith('/api/v0') || path.startsWith('api/v0')) {
      throw new Error(`[api.js] Do not include /api/v0 prefix in paths. Got: ${path}`);
    }
  }
  const p = path.startsWith('/') ? path : `/${path}`;
  return `${apiBaseUrl}${p}`;
}

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
    // Enhance error with better diagnostics
    if (error.response) {
      const status = error.response.status;
      const url = error.response.config?.url || 'unknown';
      const contentLength = error.response.headers['content-length'];
      
      // Log empty body responses for debugging
      if (contentLength === '0' || contentLength === 0) {
        console.error(`[api.js] HTTP ${status} with empty body from ${url}`);
      }
      
      // Handle 401 (authentication)
      if (status === 401 &&
          !['/session', '/server', '/application'].includes(url)) {
        console.debug('received 401 from api route, logging out');
        clearToken();
        window.location.reload(true);
        return Promise.reject(error);
      }
    }
    
    return Promise.reject(error);
  },
);

export default api;
