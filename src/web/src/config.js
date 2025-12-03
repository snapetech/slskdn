const urlBase = (window.urlBase === '/' ? '' : window.urlBase) || '';
// eslint-disable-next-line n/no-process-env
const developmentPort = window.port ?? (process.env.REACT_APP_SLSKD_PORT || 5030);
const rootUrl =
  // eslint-disable-next-line n/no-process-env
  process.env.NODE_ENV === 'production'
    ? urlBase
    : `http://localhost:${developmentPort}${urlBase}`;
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
