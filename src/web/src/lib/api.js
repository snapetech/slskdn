import { apiBaseUrl } from '../config';
import { clearToken, getToken, isPassthroughEnabled } from './token';
import axios from 'axios';

axios.defaults.baseURL = apiBaseUrl;

const api = axios.create({
  withCredentials: true,
});

api.interceptors.request.use((config) => {
  const token = getToken();

  config.headers['Content-Type'] = 'application/json';

  if (!isPassthroughEnabled() && token) {
    config.headers.Authorization = 'Bearer ' + token;
  }

  return config;
});

api.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    // Don't reload if we're already on login page or for excluded endpoints
    const isLoginPage =
      window.location.pathname === '/' ||
      window.location.pathname.endsWith('/');
    const isExcludedEndpoint = ['/session', '/server', '/application'].includes(
      error.response?.config?.url,
    );

    if (error.response?.status === 401 && !isExcludedEndpoint && !isLoginPage) {
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
