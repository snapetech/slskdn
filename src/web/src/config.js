const urlBase = (window.urlBase === '/' ? '' : window.urlBase) || '';
const developmentPort =
  window.port ?? (import.meta.env.VITE_SLSKD_PORT || 5_030);
const rootUrl =
  import.meta.env.PROD
    ? urlBase
    : import.meta.env.VITE_USE_ABSOLUTE_API_URL === 'true'
      ? `http://localhost:${developmentPort}${urlBase}`
      : urlBase;
const apiBaseUrl = `${rootUrl}/api/v0`;
const hubBaseUrl = `${rootUrl}/hub`;
const tokenKey = 'slskd-token';
const tokenPassthroughValue = 'n/a';
const activeChatKey = 'slskd-active-chat';
const activeRoomKey = 'slskd-active-room';
const activeUserInfoKey = 'slskd-active-user';

export {
  activeChatKey,
  activeRoomKey,
  activeUserInfoKey,
  apiBaseUrl,
  hubBaseUrl,
  rootUrl,
  tokenKey,
  tokenPassthroughValue,
  urlBase,
};
