import api from './api';
import { rootUrl } from '../config';

export const getPartyDirectory = async () => {
  return (await api.get('/listening-party')).data || [];
};

export const getPartyState = async (podId, channelId) => {
  const response = await api.get(
    `/listening-party/${encodeURIComponent(podId)}/${encodeURIComponent(channelId)}`,
    { validateStatus: (status) => status === 200 || status === 204 },
  );

  return response.status === 204 ? null : response.data;
};

export const publishPartyState = async (podId, channelId, event) => {
  return (
    await api.post(
      `/listening-party/${encodeURIComponent(podId)}/${encodeURIComponent(channelId)}`,
      event,
    )
  ).data;
};

export const buildRadioStreamUrl = (party) => {
  if (!party?.streamPath) return null;
  return `${rootUrl}${party.streamPath}`;
};
